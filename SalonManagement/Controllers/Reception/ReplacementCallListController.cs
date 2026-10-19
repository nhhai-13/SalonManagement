using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalonManagement.Data;
using SalonManagement.Models;
using SalonManagement.Services;

namespace SalonManagement.Controllers.Reception;

[Authorize(Roles = UserRoles.Receptionist)]
[ApiController]
[Route("api/reception/replacement-call-list")]
public sealed class ReplacementCallListController(ApplicationDbContext db, AppointmentReplacementSuggestionService suggestions) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ReplacementCallListItem>>> Get([FromQuery] int absentStylistId, [FromQuery] DateOnly date)
    {
        var day = date.ToDateTime(TimeOnly.MinValue);
        var nextDay = day.AddDays(1);
        var appointments = await db.Appointments.AsNoTracking()
            .Include(appointment => appointment.Customer)
            .Include(appointment => appointment.AppointmentServices)
                .ThenInclude(item => item.Service)
            .Where(appointment => appointment.StylistId == absentStylistId
                && appointment.AppointmentDate >= day && appointment.AppointmentDate < nextDay
                && appointment.Status != "Completed" && appointment.Status != "Cancelled"
                && appointment.Status != "Rejected" && appointment.Status != "NoShow")
            .OrderBy(appointment => appointment.StartTime)
            .ToListAsync();

        var calls = new List<ReplacementCallListItem>();
        foreach (var appointment in appointments)
        {
            var suggestion = await suggestions.SuggestAsync(appointment.AppointmentId, absentStylistId);
            if (suggestion.StylistId is not null) continue;
            calls.Add(new ReplacementCallListItem(
                appointment.AppointmentId,
                appointment.BookingReference,
                appointment.Customer.FullName,
                appointment.Customer.Phone,
                appointment.AppointmentServices.Select(item => item.Service.ServiceName).OrderBy(name => name).ToList(),
                appointment.StartTime,
                appointment.EndTime,
                suggestion.Message));
        }
        return Ok(calls);
    }
}

public sealed record ReplacementCallListItem(
    int AppointmentId,
    string BookingReference,
    string CustomerName,
    string CustomerPhone,
    IReadOnlyList<string> Services,
    TimeSpan StartTime,
    TimeSpan EndTime,
    string Reason);
