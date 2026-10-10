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
    private static readonly string[] RejectionReasons = ["Không còn thợ phù hợp", "Khung giờ không còn trống", "Không liên hệ được khách", "Yêu cầu của khách không phù hợp"];
    private static readonly TimeSpan PendingOverdueAfter = TimeSpan.FromHours(12);

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PendingAppointmentResponse>>> Get()
    {
        var now = SalonClock.GetLocalNow(timeProvider);
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
                appointment.ConfirmedAt,
                appointment.CreatedAt <= now.Subtract(PendingOverdueAfter)))
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

    [HttpGet("rejection-reasons")]
    public ActionResult<IReadOnlyList<string>> GetRejectionReasons() => Ok(RejectionReasons);

    [HttpPost("{appointmentId:int}/reject")]
    public async Task<IActionResult> Reject(int appointmentId, [FromBody] RejectPendingAppointmentRequest request)
    {
        var reason = request.Reason?.Trim();
        if (string.IsNullOrWhiteSpace(reason) || !RejectionReasons.Contains(reason, StringComparer.Ordinal))
            return BadRequest(new { error = "Vui lòng chọn một lý do từ chối hợp lệ." });

        var appointment = await db.Appointments.SingleOrDefaultAsync(item => item.AppointmentId == appointmentId);
        if (appointment is null) return NotFound(new { error = "Không tìm thấy lịch hẹn." });
        if (appointment.Status != "PendingConfirmation") return Conflict(new { error = "Lịch hẹn này đã được xử lý." });

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();

        appointment.Status = "Rejected";
        appointment.RejectionReason = reason;
        appointment.RejectedByUserId = userId;
        appointment.RejectedAt = SalonClock.GetLocalNow(timeProvider);
        appointment.UpdatedAt = appointment.RejectedAt;
        await db.SaveChangesAsync();
        return Ok(new { appointment.AppointmentId, appointment.Status, appointment.RejectionReason, appointment.RejectedByUserId, appointment.RejectedAt });
    }
}

public sealed record RejectPendingAppointmentRequest(string? Reason);

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
    DateTime? ConfirmedAt = null,
    bool IsOverdue = false);
