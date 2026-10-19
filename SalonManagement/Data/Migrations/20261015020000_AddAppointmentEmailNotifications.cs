using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalonManagement.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261015020000_AddAppointmentEmailNotifications")]
public partial class AddAppointmentEmailNotifications : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "AppointmentEmailNotifications",
            columns: table => new
            {
                AppointmentEmailNotificationId = table.Column<int>(type: "int", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                AppointmentId = table.Column<int>(type: "int", nullable: false),
                NotificationType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                RecipientEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                AttemptCount = table.Column<int>(type: "int", nullable: false),
                ScheduledAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                NextAttemptAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                SentAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                CancelledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                LastError = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AppointmentEmailNotifications", x => x.AppointmentEmailNotificationId);
                table.ForeignKey("FK_AppointmentEmailNotifications_Appointments_AppointmentId", x => x.AppointmentId, "Appointments", "AppointmentId", onDelete: ReferentialAction.Cascade);
            });
        migrationBuilder.CreateIndex(name: "IX_AppointmentEmailNotifications_Status_NextAttemptAt", table: "AppointmentEmailNotifications", columns: new[] { "Status", "NextAttemptAt" });
        migrationBuilder.CreateIndex(name: "IX_AppointmentEmailNotifications_AppointmentId_NotificationType_Status", table: "AppointmentEmailNotifications", columns: new[] { "AppointmentId", "NotificationType", "Status" });
    }

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable(name: "AppointmentEmailNotifications");
}
