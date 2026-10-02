using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalonManagement.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBookingReference : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BookingReference",
                table: "Appointments",
                type: "nvarchar(8)",
                maxLength: 8,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_BookingReference",
                table: "Appointments",
                column: "BookingReference",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Appointments_BookingReference",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "BookingReference",
                table: "Appointments");
        }
    }
}
