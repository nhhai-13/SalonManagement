using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalonManagement.Data.Migrations
{
    /// <inheritdoc />
    public partial class Sprint4BillingAndVisitHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppointmentServices_AppointmentId_ServiceId",
                table: "AppointmentServices");

            migrationBuilder.AlterColumn<string>(
                name: "TransactionCode",
                table: "Payments",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ClosedAt",
                table: "Invoices",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClosedByUserId",
                table: "Invoices",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedAt",
                table: "AppointmentServices",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsCompleted",
                table: "AppointmentServices",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "StylistId",
                table: "AppointmentServices",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TechnicalNotes",
                table: "AppointmentServices",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TechnicalNotesUpdatedAt",
                table: "AppointmentServices",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TechnicalNotesUpdatedBy",
                table: "AppointmentServices",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ClosedAt",
                table: "Appointments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClosedByUserId",
                table: "Appointments",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "InvoiceAdjustments",
                columns: table => new
                {
                    InvoiceAdjustmentId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InvoiceId = table.Column<int>(type: "int", nullable: false),
                    StylistId = table.Column<int>(type: "int", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    AmountDelta = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CreatedByName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceAdjustments", x => x.InvoiceAdjustmentId);
                    table.ForeignKey(
                        name: "FK_InvoiceAdjustments_Invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "Invoices",
                        principalColumn: "InvoiceId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InvoiceAdjustments_Stylists_StylistId",
                        column: x => x.StylistId,
                        principalTable: "Stylists",
                        principalColumn: "StylistId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InvoiceLines",
                columns: table => new
                {
                    InvoiceLineId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InvoiceId = table.Column<int>(type: "int", nullable: false),
                    AppointmentServiceId = table.Column<int>(type: "int", nullable: false),
                    ServiceName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceLines", x => x.InvoiceLineId);
                    table.ForeignKey(
                        name: "FK_InvoiceLines_AppointmentServices_AppointmentServiceId",
                        column: x => x.AppointmentServiceId,
                        principalTable: "AppointmentServices",
                        principalColumn: "AppointmentServiceId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InvoiceLines_Invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "Invoices",
                        principalColumn: "InvoiceId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentServices_AppointmentId_ServiceId",
                table: "AppointmentServices",
                columns: new[] { "AppointmentId", "ServiceId" });

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentServices_StylistId",
                table: "AppointmentServices",
                column: "StylistId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceAdjustments_InvoiceId",
                table: "InvoiceAdjustments",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceAdjustments_StylistId",
                table: "InvoiceAdjustments",
                column: "StylistId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceLines_AppointmentServiceId",
                table: "InvoiceLines",
                column: "AppointmentServiceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceLines_InvoiceId",
                table: "InvoiceLines",
                column: "InvoiceId");

            migrationBuilder.AddForeignKey(
                name: "FK_AppointmentServices_Stylists_StylistId",
                table: "AppointmentServices",
                column: "StylistId",
                principalTable: "Stylists",
                principalColumn: "StylistId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppointmentServices_Stylists_StylistId",
                table: "AppointmentServices");

            migrationBuilder.DropTable(
                name: "InvoiceAdjustments");

            migrationBuilder.DropTable(
                name: "InvoiceLines");

            migrationBuilder.DropIndex(
                name: "IX_AppointmentServices_AppointmentId_ServiceId",
                table: "AppointmentServices");

            migrationBuilder.DropIndex(
                name: "IX_AppointmentServices_StylistId",
                table: "AppointmentServices");

            migrationBuilder.DropColumn(
                name: "ClosedAt",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "ClosedByUserId",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "CompletedAt",
                table: "AppointmentServices");

            migrationBuilder.DropColumn(
                name: "IsCompleted",
                table: "AppointmentServices");

            migrationBuilder.DropColumn(
                name: "StylistId",
                table: "AppointmentServices");

            migrationBuilder.DropColumn(
                name: "TechnicalNotes",
                table: "AppointmentServices");

            migrationBuilder.DropColumn(
                name: "TechnicalNotesUpdatedAt",
                table: "AppointmentServices");

            migrationBuilder.DropColumn(
                name: "TechnicalNotesUpdatedBy",
                table: "AppointmentServices");

            migrationBuilder.DropColumn(
                name: "ClosedAt",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "ClosedByUserId",
                table: "Appointments");

            migrationBuilder.AlterColumn<string>(
                name: "TransactionCode",
                table: "Payments",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentServices_AppointmentId_ServiceId",
                table: "AppointmentServices",
                columns: new[] { "AppointmentId", "ServiceId" },
                unique: true);
        }
    }
}
