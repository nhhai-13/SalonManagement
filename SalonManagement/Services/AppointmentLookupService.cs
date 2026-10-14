using Microsoft.EntityFrameworkCore;
using SalonManagement.Data;
using SalonManagement.Models;

namespace SalonManagement.Services;

public sealed record AppointmentLookupItem(
    string Reference,
    DateTime Date,
    TimeSpan StartTime,
    TimeSpan EndTime,
    IReadOnlyList<string> Services,
    string StylistName,
    int DurationMinutes,
    string Status,
    DateTimeOffset? CancelledAt,
    string? CancelledBy);

public sealed record AppointmentLookupResult(
    IReadOnlyList<AppointmentLookupItem> Items,
    string? Error);

public sealed record CancelResult(bool Success, string? Error);

public sealed class AppointmentLookupService(ApplicationDbContext db, TimeProvider clock, AppointmentNotificationBus? notifications = null)
{
    public async Task<AppointmentLookupResult> SearchAsync(string? input)
    {
        var value = input?.Trim() ?? string.Empty;

        if (value.Length == 0)
            return new([], "Vui lòng nhập mã lịch hẹn hoặc số điện thoại.");

        if (value.Length == 8)
        {
            if (!value.All(character => char.IsLetterOrDigit(character)))
                return new([], "Mã lịch hẹn phải gồm đúng 8 ký tự chữ hoặc số.");

            var appointment = await DetailQuery()
                .FirstOrDefaultAsync(item => item.BookingReference == value.ToUpperInvariant());

            return appointment is null
                ? new([], "Không tìm thấy lịch hẹn.")
                : new([ToItem(appointment)], null);
        }

        var phone = NormalizePhone(value);
        if (phone is null)
            return new([], "Nhập mã 8 ký tự hoặc số điện thoại hợp lệ.");

        var appointments = await DetailQuery()
            .Where(item => item.Customer.Phone == phone
                        && item.Status != AppointmentStatuses.Cancelled
                        && item.Status != AppointmentStatuses.Completed
                        && item.Status != AppointmentStatuses.NoShow)
            .OrderBy(item => item.AppointmentDate)
            .ThenBy(item => item.StartTime)
            .ToListAsync();

        var items = appointments.Select(ToItem).ToList();

        return items.Count == 0
            ? new([], "Không tìm thấy lịch hẹn.")
            : new(items, null);
    }

    public async Task<CancelResult> CancelAsync(string reference, string actorId, string? reason = null)
    {
        var appointment = await db.Appointments
            .FirstOrDefaultAsync(a => a.BookingReference == reference.ToUpperInvariant());

        if (appointment is null)
            return new(false, "Không tìm thấy lịch hẹn.");

        if (appointment.Status == AppointmentStatuses.Cancelled)
            return new(false, "Lịch hẹn đã bị huỷ trước đó.");

        if (appointment.Status == AppointmentStatuses.Completed)
            return new(false, "Lịch hẹn đã hoàn thành, không thể huỷ.");

        if (appointment.Status == AppointmentStatuses.NoShow)
            return new(false, "Lịch hẹn đã đánh dấu không đến, không thể huỷ.");

        var now = clock.GetUtcNow();

        var startsAt = new DateTimeOffset(
            DateTime.SpecifyKind(
                appointment.AppointmentDate.Date.Add(appointment.StartTime),
                DateTimeKind.Unspecified),
            TimeSpan.FromHours(7));

        if (now > startsAt.AddMinutes(-120))
            return new(false, "Chỉ có thể huỷ lịch trước giờ hẹn ít nhất 120 phút. Vui lòng gọi điện cho tiệm.");

        var previousStatus = appointment.Status;

        appointment.Status = AppointmentStatuses.Cancelled;
        appointment.CancelledAt = now;
        appointment.CancelledBy = actorId;
        appointment.CancelReason = reason;
        appointment.UpdatedAt = now.UtcDateTime;
        appointment.Version = Guid.NewGuid();
        var notification = new StylistNotification { StylistId = appointment.StylistId, AppointmentId = appointment.AppointmentId,
            Message = $"Lịch #{appointment.AppointmentId} đã bị khách hủy.", CreatedAt = now };
        db.StylistNotifications.Add(notification);

        db.AppointmentAudits.Add(new AppointmentAudit
        {
            AppointmentId = appointment.AppointmentId,
            ActorId = actorId,
            Action = "cancel",
            PreviousStatus = previousStatus,
            NewStatus = AppointmentStatuses.Cancelled,
            OccurredAt = now
        });

        await db.SaveChangesAsync();
        notifications?.Publish(notification);
        return new(true, null);
    }

    private IQueryable<Appointment> DetailQuery() =>
        db.Appointments
            .AsNoTracking()
            .Include(item => item.Customer)
            .Include(item => item.Stylist)
            .Include(item => item.AppointmentServices)
                .ThenInclude(item => item.Service);

    private static AppointmentLookupItem ToItem(Appointment appointment)
    {
        var serviceDuration = appointment.AppointmentServices.Sum(item => item.DurationMinutes);
        var scheduledDuration = (appointment.EndTime - appointment.StartTime).TotalMinutes;
        var duration = scheduledDuration > 0 ? (int)scheduledDuration : serviceDuration;
        var cancelledBy = string.IsNullOrWhiteSpace(appointment.CancelledBy)
            ? null
            : appointment.CancelledBy.StartsWith("customer:", StringComparison.OrdinalIgnoreCase)
                ? "Khách hàng"
                : "Nhân viên tiệm";

        return new(
            appointment.BookingReference,
            appointment.AppointmentDate,
            appointment.StartTime,
            appointment.EndTime,
            appointment.AppointmentServices.Select(item => item.Service.ServiceName).ToList(),
            appointment.Stylist?.FullName ?? "Chưa phân công",
            duration,
            StatusLabel(appointment.Status),
            appointment.CancelledAt,
            cancelledBy);
    }

    private static string StatusLabel(string status) => status switch
    {
        "Pending" => "Chờ xác nhận",
        "Confirmed" => "Đã xác nhận",
        "Arrived" => "Đã đến",
        "InProgress" => "Đang thực hiện",
        "Completed" => "Đã hoàn thành",
        "Closed" => "Đã đóng",
        "Cancelled" => "Đã huỷ",
        "NoShow" => "Không đến",
        _ => status
    };

    private static string? NormalizePhone(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;

        var digits = new string(input.Where(char.IsDigit).ToArray());

        if (digits.StartsWith("84") && digits.Length == 11)
            digits = "0" + digits.Substring(2);

        if (digits.Length != 10 || !digits.StartsWith("0"))
            return null;

        return digits;
    }
}
