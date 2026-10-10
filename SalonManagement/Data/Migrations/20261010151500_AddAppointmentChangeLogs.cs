using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace SalonManagement.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261010151500_AddAppointmentChangeLogs")]
public partial class AddAppointmentChangeLogs : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "AppointmentChangeLogs",
            columns: table => new
            {
                AppointmentChangeLogId = table.Column<int>(type: "int", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                AppointmentId = table.Column<int>(type: "int", nullable: false),
                ActorId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                ActorName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                ChangedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                OldStylistId = table.Column<int>(type: "int", nullable: true),
                NewStylistId = table.Column<int>(type: "int", nullable: true),
                OldStartTime = table.Column<TimeSpan>(type: "time", nullable: true),
                NewStartTime = table.Column<TimeSpan>(type: "time", nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_AppointmentChangeLogs", x => x.AppointmentChangeLogId));
        migrationBuilder.CreateIndex(name: "IX_AppointmentChangeLogs_AppointmentId_ChangedAt", table: "AppointmentChangeLogs", columns: new[] { "AppointmentId", "ChangedAt" });
    }

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable(name: "AppointmentChangeLogs");
}
