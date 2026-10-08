"""HTTP regression test using a disposable SQLite database. Run after dotnet build."""
import datetime as dt
import html
import http.cookiejar
import json
import os
from pathlib import Path
import re
import socket
import sqlite3
import subprocess
import tempfile
import time
import urllib.error
import urllib.parse
import urllib.request
import uuid

ROOT = Path(__file__).resolve().parents[1]
class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, *args):
        return None

with tempfile.TemporaryDirectory(prefix="s3-03-http-", dir=ROOT) as temporary:
    database = Path(temporary) / "test.db"
    with socket.socket() as sock:
        sock.bind(("127.0.0.1", 0))
        port = sock.getsockname()[1]
    base = f"http://127.0.0.1:{port}"
    env = dict(os.environ, DatabaseProvider="Sqlite", ConnectionStrings__DefaultConnection=f"Data Source={database}",
               ASPNETCORE_ENVIRONMENT="Development", Logging__LogLevel__Default="Error")
    server = subprocess.Popen(["dotnet", str(ROOT / "SalonManagement/bin/Debug/net8.0/SalonManagement.dll"), "--urls", base],
                              cwd=ROOT / "SalonManagement", env=env, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL,
                              creationflags=subprocess.CREATE_NO_WINDOW if os.name == "nt" else 0)
    streams = []
    con = None
    try:
        def client():
            return urllib.request.build_opener(urllib.request.ProxyHandler({}), urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()), NoRedirect())
        anonymous, desk, stylist, owner = client(), client(), client(), client()
        def request(opener, path, payload=None, token=None, form=False):
            data = (urllib.parse.urlencode(payload).encode() if form else json.dumps(payload).encode()) if payload is not None else None
            headers = {"Content-Type": "application/x-www-form-urlencoded" if form else "application/json"}
            if token:
                headers["Authorization"] = "Bearer " + token
            req = urllib.request.Request(base + path, data=data, headers=headers)
            try:
                with opener.open(req, timeout=10) as result:
                    return result.status, result.read().decode()
            except urllib.error.HTTPError as result:
                return result.code, result.read().decode()
        for _ in range(120):
            try:
                if request(anonymous, "/admin/login")[0] == 200:
                    break
            except (OSError, urllib.error.URLError):
                pass
            if server.poll() is not None:
                raise AssertionError("Application exited before becoming ready")
            time.sleep(0.25)
        else:
            raise AssertionError("Application startup timed out")
        def login(opener, email, password, portal):
            status, body = request(opener, "/api/auth/login", {"email": email, "password": password, "portal": portal})
            assert status == 200, (portal, status)
            return json.loads(body)
        desk_tokens = login(desk, "receptionist@salon.local", "Reception123!", "reception")
        stylist_tokens = login(stylist, "stylist@salon.local", "Stylist123!", "stylist")
        owner_tokens = login(owner, "owner@salon.local", "Owner123!", "admin")
        assert request(anonymous, "/api/auth/management-session", token=owner_tokens["accessToken"])[0] == 200
        assert request(anonymous, "/api/auth/management-session", token=desk_tokens["accessToken"])[0] == 403
        for opener, path in [(desk, "/Appointments"), (stylist, "/StylistNotifications"), (owner, "/StylistLinks"), (owner, "/admin")]:
            assert request(opener, path)[0] == 200, path
        assert request(stylist, "/Appointments")[0] == 302
        assert request(desk, "/StylistLinks")[0] == 302
        assert request(anonymous, "/api/appointments/1/check-in", {})[0] == 401
        print("PASS login cookies, Razor pages and role restrictions")
        now = dt.datetime.now(dt.timezone.utc).astimezone(dt.timezone(dt.timedelta(hours=7))).replace(tzinfo=None)
        con = sqlite3.connect(database)
        c = con.cursor()
        c.execute("INSERT INTO Customers (FullName,Phone,IsActive,CreatedAt) VALUES (?,?,?,?)", ("HTTP Test", "0999998888", 1, now.isoformat(" ")))
        customer = c.lastrowid
        ids = []
        for delta in [0, -40, -30]:
            start = now + dt.timedelta(minutes=delta)
            end = start + dt.timedelta(minutes=90)
            c.execute("INSERT INTO Appointments (CustomerId,StylistId,AppointmentDate,StartTime,EndTime,Status,CreatedAt,LateMinutes,Version) VALUES (?,?,?,?,?,?,?,?,?)",
                      (customer, 1, start.strftime("%Y-%m-%d 00:00:00"), start.strftime("%H:%M:%S"), "23:59:59", "Confirmed", now.isoformat(" "), 0, str(uuid.uuid4()).upper()))
            ids.append(c.lastrowid)
        con.commit()
        stream = stylist.open(base + "/StylistNotifications/Stream", timeout=10)
        streams.append(stream)
        assert stream.readline().strip() == b": connected"
        stream.readline()
        assert request(anonymous, f"/api/appointments/{ids[0]}/check-in", {}, stylist_tokens["accessToken"])[0] == 403
        started = time.monotonic()
        status, body = request(anonymous, f"/api/appointments/{ids[0]}/check-in", {}, desk_tokens["accessToken"])
        assert status == 200, (status, body)
        assert json.loads(body)["status"] == "Arrived"
        event = stream.readline().decode()
        assert event.startswith("data: "), event
        assert "HTTP Test" in json.loads(event[6:])["Message"]
        assert time.monotonic() - started < 5
        assert request(anonymous, f"/api/appointments/{ids[0]}/check-in", {}, desk_tokens["accessToken"])[0] == 409
        print("PASS check-in API, duplicate rejection and immediate SSE")
        def csrf(page):
            return html.unescape(re.search(r'name="__RequestVerificationToken"[^>]*value="([^"]+)"', page).group(1))
        # Confirm the actual form posts operation instead of MVC's reserved action value.
        c.execute("UPDATE Appointments SET Status='Confirmed',CheckedInAt=NULL WHERE AppointmentId=?", (ids[2],)); con.commit()
        status, page = request(desk, "/Appointments")
        assert 'name="operation"' in page
        status, body = request(desk, "/Appointments/Change", {"id": ids[2], "operation": "check-in", "date": now.strftime("%Y-%m-%d"), "__RequestVerificationToken": csrf(page)}, form=True)
        assert status == 302
        assert c.execute("SELECT Status FROM Appointments WHERE AppointmentId=?", (ids[2],)).fetchone()[0] == "Arrived"
        assert request(desk, "/Appointments/Change", {"id": ids[2], "operation": "check-in"}, form=True)[0] == 400
        assert c.execute("SELECT COUNT(*) FROM StylistNotifications").fetchone()[0] == 2
        print("PASS Razor check-in form and CSRF protection")
        # Link/unlink through the management form and verify inbox isolation.
        status, page = request(owner, "/StylistLinks")
        stylist_user = c.execute("SELECT Id FROM AspNetUsers WHERE Email='stylist@salon.local'").fetchone()[0]
        status, _ = request(owner, "/StylistLinks/Link", {"userId": stylist_user, "stylistId": 2, "__RequestVerificationToken": csrf(page)}, form=True)
        assert status == 302
        status, body = request(stylist, "/StylistNotifications/Latest")
        assert status == 200 and json.loads(body) == []
        status, page = request(owner, "/StylistLinks")
        assert request(owner, "/StylistLinks/Link", {"userId": stylist_user, "stylistId": 1, "__RequestVerificationToken": csrf(page)}, form=True)[0] == 302
        print("PASS management linking and notification isolation")
        status, _ = request(desk, "/api/auth/logout", {"refreshToken": desk_tokens["refreshToken"]})
        assert status == 204 and request(desk, "/Appointments")[0] == 302
        con.close()
        print("PASS logout clears the browser session")
    finally:
        for stream in streams:
            stream.close()
        if con is not None:
            con.close()
        server.terminate()
        try:
            server.wait(timeout=10)
        except subprocess.TimeoutExpired:
            server.kill(); server.wait()
