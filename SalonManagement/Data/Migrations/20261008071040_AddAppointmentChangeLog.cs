using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalonManagement.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAppointmentChangeLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RescheduleChangeLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AppointmentId = table.Column<int>(type: "int", nullable: false),
                    ModifiedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    ModifiedByUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    OldStylistId = table.Column<int>(type: "int", nullable: false),
                    NewStylistId = table.Column<int>(type: "int", nullable: false),
                    OldStylistName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    NewStylistName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    OldDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NewDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    OldStartTime = table.Column<TimeSpan>(type: "time", nullable: false),
                    NewStartTime = table.Column<TimeSpan>(type: "time", nullable: false),
                    OldEndTime = table.Column<TimeSpan>(type: "time", nullable: false),
                    NewEndTime = table.Column<TimeSpan>(type: "time", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ChangedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RescheduleChangeLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RescheduleChangeLogs_Appointments_AppointmentId",
                        column: x => x.AppointmentId,
                        principalTable: "Appointments",
                        principalColumn: "AppointmentId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RescheduleChangeLogs_AppointmentId",
                table: "RescheduleChangeLogs",
                column: "AppointmentId");

            migrationBuilder.CreateIndex(
                name: "IX_RescheduleChangeLogs_ChangedAtUtc",
                table: "RescheduleChangeLogs",
                column: "ChangedAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RescheduleChangeLogs");
        }
    }
}
