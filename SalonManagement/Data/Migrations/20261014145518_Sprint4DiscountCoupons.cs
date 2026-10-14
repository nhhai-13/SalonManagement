using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalonManagement.Data.Migrations
{
    /// <inheritdoc />
    public partial class Sprint4DiscountCoupons : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DiscountCodeSnapshot",
                table: "Invoices",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DiscountCouponId",
                table: "Invoices",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DiscountTypeSnapshot",
                table: "Invoices",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountValueSnapshot",
                table: "Invoices",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DiscountCoupons",
                columns: table => new
                {
                    DiscountCouponId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    DiscountType = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Value = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    StartsOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndsOn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiscountCoupons", x => x.DiscountCouponId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_DiscountCouponId",
                table: "Invoices",
                column: "DiscountCouponId");

            migrationBuilder.CreateIndex(
                name: "IX_DiscountCoupons_Code",
                table: "DiscountCoupons",
                column: "Code",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Invoices_DiscountCoupons_DiscountCouponId",
                table: "Invoices",
                column: "DiscountCouponId",
                principalTable: "DiscountCoupons",
                principalColumn: "DiscountCouponId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Invoices_DiscountCoupons_DiscountCouponId",
                table: "Invoices");

            migrationBuilder.DropTable(
                name: "DiscountCoupons");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_DiscountCouponId",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "DiscountCodeSnapshot",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "DiscountCouponId",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "DiscountTypeSnapshot",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "DiscountValueSnapshot",
                table: "Invoices");
        }
    }
}
