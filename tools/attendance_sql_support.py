"""Temporary SQL Server database helpers for attendance HTTP regression tests."""
import os
import re
import subprocess
import uuid

SERVER = os.environ.get("ATTENDANCE_TEST_SQL_SERVER", r"(localdb)\MSSQLLocalDB")

def sql(database, statement):
    result = subprocess.run(["sqlcmd", "-S", SERVER, "-E", "-d", database, "-b", "-h", "-1", "-W", "-s", "\t", "-Q", "SET NOCOUNT ON; " + statement],
                            check=True, stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True,
                            creationflags=subprocess.CREATE_NO_WINDOW if os.name == "nt" else 0)
    return [line.strip().split("\t") for line in result.stdout.splitlines() if line.strip()]

def new_database():
    return "AttendanceHttpTest_" + uuid.uuid4().hex

def drop_database(name):
    if not re.fullmatch(r"AttendanceHttpTest_[0-9a-f]{32}", name):
        raise ValueError("Refusing to clean up a non-test database")
    sql("master", f"IF DB_ID(N'{name}') IS NOT NULL BEGIN ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}]; END")

class Connection:
    def __init__(self, database):
        self.database = database
    def cursor(self):
        return Cursor(self.database)
    def commit(self):
        pass  # Each fixture command is autocommitted by SQLCMD.
    def close(self):
        pass

class Cursor:
    def __init__(self, database):
        self.database = database
        self.rows = []
        self.lastrowid = None
    def execute(self, statement, parameters=()):
        pieces = statement.split("?")
        if len(pieces) != len(parameters) + 1:
            raise ValueError("Parameter count mismatch")
        def literal(value):
            if value is None: return "NULL"
            if isinstance(value, (int, float)): return str(value)
            return "N'" + str(value).replace("'", "''") + "'"
        query = pieces[0]
        for value, piece in zip(parameters, pieces[1:]):
            query += literal(value) + piece
        inserting = query.lstrip().upper().startswith("INSERT ")
        if inserting: query += "; SELECT CAST(SCOPE_IDENTITY() AS int)"
        self.rows = sql(self.database, query)
        if inserting and self.rows: self.lastrowid = int(self.rows[0][0])
        return self
    def fetchone(self):
        if not self.rows: return None
        row = self.rows.pop(0)
        return tuple(int(v) if re.fullmatch(r"-?[0-9]+", v) else v for v in row)
