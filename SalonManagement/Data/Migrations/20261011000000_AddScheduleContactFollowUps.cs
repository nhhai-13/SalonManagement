using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalonManagement.Data.Migrations;

[DbContext(typeof(SalonManagement.Data.ApplicationDbContext))]
[Migration("20261011000000_AddScheduleContactFollowUps")]
public partial class AddScheduleContactFollowUps : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ScheduleContactFollowUps",
            columns: table => new
            {
                ScheduleContactFollowUpId = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                AppointmentId = table.Column<int>(type: "int", nullable: false),
                Category = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                OriginalAppointmentDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                OriginalStartTime = table.Column<TimeSpan>(type: "time", nullable: false),
                ContactStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                ContactNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                ContactedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ScheduleContactFollowUps", x => x.ScheduleContactFollowUpId);
                table.ForeignKey(
                    name: "FK_ScheduleContactFollowUps_Appointments_AppointmentId",
                    column: x => x.AppointmentId,
                    principalTable: "Appointments",
                    principalColumn: "AppointmentId",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ScheduleContactFollowUps_AppointmentId",
            table: "ScheduleContactFollowUps",
            column: "AppointmentId",
            unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_ScheduleContactFollowUps_OriginalAppointmentDate_ContactStatus_Category",
            table: "ScheduleContactFollowUps",
            columns: new[] { "OriginalAppointmentDate", "ContactStatus", "Category" });
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropTable(name: "ScheduleContactFollowUps");
}
