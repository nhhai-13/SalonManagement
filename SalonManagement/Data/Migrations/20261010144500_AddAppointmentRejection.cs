using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace SalonManagement.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261010144500_AddAppointmentRejection")]
public partial class AddAppointmentRejection : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>(name: "RejectedAt", table: "Appointments", type: "datetime2", nullable: true);
        migrationBuilder.AddColumn<string>(name: "RejectedByUserId", table: "Appointments", type: "nvarchar(max)", nullable: true);
        migrationBuilder.AddColumn<string>(name: "RejectionReason", table: "Appointments", type: "nvarchar(max)", nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "RejectedAt", table: "Appointments");
        migrationBuilder.DropColumn(name: "RejectedByUserId", table: "Appointments");
        migrationBuilder.DropColumn(name: "RejectionReason", table: "Appointments");
    }
}
