using Microsoft.EntityFrameworkCore.Migrations;
#nullable disable
namespace SalonManagement.Data.Migrations;
public partial class PreserveRescheduleHistory : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        if (ActiveProvider.Contains("SqlServer"))
            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[dbo].[RescheduleChangeLogs]', N'U') IS NULL
                   AND COL_LENGTH(N'[dbo].[AppointmentChangeLogs]', N'ChangedAtUtc') IS NOT NULL
                BEGIN
                    EXEC sp_rename N'[dbo].[AppointmentChangeLogs]', N'RescheduleChangeLogs';
                    IF OBJECT_ID(N'[dbo].[PK_AppointmentChangeLogs]', N'PK') IS NOT NULL
                        EXEC sp_rename N'[dbo].[PK_AppointmentChangeLogs]', N'PK_RescheduleChangeLogs', N'OBJECT';
                    IF OBJECT_ID(N'[dbo].[FK_AppointmentChangeLogs_Appointments_AppointmentId]', N'F') IS NOT NULL
                        EXEC sp_rename N'[dbo].[FK_AppointmentChangeLogs_Appointments_AppointmentId]', N'FK_RescheduleChangeLogs_Appointments_AppointmentId', N'OBJECT';
                END
                """);
    }
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // History is retained: renaming it back could overwrite the other workflow's table.
    }
}