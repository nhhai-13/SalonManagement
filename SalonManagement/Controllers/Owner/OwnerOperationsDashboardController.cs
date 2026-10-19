using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalonManagement.Data;
using SalonManagement.Models;
using SalonManagement.Services;

namespace SalonManagement.Controllers.Owner;

[Authorize(Roles = UserRoles.Owner)]
public sealed class OwnerOperationsDashboardPageController : Controller
{
    [HttpGet("owner/operations-dashboard")]
    public IActionResult Index() => View("~/Views/OwnerOperationsDashboard/Index.cshtml");
}

[Authorize(Roles = UserRoles.Owner)]
[ApiController]
[Route("api/owner/operations-dashboard")]
public sealed class OwnerOperationsDashboardController(ApplicationDbContext db, TimeProvider timeProvider, AppointmentEmailNotificationService emailNotifications) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<OwnerOperationsSnapshot>> Get()
    {
        var today = SalonClock.GetLocalNow(timeProvider).Date;
        var appointments = await TodayAppointmentsAsync(today);
        // SQLite stores decimal values as text and cannot translate SUM(decimal).
        // Materialise the small daily payment set first, then total it in .NET so
        // the owner dashboard works on both SQLite demo data and SQL Server.
        var paidAmounts = await db.Payments.AsNoTracking()
            .Where(item => item.PaymentStatus == "Paid" && item.PaidAt != null && item.PaidAt >= today && item.PaidAt < today.AddDays(1) && item.Invoice.Status == "Paid")
            .Select(item => item.Amount)
            .ToListAsync();
        var paidRevenue = paidAmounts.Sum();
        var scheduledStylistIds = await db.WorkSchedules.AsNoTracking()
            .Where(item => item.WorkDate == today && item.Status == "Working")
            .Select(item => item.StylistId).Distinct().ToListAsync();
        var offStylistIds = await db.StylistTimeOffs.AsNoTracking()
            .Where(item => item.OffDate == DateOnly.FromDateTime(today) && item.IsFullDay)
            .Select(item => item.StylistId).ToListAsync();
        var onDuty = scheduledStylistIds.Count(id => !offStylistIds.Contains(id));
        return Ok(new OwnerOperationsSnapshot(today, paidRevenue, onDuty, scheduledStylistIds.Count,
            new AppointmentStatusCounts(
                appointments.Count(item => StatusKey(item.Status) == "pending"),
                appointments.Count(item => StatusKey(item.Status) == "confirmed"),
                appointments.Count(item => StatusKey(item.Status) == "arrived"),
                appointments.Count(item => StatusKey(item.Status) == "completed"),
                appointments.Count(item => StatusKey(item.Status) == "cancelled"),
                appointments.Count(item => StatusKey(item.Status) == "noshow"))));
    }

    [HttpGet("appointments")]
    public async Task<ActionResult<IReadOnlyList<OwnerDashboardAppointmentRow>>> Appointments(string status)
    {
        if (status is not ("pending" or "confirmed" or "arrived" or "completed" or "cancelled" or "noshow"))
            return BadRequest(new { error = "Nhóm trạng thái không hợp lệ." });
        var appointments = await TodayAppointmentsAsync(SalonClock.GetLocalNow(timeProvider).Date);
        return Ok(appointments.Where(item => StatusKey(item.Status) == status).OrderBy(item => item.StartTime).Select(ToRow).ToList());
    }

    [HttpPost("appointments/{appointmentId:int}/confirm")]
    public async Task<IActionResult> Confirm(int appointmentId)
    {
        var appointment = await db.Appointments.Include(item => item.Customer).Include(item => item.Stylist).Include(item => item.AppointmentServices).ThenInclude(item => item.Service)
            .SingleOrDefaultAsync(item => item.AppointmentId == appointmentId);
        if (appointment is null) return NotFound(new { error = "Không tìm thấy lịch hẹn." });
        if (appointment.Status != "PendingConfirmation") return Conflict(new { error = "Lịch hẹn này đã được xử lý." });
        if (appointment.NameConflictResolution == "PendingReceptionReview") return Conflict(new { error = "Lịch hẹn đang chờ xử lý tên hồ sơ." });
        var now = SalonClock.GetLocalNow(timeProvider);
        appointment.Status = "Confirmed";
        appointment.ConfirmedAt = now;
        appointment.ConfirmedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? "owner";
        appointment.UpdatedAt = now;
        await emailNotifications.QueueInitialNotificationsAsync(appointment);
        await db.SaveChangesAsync();
        return Ok(new { message = "Đã xác nhận lịch hẹn." });
    }

    [HttpPost("appointments/{appointmentId:int}/reject")]
    public async Task<IActionResult> Reject(int appointmentId)
    {
        var appointment = await db.Appointments.SingleOrDefaultAsync(item => item.AppointmentId == appointmentId);
        if (appointment is null) return NotFound(new { error = "Không tìm thấy lịch hẹn." });
        if (appointment.Status != "PendingConfirmation") return Conflict(new { error = "Lịch hẹn này đã được xử lý." });
        appointment.Status = "Rejected";
        appointment.RejectionReason = "Chủ tiệm từ chối từ bảng tổng quan";
        appointment.RejectedAt = SalonClock.GetLocalNow(timeProvider);
        appointment.RejectedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? "owner";
        appointment.UpdatedAt = appointment.RejectedAt;
        await db.SaveChangesAsync();
        return Ok(new { message = "Đã hủy lịch hẹn." });
    }

    private async Task<List<Appointment>> TodayAppointmentsAsync(DateTime today) => await db.Appointments.AsNoTracking()
        .Include(item => item.Customer).Include(item => item.Stylist).Include(item => item.Invoice)
        .Include(item => item.AppointmentServices).ThenInclude(item => item.Service)
        .Where(item => item.AppointmentDate == today).ToListAsync();

    private static OwnerDashboardAppointmentRow ToRow(Appointment appointment) => new(appointment.AppointmentId, appointment.BookingReference, appointment.CustomerNameOverride ?? appointment.Customer.FullName, MaskPhone(appointment.Customer.Phone), string.Join(", ", appointment.AppointmentServices.Select(item => item.Service.ServiceName)), appointment.Stylist?.FullName ?? "Chưa phân công", appointment.StartTime, appointment.EndTime, appointment.Status, appointment.Invoice?.Status == "Paid");
    private static string? StatusKey(string status) => status switch { "Pending" or "PendingConfirmation" => "pending", "Confirmed" => "confirmed", "Arrived" or "InProgress" => "arrived", "Completed" => "completed", "Cancelled" or "Rejected" => "cancelled", "NoShow" => "noshow", _ => null };
    private static string MaskPhone(string phone) { var digits = new string(phone.Where(char.IsDigit).ToArray()); return digits.Length < 7 ? "***" : $"{digits[..3]}.***.{digits[^3..]}"; }
}

public sealed record OwnerOperationsSnapshot(DateTime Date, decimal RevenueToday, int StylistsOnDuty, int StylistsScheduled, AppointmentStatusCounts Statuses);
public sealed record AppointmentStatusCounts(int Pending, int Confirmed, int Arrived, int Completed, int Cancelled, int NoShow);
public sealed record OwnerDashboardAppointmentRow(int AppointmentId, string Reference, string CustomerName, string MaskedPhone, string Services, string StylistName, TimeSpan StartTime, TimeSpan EndTime, string Status, bool IsPaid);
