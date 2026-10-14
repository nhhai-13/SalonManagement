using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace SalonManagement.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261015011000_Sprint4AppointmentEmailDeliveries")]
public sealed class Sprint4AppointmentEmailDeliveries : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "AppointmentEmailDeliveries",
            columns: table => new
            {
                AppointmentEmailDeliveryId = table.Column<int>(type: "int", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                AppointmentId = table.Column<int>(type: "int", nullable: false),
                Kind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                RecipientEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                AppointmentStartAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                NextAttemptAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                AttemptCount = table.Column<int>(type: "int", nullable: false),
                SentAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                LastError = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AppointmentEmailDeliveries", x => x.AppointmentEmailDeliveryId);
                table.ForeignKey("FK_AppointmentEmailDeliveries_Appointments_AppointmentId", x => x.AppointmentId,
                    "Appointments", "AppointmentId", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex("IX_AppointmentEmailDeliveries_AppointmentId_Kind", "AppointmentEmailDeliveries", new[] { "AppointmentId", "Kind" }, unique: true);
        migrationBuilder.CreateIndex("IX_AppointmentEmailDeliveries_Status_NextAttemptAt", "AppointmentEmailDeliveries", new[] { "Status", "NextAttemptAt" });
    }

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable(name: "AppointmentEmailDeliveries");
}
