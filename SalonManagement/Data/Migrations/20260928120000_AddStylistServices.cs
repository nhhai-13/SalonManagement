using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SalonManagement.Data;

#nullable disable

namespace SalonManagement.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260928120000_AddStylistServices")]
public partial class AddStylistServices : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "StylistServices",
            columns: table => new
            {
                StylistId = table.Column<int>(type: "int", nullable: false),
                ServiceId = table.Column<int>(type: "int", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_StylistServices", x => new { x.StylistId, x.ServiceId });
                table.ForeignKey("FK_StylistServices_Services_ServiceId", x => x.ServiceId, "Services", "ServiceId", onDelete: ReferentialAction.Cascade);
                table.ForeignKey("FK_StylistServices_Stylists_StylistId", x => x.StylistId, "Stylists", "StylistId", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(name: "IX_StylistServices_ServiceId", table: "StylistServices", column: "ServiceId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "StylistServices");
    }
}
