using System.Data;
using Microsoft.EntityFrameworkCore;

namespace SalonManagement.Data;

public static class SqliteSprint4SchemaUpgrade
{
    public static async Task EnsureAsync(ApplicationDbContext db)
    {
        if (!db.Database.IsSqlite()) throw new InvalidOperationException("Sprint 4 SQLite schema upgrade requires SQLite.");
        var connection = db.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere) await connection.OpenAsync();
        try
        {
            await EnsureColumn(db, "Invoices", "DiscountCodeSnapshot", "TEXT NULL");
            await EnsureColumn(db, "Invoices", "DiscountCouponId", "INTEGER NULL");
            await EnsureColumn(db, "Invoices", "DiscountTypeSnapshot", "TEXT NULL");
            await EnsureColumn(db, "Invoices", "DiscountValueSnapshot", "NUMERIC NULL");
            await db.Database.ExecuteSqlRawAsync("""
                CREATE TABLE IF NOT EXISTS DiscountCoupons (
                    DiscountCouponId INTEGER NOT NULL CONSTRAINT PK_DiscountCoupons PRIMARY KEY AUTOINCREMENT,
                    Code TEXT NOT NULL, DiscountType TEXT NOT NULL, Value NUMERIC NOT NULL, StartsOn TEXT NOT NULL, EndsOn TEXT NOT NULL);
                CREATE UNIQUE INDEX IF NOT EXISTS IX_DiscountCoupons_Code ON DiscountCoupons(Code);
                CREATE INDEX IF NOT EXISTS IX_Invoices_DiscountCouponId ON Invoices(DiscountCouponId);
                CREATE TABLE IF NOT EXISTS RehearsalRuns (
                    RehearsalRunId INTEGER NOT NULL CONSTRAINT PK_RehearsalRuns PRIMARY KEY AUTOINCREMENT,
                    PerformedBy TEXT NOT NULL, PerformedAt TEXT NOT NULL, Notes TEXT NULL);
                CREATE TABLE IF NOT EXISTS RehearsalIssues (
                    RehearsalIssueId INTEGER NOT NULL CONSTRAINT PK_RehearsalIssues PRIMARY KEY AUTOINCREMENT,
                    RehearsalRunId INTEGER NOT NULL, Step TEXT NOT NULL, Account TEXT NOT NULL, Observation TEXT NOT NULL,
                    Severity TEXT NOT NULL, Status TEXT NOT NULL, Assignee TEXT NULL, LastChangedBy TEXT NULL, LastChangedAt TEXT NULL,
                    CONSTRAINT FK_RehearsalIssues_RehearsalRuns_RehearsalRunId FOREIGN KEY (RehearsalRunId) REFERENCES RehearsalRuns(RehearsalRunId) ON DELETE CASCADE);
                CREATE TABLE IF NOT EXISTS StagingEmailMessages (
                    StagingEmailMessageId INTEGER NOT NULL CONSTRAINT PK_StagingEmailMessages PRIMARY KEY AUTOINCREMENT,
                    Template TEXT NOT NULL, Recipient TEXT NOT NULL, Summary TEXT NOT NULL, CreatedAt TEXT NOT NULL);
                CREATE INDEX IF NOT EXISTS IX_StagingEmailMessages_CreatedAt ON StagingEmailMessages(CreatedAt);
                CREATE TABLE IF NOT EXISTS StylistReviews (
                    StylistReviewId INTEGER NOT NULL CONSTRAINT PK_StylistReviews PRIMARY KEY AUTOINCREMENT,
                    AppointmentId INTEGER NOT NULL, CustomerId INTEGER NOT NULL, StylistId INTEGER NOT NULL,
                    ReviewTokenHash TEXT NOT NULL, ExpiresAt TEXT NOT NULL, RequestedAt TEXT NOT NULL, SubmittedAt TEXT NULL,
                    Rating INTEGER NULL, Comment TEXT NULL,
                    CONSTRAINT FK_StylistReviews_Appointments_AppointmentId FOREIGN KEY (AppointmentId) REFERENCES Appointments(AppointmentId) ON DELETE CASCADE,
                    CONSTRAINT FK_StylistReviews_Customers_CustomerId FOREIGN KEY (CustomerId) REFERENCES Customers(CustomerId) ON DELETE RESTRICT,
                    CONSTRAINT FK_StylistReviews_Stylists_StylistId FOREIGN KEY (StylistId) REFERENCES Stylists(StylistId) ON DELETE RESTRICT);
                CREATE UNIQUE INDEX IF NOT EXISTS IX_StylistReviews_AppointmentId ON StylistReviews(AppointmentId);
                CREATE UNIQUE INDEX IF NOT EXISTS IX_StylistReviews_ReviewTokenHash ON StylistReviews(ReviewTokenHash);
                """);
        }
        finally
        {
            if (openedHere) await connection.CloseAsync();
        }
    }

    private static async Task EnsureColumn(ApplicationDbContext db, string table, string column, string type)
    {
        var connection = db.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info(\"{table}\")";
        await using var reader = await command.ExecuteReaderAsync();
        var found = false;
        while (await reader.ReadAsync())
        {
            if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase)) found = true;
        }
        await reader.CloseAsync();
        if (!found)
        {
            // These identifiers and types come only from the fixed calls above, not user input.
            await using var alter = connection.CreateCommand();
            alter.CommandText = $"ALTER TABLE \"{table}\" ADD COLUMN \"{column}\" {type}";
            await alter.ExecuteNonQueryAsync();
        }
    }
}
