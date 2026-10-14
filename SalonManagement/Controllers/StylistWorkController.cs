using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalonManagement.Data;
using SalonManagement.Models;

namespace SalonManagement.Controllers;

[ApiController]
[Authorize(Roles = UserRoles.Stylist)]
[Route("api/stylist/work")]
public sealed class StylistWorkController(ApplicationDbContext db, TimeProvider clock) : ControllerBase
{
    [HttpGet("appointments")]
    public async Task<IActionResult> Appointments([FromQuery] DateTime? date, CancellationToken cancellationToken)
    {
        var stylistId = await CurrentStylistId(cancellationToken);
        if (stylistId is null) return Conflict(new { message = "Tài khoản thợ chưa được liên kết với hồ sơ thợ." });
        var day = (date ?? clock.GetUtcNow().ToOffset(TimeSpan.FromHours(7)).DateTime).Date;
        var now = clock.GetUtcNow();
        var appointmentRows = await db.Appointments.AsNoTracking()
            .Where(item => item.StylistId == stylistId && item.AppointmentDate >= day && item.AppointmentDate < day.AddDays(1)
                && item.Status != AppointmentStatuses.Cancelled && item.Status != AppointmentStatuses.NoShow && item.Status != "Rejected")
            .Select(item => new StylistAppointmentResponse(
                item.AppointmentId,
                item.AppointmentDate,
                item.StartTime,
                item.EndTime,
                item.Status,
                MaskPhone(item.Customer.Phone),
                item.AppointmentServices.Sum(service => service.DurationMinutes),
                item.AppointmentServices.Select(service => service.Service.ServiceName).ToList(),
                item.Notes,
                item.ServiceStartedAt,
                item.ServiceCompletedAt,
                item.ServiceStartedAt.HasValue
                    ? (int)Math.Ceiling(((item.ServiceCompletedAt ?? now) - item.ServiceStartedAt.Value).TotalMinutes)
                    : (int?)null))
            .ToListAsync(cancellationToken);
        var appointments = appointmentRows.OrderBy(item => item.StartTime).ToList();
        return Ok(appointments);
    }

    [HttpPost("appointments/{appointmentId:int}/start")]
    public Task<IActionResult> Start(int appointmentId, CancellationToken cancellationToken) => Change(appointmentId, true, cancellationToken);

    [HttpPost("appointments/{appointmentId:int}/complete")]
    public Task<IActionResult> Complete(int appointmentId, CancellationToken cancellationToken) => Change(appointmentId, false, cancellationToken);

    private async Task<IActionResult> Change(int appointmentId, bool start, CancellationToken cancellationToken)
    {
        var stylistId = await CurrentStylistId(cancellationToken);
        if (stylistId is null) return Conflict(new { message = "Tài khoản thợ chưa được liên kết với hồ sơ thợ." });
        var appointment = await db.Appointments.Include(item => item.AppointmentServices)
            .SingleOrDefaultAsync(item => item.AppointmentId == appointmentId && item.StylistId == stylistId, cancellationToken);
        if (appointment is null) return NotFound(new { message = "Không tìm thấy lịch hẹn của thợ này." });
        if (start && appointment.Status != AppointmentStatuses.Arrived)
            return Conflict(new { message = "Chỉ bắt đầu dịch vụ khi lịch đang ở trạng thái Đã đến." });
        if (!start && appointment.Status != "InProgress")
            return Conflict(new { message = "Chỉ hoàn tất dịch vụ đã bắt đầu." });

        var now = clock.GetUtcNow();
        var oldStatus = appointment.Status;
        if (start)
        {
            appointment.Status = "InProgress";
            appointment.ServiceStartedAt = now;
        }
        else
        {
            if (!appointment.ServiceStartedAt.HasValue) return Conflict(new { message = "Thiếu giờ bắt đầu dịch vụ." });
            appointment.Status = AppointmentStatuses.Completed;
            appointment.ServiceCompletedAt = now;
        }
        appointment.UpdatedAt = now.UtcDateTime;
        appointment.Version = Guid.NewGuid();
        var actorId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub") ?? string.Empty;
        db.AppointmentAudits.Add(new AppointmentAudit
        {
            AppointmentId = appointment.AppointmentId,
            ActorId = actorId,
            Action = start ? "service-start" : "service-complete",
            PreviousStatus = oldStatus,
            NewStatus = appointment.Status,
            OccurredAt = now
        });
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { return Conflict(new { message = "Lịch vừa được cập nhật. Hãy tải lại trước khi tiếp tục." }); }

        var expected = appointment.AppointmentServices.Sum(item => item.DurationMinutes);
        var actual = appointment.ServiceCompletedAt.HasValue
            ? (int)Math.Ceiling((appointment.ServiceCompletedAt.Value - appointment.ServiceStartedAt!.Value).TotalMinutes)
            : (int?)null;
        return Ok(new
        {
            appointment.AppointmentId,
            appointment.Status,
            appointment.ServiceStartedAt,
            appointment.ServiceCompletedAt,
            expectedDurationMinutes = expected,
            actualDurationMinutes = actual,
            alertReception = actual.HasValue && actual.Value > expected + 20
        });
    }

    private Task<int?> CurrentStylistId(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return db.Users.AsNoTracking().Where(user => user.Id == userId).Select(user => user.StylistId).SingleOrDefaultAsync(cancellationToken);
    }

    private static string MaskPhone(string phone) => phone.Length < 7 ? new string('*', phone.Length) : phone[..3] + "****" + phone[^3..];
}

[ApiController]
[Authorize(Roles = RoleGroups.FrontDesk)]
[Route("api/reception/service-overruns")]
public sealed class ReceptionServiceOverrunController(ApplicationDbContext db, TimeProvider clock) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] DateTime? date, CancellationToken cancellationToken)
    {
        var day = (date ?? clock.GetUtcNow().ToOffset(TimeSpan.FromHours(7)).DateTime).Date;
        var now = clock.GetUtcNow();
        var active = await db.Appointments.AsNoTracking()
            .Where(item => item.AppointmentDate >= day && item.AppointmentDate < day.AddDays(1)
                && item.Status == "InProgress" && item.ServiceStartedAt != null)
            .Select(item => new
            {
                item.AppointmentId,
                CustomerName = item.Customer.FullName,
                StylistName = item.Stylist.FullName,
                item.ServiceStartedAt,
                ExpectedMinutes = item.AppointmentServices.Sum(service => service.DurationMinutes)
            }).ToListAsync(cancellationToken);
        var overdue = active.Select(item => new
            {
                item.AppointmentId,
                item.CustomerName,
                item.StylistName,
                ExpectedMinutes = item.ExpectedMinutes,
                ActualMinutes = (int)Math.Ceiling((now - item.ServiceStartedAt!.Value).TotalMinutes)
            })
            .Where(item => item.ActualMinutes > item.ExpectedMinutes + 20)
            .OrderByDescending(item => item.ActualMinutes - item.ExpectedMinutes)
            .ToList();
        return Ok(overdue);
    }
}

public sealed record StylistAppointmentResponse(
    int AppointmentId,
    DateTime Date,
    TimeSpan StartTime,
    TimeSpan EndTime,
    string Status,
    string CustomerPhone,
    int ExpectedDurationMinutes,
    IReadOnlyList<string> Services,
    string? CustomerNote,
    DateTimeOffset? ServiceStartedAt,
    DateTimeOffset? ServiceCompletedAt,
    int? ActualDurationMinutes);
