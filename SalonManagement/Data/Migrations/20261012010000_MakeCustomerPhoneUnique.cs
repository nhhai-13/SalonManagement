using Microsoft.EntityFrameworkCore.Migrations;

namespace SalonManagement.Data.Migrations;

public partial class MakeCustomerPhoneUnique : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_Customers_Phone", table: "Customers");
        migrationBuilder.CreateIndex(name: "IX_Customers_Phone", table: "Customers", column: "Phone", unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_Customers_Phone", table: "Customers");
        migrationBuilder.CreateIndex(name: "IX_Customers_Phone", table: "Customers", column: "Phone");
    }
}
