using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalonManagement.Data.Migrations
{
    /// <inheritdoc />
    public partial class Sprint4RehearsalLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RehearsalRuns",
                columns: table => new
                {
                    RehearsalRunId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PerformedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    PerformedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RehearsalRuns", x => x.RehearsalRunId);
                });

            migrationBuilder.CreateTable(
                name: "RehearsalIssues",
                columns: table => new
                {
                    RehearsalIssueId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RehearsalRunId = table.Column<int>(type: "int", nullable: false),
                    Step = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Account = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Observation = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Severity = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    Assignee = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    LastChangedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    LastChangedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RehearsalIssues", x => x.RehearsalIssueId);
                    table.ForeignKey(
                        name: "FK_RehearsalIssues_RehearsalRuns_RehearsalRunId",
                        column: x => x.RehearsalRunId,
                        principalTable: "RehearsalRuns",
                        principalColumn: "RehearsalRunId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RehearsalIssues_RehearsalRunId",
                table: "RehearsalIssues",
                column: "RehearsalRunId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RehearsalIssues");

            migrationBuilder.DropTable(
                name: "RehearsalRuns");
        }
    }
}
