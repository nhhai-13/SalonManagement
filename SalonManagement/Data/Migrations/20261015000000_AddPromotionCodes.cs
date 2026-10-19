using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalonManagement.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261015000000_AddPromotionCodes")]
public partial class AddPromotionCodes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "PromotionCode",
            table: "Invoices",
            type: "nvarchar(64)",
            maxLength: 64,
            nullable: true);

        migrationBuilder.CreateTable(
            name: "PromotionCodes",
            columns: table => new
            {
                PromotionCodeId = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                Code = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                Name = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                DiscountType = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                DiscountValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                StartsAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                EndsAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                IsActive = table.Column<bool>(type: "bit", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_PromotionCodes", x => x.PromotionCodeId));

        migrationBuilder.CreateIndex(name: "IX_PromotionCodes_Code", table: "PromotionCodes", column: "Code", unique: true);
        migrationBuilder.CreateIndex(name: "IX_PromotionCodes_IsActive_StartsAt_EndsAt", table: "PromotionCodes", columns: new[] { "IsActive", "StartsAt", "EndsAt" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "PromotionCodes");
        migrationBuilder.DropColumn(name: "PromotionCode", table: "Invoices");
    }
}
