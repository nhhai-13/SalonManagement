using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalonManagement.Data.Migrations;

public partial class Sprint3Workflows : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "ServiceStartedAt",
            table: "Appointments",
            type: "datetimeoffset",
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "ServiceCompletedAt",
            table: "Appointments",
            type: "datetimeoffset",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Source",
            table: "Appointments",
            type: "nvarchar(max)",
            nullable: false,
            defaultValue: "Online");

        migrationBuilder.CreateIndex(
            name: "IX_Customers_FullName",
            table: "Customers",
            column: "FullName");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_Customers_FullName", table: "Customers");
        migrationBuilder.DropColumn(name: "ServiceStartedAt", table: "Appointments");
        migrationBuilder.DropColumn(name: "ServiceCompletedAt", table: "Appointments");
        migrationBuilder.DropColumn(name: "Source", table: "Appointments");
    }
}
