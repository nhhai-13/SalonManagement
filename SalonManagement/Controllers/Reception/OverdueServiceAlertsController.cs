using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalonManagement.Data;
using SalonManagement.Models;

namespace SalonManagement.Controllers.Reception;

[Authorize(Roles = UserRoles.Receptionist)]
[ApiController]
[Route("api/reception/overdue-service-alerts")]
public sealed class OverdueServiceAlertsController(ApplicationDbContext db) : ControllerBase
{
    private const int AlertThresholdMinutes = 20;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<OverdueServiceAlertResponse>>> Get()
    {
        // Calculate durations in memory so this works consistently with SQL Server and SQLite.
        var completedAppointments = await db.Appointments.AsNoTracking()
            .Include(appointment => appointment.Customer)
            .Include(appointment => appointment.Stylist)
            .Include(appointment => appointment.AppointmentServices)
                .ThenInclude(item => item.Service)
            .Where(appointment => appointment.Status == "Completed"
                && appointment.ActualStartedAt != null
                && appointment.ActualCompletedAt != null)
            .ToListAsync();

        var alerts = completedAppointments
            .Select(appointment =>
            {
                var standard = appointment.AppointmentServices.Sum(item => item.DurationMinutes);
                var actual = (int)(appointment.ActualCompletedAt!.Value - appointment.ActualStartedAt!.Value).TotalMinutes;
                return new OverdueServiceAlertResponse(
                    appointment.AppointmentId,
                    appointment.BookingReference,
                    appointment.Customer.FullName,
                    appointment.Stylist.FullName,
                    appointment.AppointmentDate,
                    appointment.StartTime,
                    appointment.EndTime,
                    appointment.AppointmentServices.Select(item => item.Service.ServiceName).OrderBy(name => name).ToList(),
                    standard,
                    actual,
                    actual - standard);
            })
            .Where(alert => alert.ExcessDurationMinutes >= AlertThresholdMinutes)
            .OrderByDescending(alert => alert.ExcessDurationMinutes)
            .ThenBy(alert => alert.AppointmentDate)
            .ThenBy(alert => alert.StartTime)
            .ToList();

        return Ok(alerts);
    }
}

public sealed record OverdueServiceAlertResponse(
    int AppointmentId,
    string BookingReference,
    string CustomerName,
    string StylistName,
    DateTime AppointmentDate,
    TimeSpan StartTime,
    TimeSpan EndTime,
    IReadOnlyList<string> Services,
    int StandardDurationMinutes,
    int ActualDurationMinutes,
    int ExcessDurationMinutes);
