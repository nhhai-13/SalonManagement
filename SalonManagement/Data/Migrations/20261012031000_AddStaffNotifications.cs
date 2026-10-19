using Microsoft.EntityFrameworkCore.Migrations;

namespace SalonManagement.Data.Migrations;

public partial class AddStaffNotifications : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(name: "StaffNotifications", columns: table => new
        {
            StaffNotificationId = table.Column<int>(type: "int", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
            RecipientEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
            AppointmentId = table.Column<int>(type: "int", nullable: false),
            Type = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
            Message = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
            CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
            ReadAt = table.Column<DateTime>(type: "datetime2", nullable: true)
        }, constraints: table => table.PrimaryKey("PK_StaffNotifications", value => value.StaffNotificationId));
        migrationBuilder.CreateIndex(name: "IX_StaffNotifications_RecipientEmail_ReadAt_CreatedAt", table: "StaffNotifications", columns: new[] { "RecipientEmail", "ReadAt", "CreatedAt" });
    }

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable(name: "StaffNotifications");
}
