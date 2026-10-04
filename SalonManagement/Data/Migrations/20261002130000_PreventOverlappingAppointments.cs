using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace SalonManagement.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261002130000_PreventOverlappingAppointments")]
public partial class PreventOverlappingAppointments : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        if (ActiveProvider == "Microsoft.EntityFrameworkCore.SqlServer")
        {
            migrationBuilder.Sql("""
                CREATE TRIGGER TR_Appointments_PreventOverlap ON Appointments
                AFTER INSERT, UPDATE AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (
                        SELECT 1 FROM inserted i
                        JOIN Appointments a ON a.StylistId = i.StylistId
                          AND a.AppointmentDate = i.AppointmentDate
                          AND a.AppointmentId <> i.AppointmentId
                          AND a.Status IN ('Pending', 'Confirmed', 'InProgress')
                          AND i.Status IN ('Pending', 'Confirmed', 'InProgress')
                          AND a.StartTime < i.EndTime AND i.StartTime < a.EndTime)
                    BEGIN
                        ROLLBACK TRANSACTION;
                        THROW 51000, 'Appointment overlaps an existing booking for this stylist.', 1;
                    END
                END
                """);
        }
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        if (ActiveProvider == "Microsoft.EntityFrameworkCore.SqlServer")
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS TR_Appointments_PreventOverlap;");
    }
}
