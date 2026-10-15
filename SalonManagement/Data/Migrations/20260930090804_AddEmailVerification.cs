using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SalonManagement.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailVerification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Các tài khoản đã tồn tại trước khi có luồng xác minh vẫn được tiếp tục sử dụng.
            migrationBuilder.Sql("UPDATE AspNetUsers SET EmailConfirmed = 1");

            migrationBuilder.AddColumn<string>(
                name: "EmailVerificationCodeHash",
                table: "AspNetUsers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EmailVerificationCodeExpiresAtUtc",
                table: "AspNetUsers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EmailVerificationCodeSentAtUtc",
                table: "AspNetUsers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EmailVerificationFailedAttempts",
                table: "AspNetUsers",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "EmailVerificationCodeHash", table: "AspNetUsers");
            migrationBuilder.DropColumn(name: "EmailVerificationCodeExpiresAtUtc", table: "AspNetUsers");
            migrationBuilder.DropColumn(name: "EmailVerificationCodeSentAtUtc", table: "AspNetUsers");
            migrationBuilder.DropColumn(name: "EmailVerificationFailedAttempts", table: "AspNetUsers");
        }
    }
}
