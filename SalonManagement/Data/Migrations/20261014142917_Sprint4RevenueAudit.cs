using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalonManagement.Data.Migrations
{
    /// <inheritdoc />
    public partial class Sprint4RevenueAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ClosedByName",
                table: "Invoices",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "InvoiceLineId",
                table: "InvoiceAdjustments",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "CollectedAmount",
                table: "AppointmentServices",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "ClosedByName",
                table: "Appointments",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceAdjustments_InvoiceLineId",
                table: "InvoiceAdjustments",
                column: "InvoiceLineId");

            migrationBuilder.AddForeignKey(
                name: "FK_InvoiceAdjustments_InvoiceLines_InvoiceLineId",
                table: "InvoiceAdjustments",
                column: "InvoiceLineId",
                principalTable: "InvoiceLines",
                principalColumn: "InvoiceLineId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InvoiceAdjustments_InvoiceLines_InvoiceLineId",
                table: "InvoiceAdjustments");

            migrationBuilder.DropIndex(
                name: "IX_InvoiceAdjustments_InvoiceLineId",
                table: "InvoiceAdjustments");

            migrationBuilder.DropColumn(
                name: "ClosedByName",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "InvoiceLineId",
                table: "InvoiceAdjustments");

            migrationBuilder.DropColumn(
                name: "CollectedAmount",
                table: "AppointmentServices");

            migrationBuilder.DropColumn(
                name: "ClosedByName",
                table: "Appointments");
        }
    }
}
