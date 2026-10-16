using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalonManagement.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261011210000_AddAppointmentCancellationDetails")]
public partial class AddAppointmentCancellationDetails : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTimeOffset>(name: "CancelledAt", table: "Appointments", type: "datetimeoffset", nullable: true);
        migrationBuilder.AddColumn<string>(name: "CancelledBy", table: "Appointments", type: "nvarchar(max)", nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "CancelledAt", table: "Appointments");
        migrationBuilder.DropColumn(name: "CancelledBy", table: "Appointments");
    }
}
