using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SalonManagement.Data;

#nullable disable

namespace SalonManagement.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261011020000_AddCustomerProfiles")]
public partial class AddCustomerProfiles : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "NormalizedPhone",
            table: "Customers",
            type: "nvarchar(20)",
            maxLength: 20,
            nullable: false,
            defaultValue: "");
        migrationBuilder.CreateIndex(
            name: "IX_Customers_NormalizedPhone",
            table: "Customers",
            column: "NormalizedPhone");
        if (ActiveProvider.Contains("SqlServer", StringComparison.Ordinal))
        {
            migrationBuilder.Sql("""
                UPDATE Customers
                SET NormalizedPhone = CASE
                    WHEN LEFT(digits.PhoneDigits, 2) = '84' AND LEN(digits.PhoneDigits) >= 11
                        THEN '0' + SUBSTRING(digits.PhoneDigits, 3, 20)
                    ELSE digits.PhoneDigits
                END
                FROM Customers
                CROSS APPLY (SELECT REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(Phone, '+', ''), ' ', ''), '-', ''), '(', ''), ')', ''), '.', ''), '/', ''), CHAR(9), '') AS PhoneDigits) digits
                """);
        }
        else
        {
            migrationBuilder.Sql("""
                UPDATE Customers SET NormalizedPhone =
                    CASE WHEN substr(PhoneDigits, 1, 2) = '84' AND length(PhoneDigits) >= 11
                        THEN '0' || substr(PhoneDigits, 3)
                        ELSE PhoneDigits END
                FROM (
                    SELECT CustomerId,
                        replace(replace(replace(replace(replace(replace(replace(replace(Phone, '+', ''), ' ', ''), '-', ''), '(', ''), ')', ''), '.', ''), '/', ''), char(9), '') AS PhoneDigits
                    FROM Customers
                ) AS digits
                WHERE Customers.CustomerId = digits.CustomerId
                """);
        }
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_Customers_NormalizedPhone", table: "Customers");
        migrationBuilder.DropColumn(name: "NormalizedPhone", table: "Customers");
    }
}
