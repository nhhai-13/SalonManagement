using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalonManagement.Data.Migrations
{
    /// <inheritdoc />
    public partial class AppointmentAttendance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "StylistId",
                table: "AspNetUsers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CheckedInAt",
                table: "Appointments",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LateMinutes",
                table: "Appointments",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "NoShowAt",
                table: "Appointments",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StatusBeforeNoShow",
                table: "Appointments",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "Version",
                table: "Appointments",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "AppointmentAudits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AppointmentId = table.Column<int>(type: "int", nullable: false),
                    ActorId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Action = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PreviousStatus = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NewStatus = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppointmentAudits", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StylistNotifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StylistId = table.Column<int>(type: "int", nullable: false),
                    AppointmentId = table.Column<int>(type: "int", nullable: false),
                    Message = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StylistNotifications", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_StylistId",
                table: "AspNetUsers",
                column: "StylistId");

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentAudits_AppointmentId_OccurredAt",
                table: "AppointmentAudits",
                columns: new[] { "AppointmentId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_StylistNotifications_StylistId_CreatedAt",
                table: "StylistNotifications",
                columns: new[] { "StylistId", "CreatedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_Stylists_StylistId",
                table: "AspNetUsers",
                column: "StylistId",
                principalTable: "Stylists",
                principalColumn: "StylistId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_Stylists_StylistId",
                table: "AspNetUsers");

            migrationBuilder.DropTable(
                name: "AppointmentAudits");

            migrationBuilder.DropTable(
                name: "StylistNotifications");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_StylistId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "StylistId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "CheckedInAt",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "LateMinutes",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "NoShowAt",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "StatusBeforeNoShow",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Appointments");
        }
    }
}
