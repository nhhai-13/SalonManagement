using Microsoft.EntityFrameworkCore.Migrations;

namespace SalonManagement.Data.Migrations;

public partial class AddAppointmentNameConflictHandling : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "CustomerNameOverride", table: "Appointments", type: "nvarchar(150)", maxLength: 150, nullable: true);
        migrationBuilder.AddColumn<string>(name: "NameConflictResolution", table: "Appointments", type: "nvarchar(30)", maxLength: 30, nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "CustomerNameOverride", table: "Appointments");
        migrationBuilder.DropColumn(name: "NameConflictResolution", table: "Appointments");
    }
}
