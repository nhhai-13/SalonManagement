using Microsoft.EntityFrameworkCore.Migrations;

namespace SalonManagement.Data.Migrations;

public partial class AddAppointmentBookingSource : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.AddColumn<string>(name: "BookingSource", table: "Appointments", type: "nvarchar(20)", maxLength: 20, nullable: true);
    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropColumn(name: "BookingSource", table: "Appointments");
}
