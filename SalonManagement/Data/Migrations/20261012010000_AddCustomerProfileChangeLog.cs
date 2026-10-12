using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SalonManagement.Data;

#nullable disable

namespace SalonManagement.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261012010000_AddCustomerProfileChangeLog")]
public partial class AddCustomerProfileChangeLog : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "CustomerProfileChangeLogs",
            columns: table => new
            {
                CustomerProfileChangeLogId = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                CustomerId = table.Column<int>(type: "int", nullable: false),
                ChangedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                ChangedByEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                PreviousFullName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                PreviousEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                PreviousNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                UpdatedFullName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                UpdatedEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                UpdatedNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                ChangedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CustomerProfileChangeLogs", item => item.CustomerProfileChangeLogId);
                table.ForeignKey(
                    name: "FK_CustomerProfileChangeLogs_Customers_CustomerId",
                    column: item => item.CustomerId,
                    principalTable: "Customers",
                    principalColumn: "CustomerId",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_CustomerProfileChangeLogs_CustomerId_ChangedAtUtc",
            table: "CustomerProfileChangeLogs",
            columns: new[] { "CustomerId", "ChangedAtUtc" });
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropTable(name: "CustomerProfileChangeLogs");
}
