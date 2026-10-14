using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalonManagement.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddShopHoliday : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ShopHolidays",
                columns: table => new
                {
                    ShopHolidayId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HolidayDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShopHolidays", x => x.ShopHolidayId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ShopHolidays_HolidayDate",
                table: "ShopHolidays",
                column: "HolidayDate",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ShopHolidays");
        }
    }
}
