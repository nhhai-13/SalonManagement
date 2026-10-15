using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalonManagement.Data;
using SalonManagement.Models;
using SalonManagement.Services;

namespace SalonManagement.Controllers.StylistPortal;

[Authorize(Roles = UserRoles.Stylist)]
[ApiController]
[Route("api/stylist/command")]
public sealed class StylistCommandCenterController(ApplicationDbContext db, TimeProvider timeProvider) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<StylistCommandSnapshot>> Get()
    {
        var stylist = await CurrentStylistAsync();
        if (stylist is null) return Forbid();

        var now = SalonClock.GetLocalNow(timeProvider);
        var today = now.Date;
        var appointments = (await db.Appointments.AsNoTracking()
                .Include(item => item.Customer)
                .Include(item => item.AppointmentServices).ThenInclude(item => item.Service)
                .Where(item => item.StylistId == stylist.StylistId && item.AppointmentDate == today)
                .ToListAsync())
            .OrderBy(item => item.StartTime).ThenBy(item => item.AppointmentId).ToList();
        var shifts = (await db.WorkSchedules.AsNoTracking()
                .Where(item => item.StylistId == stylist.StylistId && item.WorkDate == today && item.Status == "Working")
                .ToListAsync())
            .OrderBy(item => item.StartTime).ToList();
        var active = appointments.FirstOrDefault(item => item.Status == "InProgress");
        var upcoming = appointments.Where(item => item.Status is "Arrived" or "Confirmed" or "PendingConfirmation" or "Pending")
            .Where(item => active is null || item.AppointmentId != active.AppointmentId)
            .Take(6).Select(ToItem).ToList();
        var unread = await db.StaffNotifications.AsNoTracking()
            .CountAsync(item => item.RecipientEmail == stylist.Email && item.ReadAt == null);

        return Ok(new StylistCommandSnapshot(
            now,
            stylist.FullName,
            shifts.Select(item => new StylistShift(item.StartTime, item.EndTime, item.Notes)).ToList(),
            active is null ? null : ToItem(active),
            appointments.Count(item => item.Status == "InProgress"),
            appointments.Count(item => item.Status is "Arrived" or "Confirmed" or "PendingConfirmation" or "Pending"),
            appointments.Count(item => item.Status == "Completed"),
            unread,
            upcoming));
    }

    [HttpGet("appointments/{appointmentId:int}/technical")]
    public async Task<ActionResult<TechnicalDetailsResponse>> TechnicalDetails(int appointmentId)
    {
        var stylist = await CurrentStylistAsync();
        if (stylist is null) return Forbid();
        var appointment = await db.Appointments.AsNoTracking().Include(item => item.Customer)
            .SingleOrDefaultAsync(item => item.AppointmentId == appointmentId && item.StylistId == stylist.StylistId);
        if (appointment is null) return NotFound(new { error = "Không tìm thấy lượt hẹn của bạn." });
        var history = (await db.Appointments.AsNoTracking().Include(item => item.Stylist).Include(item => item.AppointmentServices).ThenInclude(item => item.Service)
                .Where(item => item.CustomerId == appointment.CustomerId && item.AppointmentId != appointment.AppointmentId && item.Status == "Completed")
                .ToListAsync())
            .OrderByDescending(item => item.AppointmentDate).ThenByDescending(item => item.StartTime).Take(10)
            .Select(item => new TechnicalHistoryItem(item.AppointmentDate, string.Join(", ", item.AppointmentServices.Select(service => service.Service.ServiceName)), item.Stylist.FullName, item.TechnicalNotes))
            .ToList();
        return Ok(new TechnicalDetailsResponse(appointment.AppointmentId, appointment.CustomerNameOverride ?? appointment.Customer.FullName, appointment.TechnicalNotes, history));
    }

    [HttpPost("appointments/{appointmentId:int}/technical")]
    public async Task<IActionResult> SaveTechnicalNotes(int appointmentId, [FromBody] SaveTechnicalNotesRequest request)
    {
        var stylist = await CurrentStylistAsync();
        if (stylist is null) return Forbid();
        var notes = request.Notes?.Trim();
        if (notes?.Length > 500) return BadRequest(new { error = "Ghi chú kỹ thuật tối đa 500 ký tự." });
        var appointment = await db.Appointments.SingleOrDefaultAsync(item => item.AppointmentId == appointmentId && item.StylistId == stylist.StylistId);
        if (appointment is null) return NotFound(new { error = "Không tìm thấy lượt hẹn của bạn." });
        appointment.TechnicalNotes = string.IsNullOrEmpty(notes) ? null : notes;
        appointment.UpdatedAt = SalonClock.GetLocalNow(timeProvider);
        await db.SaveChangesAsync();
        return Ok(new { message = "Đã lưu ghi chú kỹ thuật nội bộ." });
    }

    private async Task<Stylist?> CurrentStylistAsync()
    {
        var email = User.FindFirstValue(JwtRegisteredClaimNames.Email) ?? User.FindFirstValue(ClaimTypes.Email);
        return string.IsNullOrWhiteSpace(email) ? null : await db.Stylists.AsNoTracking().SingleOrDefaultAsync(item => item.Email == email && item.IsActive);
    }

    private static StylistAppointmentItem ToItem(Appointment appointment) => new(
        appointment.AppointmentId,
        appointment.CustomerNameOverride ?? appointment.Customer.FullName,
        MaskPhone(appointment.Customer.Phone),
        string.Join(", ", appointment.AppointmentServices.Select(item => item.Service.ServiceName)),
        appointment.AppointmentServices.Sum(item => item.DurationMinutes),
        appointment.StartTime,
        appointment.EndTime,
        appointment.Status,
        appointment.ArrivedAt,
        appointment.ActualStartedAt,
        appointment.Notes);

    private static string MaskPhone(string phone)
    {
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        return digits.Length < 7 ? "***" : $"{digits[..3]}.****.{digits[^3..]}";
    }
}

public sealed record StylistCommandSnapshot(DateTime GeneratedAt, string StylistName, IReadOnlyList<StylistShift> Shifts, StylistAppointmentItem? CurrentAppointment, int InServiceCount, int WaitingCount, int CompletedCount, int UnreadNotifications, IReadOnlyList<StylistAppointmentItem> UpcomingAppointments);
public sealed record StylistShift(TimeSpan StartTime, TimeSpan EndTime, string? BreakNotes);
public sealed record StylistAppointmentItem(int AppointmentId, string CustomerName, string MaskedPhone, string Services, int StandardMinutes, TimeSpan StartTime, TimeSpan EndTime, string Status, DateTime? ArrivedAt, DateTime? ActualStartedAt, string? CustomerNote);
public sealed record TechnicalHistoryItem(DateTime AppointmentDate, string Services, string StylistName, string? TechnicalNotes);
public sealed record TechnicalDetailsResponse(int AppointmentId, string CustomerName, string? CurrentTechnicalNotes, IReadOnlyList<TechnicalHistoryItem> History);
public sealed record SaveTechnicalNotesRequest(string? Notes);
