using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalonManagement.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddConfirmationEmailFieldsToAppointment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // CHỈ THÊM 3 CỘT MỚI VÀO BẢNG APPOINTMENTS
            migrationBuilder.AddColumn<string>(
                name: "ConfirmationEmailStatus",
                table: "Appointments",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ConfirmationEmailSentAt",
                table: "Appointments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConfirmationEmailError",
                table: "Appointments",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // XÓA 3 CỘT NẾU CẦN ROLLBACK
            migrationBuilder.DropColumn(
                name: "ConfirmationEmailStatus",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "ConfirmationEmailSentAt",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "ConfirmationEmailError",
                table: "Appointments");
        }
    }
}