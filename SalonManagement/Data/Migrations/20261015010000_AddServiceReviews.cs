using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalonManagement.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261015010000_AddServiceReviews")]
public partial class AddServiceReviews : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ServiceReviews",
            columns: table => new
            {
                ServiceReviewId = table.Column<int>(type: "int", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                AppointmentId = table.Column<int>(type: "int", nullable: false),
                TokenHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                EmailSentAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                Rating = table.Column<int>(type: "int", nullable: true),
                Comment = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ServiceReviews", x => x.ServiceReviewId);
                table.ForeignKey("FK_ServiceReviews_Appointments_AppointmentId", x => x.AppointmentId, "Appointments", "AppointmentId", onDelete: ReferentialAction.Cascade);
            });
        migrationBuilder.CreateIndex(name: "IX_ServiceReviews_AppointmentId", table: "ServiceReviews", column: "AppointmentId", unique: true);
        migrationBuilder.CreateIndex(name: "IX_ServiceReviews_TokenHash", table: "ServiceReviews", column: "TokenHash", unique: true);
        migrationBuilder.CreateIndex(name: "IX_ServiceReviews_SubmittedAt_ExpiresAt", table: "ServiceReviews", columns: new[] { "SubmittedAt", "ExpiresAt" });
    }

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable(name: "ServiceReviews");
}
