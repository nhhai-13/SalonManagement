using System.Data;
using Microsoft.EntityFrameworkCore;

namespace SalonManagement.Data;

public static class SqliteSprint4SchemaUpgrade
{
    public static async Task EnsureAsync(ApplicationDbContext db)
    {
        if (!db.Database.IsSqlite())
            throw new InvalidOperationException("Sprint 4 SQLite schema upgrade requires SQLite.");

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
                CREATE TABLE IF NOT EXISTS StylistReviews (
                    StylistReviewId INTEGER NOT NULL CONSTRAINT PK_StylistReviews PRIMARY KEY AUTOINCREMENT,
                    AppointmentId INTEGER NOT NULL, CustomerId INTEGER NOT NULL, StylistId INTEGER NOT NULL,
                    ReviewTokenHash TEXT NOT NULL, ExpiresAt TEXT NOT NULL, RequestedAt TEXT NOT NULL,
                    SubmittedAt TEXT NULL, Rating INTEGER NULL, Comment TEXT NULL,
                    CONSTRAINT FK_StylistReviews_Appointments_AppointmentId FOREIGN KEY (AppointmentId) REFERENCES Appointments(AppointmentId) ON DELETE CASCADE,
                    CONSTRAINT FK_StylistReviews_Customers_CustomerId FOREIGN KEY (CustomerId) REFERENCES Customers(CustomerId) ON DELETE RESTRICT,
                    CONSTRAINT FK_StylistReviews_Stylists_StylistId FOREIGN KEY (StylistId) REFERENCES Stylists(StylistId) ON DELETE RESTRICT);
                CREATE UNIQUE INDEX IF NOT EXISTS IX_StylistReviews_AppointmentId ON StylistReviews(AppointmentId);
                CREATE UNIQUE INDEX IF NOT EXISTS IX_StylistReviews_ReviewTokenHash ON StylistReviews(ReviewTokenHash);
                CREATE INDEX IF NOT EXISTS IX_StylistReviews_CustomerId ON StylistReviews(CustomerId);
                CREATE INDEX IF NOT EXISTS IX_StylistReviews_StylistId ON StylistReviews(StylistId);
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
        if (found) return;

        // The identifiers and types are fixed at the calls above.
        await using var alter = connection.CreateCommand();
        alter.CommandText = $"ALTER TABLE \"{table}\" ADD COLUMN \"{column}\" {type}";
        await alter.ExecuteNonQueryAsync();
    }
}
