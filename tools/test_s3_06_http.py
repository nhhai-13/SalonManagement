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

with tempfile.TemporaryDirectory(prefix="s3-06-http-", dir=ROOT) as temporary:
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
        for opener, path in [(desk, "/NoShows"), (desk, "/NoShows/Create"), (owner, "/admin")]:
            assert request(opener, path)[0] == 200, path
        assert request(stylist, "/NoShows")[0] == 302
        assert request(anonymous, "/api/appointments/1/no-show", {})[0] == 401
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
        assert request(anonymous, f"/api/appointments/{ids[1]}/no-show", {}, stylist_tokens["accessToken"])[0] == 403
        assert request(anonymous, f"/api/appointments/{ids[1]}/no-show", {}, desk_tokens["accessToken"])[0] == 200
        status, body = request(anonymous, "/api/appointments/customer-warning?phone=0999998888", token=desk_tokens["accessToken"])
        assert json.loads(body)["noShowCount"] == 1
        status, body = request(anonymous, "/api/appointments/occupied-slots?stylistId=1&date=" + now.strftime("%Y-%m-%d"), token=desk_tokens["accessToken"])
        assert ids[1] not in [x["appointmentId"] for x in json.loads(body)]
        # The third fixture overlaps, so undo must fail until that overlap is cancelled.
        status, body = request(anonymous, f"/api/appointments/{ids[1]}/undo-no-show", {}, desk_tokens["accessToken"])
        assert status == 409, (status, body[:500])
        c.execute("UPDATE Appointments SET Status='Cancelled' WHERE AppointmentId IN (?,?)", (ids[0], ids[2])); con.commit()
        assert request(anonymous, f"/api/appointments/{ids[1]}/undo-no-show", {}, desk_tokens["accessToken"])[0] == 200
        status, body = request(anonymous, "/api/appointments/customer-warning?phone=0999998888", token=desk_tokens["accessToken"])
        assert json.loads(body)["noShowCount"] == 0
        print("PASS no-show APIs, slot release, history and undo")
        def csrf(page):
            return html.unescape(re.search(r'name="__RequestVerificationToken"[^>]*value="([^"]+)"', page).group(1))
        # Confirm the real no-show form and its CSRF protection.
        c.execute("UPDATE Appointments SET Status='Confirmed',CheckedInAt=NULL WHERE AppointmentId=?", (ids[1],)); con.commit()
        status, page = request(desk, "/NoShows")
        assert 'name="operation"' in page
        status, body = request(desk, "/NoShows/Change", {"id": ids[1], "operation": "no-show", "date": now.strftime("%Y-%m-%d"), "__RequestVerificationToken": csrf(page)}, form=True)
        assert status == 302
        assert c.execute("SELECT Status FROM Appointments WHERE AppointmentId=?", (ids[1],)).fetchone()[0] == "NoShow"
        assert request(desk, "/NoShows/Change", {"id": ids[1], "operation": "no-show"}, form=True)[0] == 400
        print("PASS Razor no-show form and CSRF protection")
        # Create a new booking on a released slot and enforce its no-show warning server-side.
        future = now + dt.timedelta(minutes=10)
        c.execute("UPDATE Appointments SET Status='NoShow',CheckedInAt=NULL,StartTime='00:00:00',EndTime='23:59:00' WHERE AppointmentId IN (?,?,?)", tuple(ids))
        c.execute("INSERT INTO WorkSchedules (StylistId,WorkDate,StartTime,EndTime,Status,CreatedAt) VALUES (?,?,?,?,?,?)", (1, future.strftime("%Y-%m-%d 00:00:00"), "00:00:00", "23:59:00", "Working", now.isoformat(" ")))
        c.execute("UPDATE Services SET DurationMinutes=1 WHERE ServiceId=1"); con.commit()
        status, page = request(desk, "/NoShows/Create")
        form = {"FullName": "HTTP Test", "Phone": "0999998888", "StylistId": 1, "ServiceId": 1, "StartsAt": future.strftime("%Y-%m-%dT%H:%M"), "__RequestVerificationToken": csrf(page)}
        before = c.execute("SELECT COUNT(*) FROM Appointments").fetchone()[0]
        assert request(desk, "/NoShows/Create", form, form=True)[0] == 200
        assert c.execute("SELECT COUNT(*) FROM Appointments").fetchone()[0] == before
        form["AcknowledgeNoShow"] = "true"
        assert request(desk, "/NoShows/Create", form, form=True)[0] == 302
        assert c.execute("SELECT COUNT(*) FROM Appointments").fetchone()[0] == before + 1
        assert request(desk, "/NoShows/Create", form, form=True)[0] == 200
        assert c.execute("SELECT COUNT(*) FROM Appointments").fetchone()[0] == before + 1
        print("PASS booking warning, rebooking released slot and overlap rejection")
        status, _ = request(desk, "/api/auth/logout", {"refreshToken": desk_tokens["refreshToken"]})
        assert status == 204 and request(desk, "/NoShows")[0] == 302
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
