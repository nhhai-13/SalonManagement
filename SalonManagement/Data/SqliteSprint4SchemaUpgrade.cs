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
