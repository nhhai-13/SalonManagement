using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace SalonManagement.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261010154500_AddAppointmentChangeEmails")]
public partial class AddAppointmentChangeEmails : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "EmailStatus", table: "AppointmentChangeLogs", type: "nvarchar(max)", nullable: false, defaultValue: "NotRequired");
        migrationBuilder.CreateTable(
            name: "AppointmentChangeEmails",
            columns: table => new
            {
                AppointmentChangeEmailId = table.Column<int>(type: "int", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                AppointmentChangeLogId = table.Column<int>(type: "int", nullable: false),
                RecipientEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                StylistName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                AppointmentDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                StartTime = table.Column<TimeSpan>(type: "time", nullable: false),
                Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                AttemptCount = table.Column<int>(type: "int", nullable: false),
                NextAttemptAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                SentAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                LastError = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_AppointmentChangeEmails", x => x.AppointmentChangeEmailId));
        migrationBuilder.CreateIndex(name: "IX_AppointmentChangeEmails_Status_NextAttemptAt", table: "AppointmentChangeEmails", columns: new[] { "Status", "NextAttemptAt" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "AppointmentChangeEmails");
        migrationBuilder.DropColumn(name: "EmailStatus", table: "AppointmentChangeLogs");
    }
}
