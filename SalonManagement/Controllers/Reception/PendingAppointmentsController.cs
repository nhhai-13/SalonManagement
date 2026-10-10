using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalonManagement.Data;
using SalonManagement.Models;

namespace SalonManagement.Controllers.Reception;

[Authorize(Roles = UserRoles.Receptionist)]
[ApiController]
[Route("api/reception/pending-appointments")]
public sealed class PendingAppointmentsController(ApplicationDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PendingAppointmentResponse>>> Get()
    {
        var appointments = await db.Appointments.AsNoTracking()
            .Where(appointment => appointment.Status == "PendingConfirmation")
            .OrderBy(appointment => appointment.AppointmentDate)
            .ThenBy(appointment => appointment.StartTime)
            .ThenBy(appointment => appointment.CreatedAt)
            .Select(appointment => new PendingAppointmentResponse(
                appointment.AppointmentId,
                appointment.BookingReference,
                appointment.Customer.FullName,
                appointment.Customer.Phone,
                appointment.Customer.Email,
                appointment.Stylist.FullName,
                appointment.AppointmentServices
                    .OrderBy(item => item.Service.ServiceName)
                    .Select(item => item.Service.ServiceName)
                    .ToList(),
                appointment.AppointmentDate,
                appointment.StartTime,
                appointment.EndTime,
                appointment.Status,
                appointment.CreatedAt))
            .ToListAsync();

        return Ok(appointments);
    }
}

public sealed record PendingAppointmentResponse(
    int AppointmentId,
    string Reference,
    string CustomerName,
    string CustomerPhone,
    string? CustomerEmail,
    string StylistName,
    IReadOnlyList<string> Services,
    DateTime AppointmentDate,
    TimeSpan StartTime,
    TimeSpan EndTime,
    string Status,
    DateTime CreatedAt);
