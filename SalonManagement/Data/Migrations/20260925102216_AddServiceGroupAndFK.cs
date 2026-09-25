using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalonManagement.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddServiceGroupAndFK : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ServiceGroupId",
                table: "Services",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ServiceGroups",
                columns: table => new
                {
                    ServiceGroupId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GroupName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceGroups", x => x.ServiceGroupId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Services_ServiceGroupId",
                table: "Services",
                column: "ServiceGroupId");

            migrationBuilder.AddForeignKey(
                name: "FK_Services_ServiceGroups_ServiceGroupId",
                table: "Services",
                column: "ServiceGroupId",
                principalTable: "ServiceGroups",
                principalColumn: "ServiceGroupId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Services_ServiceGroups_ServiceGroupId",
                table: "Services");

            migrationBuilder.DropTable(
                name: "ServiceGroups");

            migrationBuilder.DropIndex(
                name: "IX_Services_ServiceGroupId",
                table: "Services");

            migrationBuilder.DropColumn(
                name: "ServiceGroupId",
                table: "Services");
        }
    }
}
