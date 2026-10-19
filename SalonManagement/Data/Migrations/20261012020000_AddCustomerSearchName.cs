using Microsoft.EntityFrameworkCore.Migrations;

namespace SalonManagement.Data.Migrations;

public partial class AddCustomerSearchName : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "NormalizedSearchName", table: "Customers", type: "nvarchar(150)", maxLength: 150, nullable: false, defaultValue: "");
        migrationBuilder.CreateIndex(name: "IX_Customers_NormalizedSearchName", table: "Customers", column: "NormalizedSearchName");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_Customers_NormalizedSearchName", table: "Customers");
        migrationBuilder.DropColumn(name: "NormalizedSearchName", table: "Customers");
    }
}
