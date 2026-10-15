using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SalonManagement.Data;

#nullable disable

namespace SalonManagement.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261011010000_AddScheduleChangeNotificationOutbox")]
public partial class AddScheduleChangeNotificationOutbox : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ScheduleChangeNotifications",
            columns: table => new
            {
                ScheduleChangeNotificationId = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                AppointmentId = table.Column<int>(type: "int", nullable: false),
                Version = table.Column<int>(type: "int", nullable: false),
                RecipientEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                CustomerName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                ServiceNames = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                AppointmentDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                OldStylistName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                NewStylistName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                OldStartTime = table.Column<TimeSpan>(type: "time", nullable: false),
                NewStartTime = table.Column<TimeSpan>(type: "time", nullable: false),
                TransferredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                AttemptCount = table.Column<int>(type: "int", nullable: false),
                LastAttemptAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                NextAttemptAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                SentAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                LastError = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ScheduleChangeNotifications", x => x.ScheduleChangeNotificationId);
                table.ForeignKey(
                    name: "FK_ScheduleChangeNotifications_Appointments_AppointmentId",
                    column: x => x.AppointmentId,
                    principalTable: "Appointments",
                    principalColumn: "AppointmentId",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ScheduleChangeNotifications_AppointmentId_Version",
            table: "ScheduleChangeNotifications",
            columns: new[] { "AppointmentId", "Version" },
            unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_ScheduleChangeNotifications_Status_NextAttemptAt",
            table: "ScheduleChangeNotifications",
            columns: new[] { "Status", "NextAttemptAt" });
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropTable(name: "ScheduleChangeNotifications");
}
