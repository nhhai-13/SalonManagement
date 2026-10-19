using Microsoft.EntityFrameworkCore.Migrations;

namespace SalonManagement.Data.Migrations;

public partial class AddAppointmentReceptionLifecycle : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>(name: "ArrivedAt", table: "Appointments", type: "datetime2", nullable: true);
        migrationBuilder.AddColumn<bool>(name: "IsLate", table: "Appointments", type: "bit", nullable: false, defaultValue: false);
        migrationBuilder.AddColumn<string>(name: "NoShowByUserId", table: "Appointments", type: "nvarchar(max)", nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "ArrivedAt", table: "Appointments");
        migrationBuilder.DropColumn(name: "IsLate", table: "Appointments");
        migrationBuilder.DropColumn(name: "NoShowByUserId", table: "Appointments");
    }
}
