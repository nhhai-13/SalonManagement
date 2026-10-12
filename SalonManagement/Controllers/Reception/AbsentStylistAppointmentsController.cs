using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalonManagement.Data;
using SalonManagement.Models;

namespace SalonManagement.Controllers.Reception;

[Authorize(Roles = UserRoles.Receptionist)]
[ApiController]
[Route("api/reception/absent-stylist-appointments")]
public sealed class AbsentStylistAppointmentsController(ApplicationDbContext db) : ControllerBase
{
    [HttpGet("stylists")]
    public async Task<ActionResult<IReadOnlyList<AbsentStylistOption>>> GetStylists()
    {
        var stylists = await db.Stylists.AsNoTracking()
            .Where(stylist => stylist.IsActive)
            .OrderBy(stylist => stylist.FullName)
            .ThenBy(stylist => stylist.StylistId)
            .Select(stylist => new AbsentStylistOption(stylist.StylistId, stylist.FullName))
            .ToListAsync();
        return Ok(stylists);
    }

    [HttpGet]
    public async Task<ActionResult<AbsentStylistAppointmentsResponse>> Get([FromQuery] int stylistId, [FromQuery] DateOnly date)
    {
        var stylist = await db.Stylists.AsNoTracking()
            .Where(item => item.StylistId == stylistId)
            .Select(item => new AbsentStylistOption(item.StylistId, item.FullName))
            .SingleOrDefaultAsync();
        if (stylist is null) return NotFound(new { error = "Không tìm thấy thợ được chọn." });

        var day = date.ToDateTime(TimeOnly.MinValue);
        var nextDay = day.AddDays(1);
        var appointments = await db.Appointments.AsNoTracking()
            .Include(appointment => appointment.Customer)
            .Include(appointment => appointment.AppointmentServices)
                .ThenInclude(item => item.Service)
            .Where(appointment => appointment.StylistId == stylistId
                && appointment.AppointmentDate >= day
                && appointment.AppointmentDate < nextDay
                && appointment.Status != "Completed"
                && appointment.Status != "Cancelled"
                && appointment.Status != "Rejected"
                && appointment.Status != "NoShow")
            .OrderBy(appointment => appointment.StartTime)
            .ThenBy(appointment => appointment.EndTime)
            .ThenBy(appointment => appointment.AppointmentId)
            .Select(appointment => new AbsentStylistAppointment(
                appointment.AppointmentId,
                appointment.BookingReference,
                appointment.CustomerNameOverride ?? appointment.Customer.FullName,
                appointment.Customer.Phone,
                appointment.AppointmentServices.Select(item => item.Service.ServiceName).OrderBy(name => name).ToList(),
                appointment.StartTime,
                appointment.EndTime,
                appointment.Status))
            .ToListAsync();

        return Ok(new AbsentStylistAppointmentsResponse(stylist, date, appointments));
    }
}

public sealed class ReceptionAbsenceAppointmentsPageController : Controller
{
    [Authorize(Roles = UserRoles.Receptionist)]
    [HttpGet("reception/absence-appointments")]
    public IActionResult Index() => View();
}

public sealed record AbsentStylistOption(int StylistId, string FullName);

public sealed record AbsentStylistAppointment(
    int AppointmentId,
    string BookingReference,
    string CustomerName,
    string CustomerPhone,
    IReadOnlyList<string> Services,
    TimeSpan StartTime,
    TimeSpan EndTime,
    string Status);

public sealed record AbsentStylistAppointmentsResponse(
    AbsentStylistOption Stylist,
    DateOnly Date,
    IReadOnlyList<AbsentStylistAppointment> Appointments);
