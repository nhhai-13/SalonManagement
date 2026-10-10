using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using SalonManagement.Data;
using SalonManagement.Models;
using SalonManagement.Services;

namespace SalonManagement.Controllers.Reception;

[Authorize(Roles = UserRoles.Receptionist)]
[ApiController]
[Route("api/reception/pending-appointments")]
public sealed class PendingAppointmentsController(ApplicationDbContext db, TimeProvider timeProvider) : ControllerBase
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
                appointment.CreatedAt,
                appointment.ConfirmedByUserId,
                appointment.ConfirmedAt))
            .ToListAsync();

        return Ok(appointments);
    }

    [HttpPost("{appointmentId:int}/confirm")]
    public async Task<IActionResult> Confirm(int appointmentId)
    {
        var appointment = await db.Appointments.SingleOrDefaultAsync(item => item.AppointmentId == appointmentId);
        if (appointment is null) return NotFound(new { error = "Không tìm thấy lịch hẹn." });
        if (appointment.Status != "PendingConfirmation") return Conflict(new { error = "Lịch hẹn này đã được xử lý." });

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();

        appointment.Status = "Confirmed";
        appointment.ConfirmedByUserId = userId;
        appointment.ConfirmedAt = SalonClock.GetLocalNow(timeProvider);
        appointment.UpdatedAt = appointment.ConfirmedAt;
        await db.SaveChangesAsync();
        return Ok(new { appointment.AppointmentId, appointment.Status, appointment.ConfirmedByUserId, appointment.ConfirmedAt });
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
    DateTime CreatedAt,
    string? ConfirmedByUserId = null,
    DateTime? ConfirmedAt = null);
