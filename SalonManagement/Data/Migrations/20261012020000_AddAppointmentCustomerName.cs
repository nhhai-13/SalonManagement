using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SalonManagement.Data;

#nullable disable

namespace SalonManagement.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261012020000_AddAppointmentCustomerName")]
public partial class AddAppointmentCustomerName : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "CustomerName",
            table: "Appointments",
            type: "nvarchar(150)",
            maxLength: 150,
            nullable: true);
        migrationBuilder.Sql("UPDATE Appointments SET CustomerName = (SELECT FullName FROM Customers WHERE Customers.CustomerId = Appointments.CustomerId) WHERE CustomerName IS NULL;");
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropColumn(name: "CustomerName", table: "Appointments");
}
