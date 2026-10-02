using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalonManagement.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStylistAvailabilityExceptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StylistBreaks",
                columns: table => new
                {
                    StylistBreakId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StylistId = table.Column<int>(type: "int", nullable: false),
                    BreakDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StartTime = table.Column<TimeSpan>(type: "time", nullable: false),
                    EndTime = table.Column<TimeSpan>(type: "time", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StylistBreaks", x => x.StylistBreakId);
                    table.ForeignKey(
                        name: "FK_StylistBreaks_Stylists_StylistId",
                        column: x => x.StylistId,
                        principalTable: "Stylists",
                        principalColumn: "StylistId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StylistDaysOff",
                columns: table => new
                {
                    StylistDayOffId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StylistId = table.Column<int>(type: "int", nullable: false),
                    OffDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StylistDaysOff", x => x.StylistDayOffId);
                    table.ForeignKey(
                        name: "FK_StylistDaysOff_Stylists_StylistId",
                        column: x => x.StylistId,
                        principalTable: "Stylists",
                        principalColumn: "StylistId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StylistBreaks_StylistId_BreakDate",
                table: "StylistBreaks",
                columns: new[] { "StylistId", "BreakDate" });

            migrationBuilder.CreateIndex(
                name: "IX_StylistDaysOff_StylistId_OffDate",
                table: "StylistDaysOff",
                columns: new[] { "StylistId", "OffDate" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StylistBreaks");

            migrationBuilder.DropTable(
                name: "StylistDaysOff");
        }
    }
}
