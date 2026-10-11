using Microsoft.EntityFrameworkCore;
using SalonManagement.Data;

namespace SalonManagement.Services;

public sealed record AppointmentCancellationResult(bool Succeeded, string? ErrorCode, string? Error)
{
    public static AppointmentCancellationResult Rejected(string code, string error) => new(false, code, error);
    public static AppointmentCancellationResult Success() => new(true, null, null);
}

public sealed class AppointmentCancellationService(ApplicationDbContext db, TimeProvider timeProvider)
{
    private static readonly TimeSpan MinimumNotice = TimeSpan.FromMinutes(120);

    public async Task<AppointmentCancellationResult> CancelAsync(string? reference)
    {
        var normalizedReference = reference?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(normalizedReference)) return AppointmentCancellationResult.Rejected("invalid_reference", "Mã lịch hẹn không hợp lệ.");
        var appointment = await db.Appointments.SingleOrDefaultAsync(item => item.BookingReference == normalizedReference);
        if (appointment is null) return AppointmentCancellationResult.Rejected("not_found", "Không tìm thấy lịch hẹn.");
        if (appointment.Status == "Cancelled") return AppointmentCancellationResult.Rejected("already_cancelled", "Lịch hẹn này đã được huỷ.");

        var appointmentAt = appointment.AppointmentDate.Date.Add(appointment.StartTime);
        if (appointmentAt - SalonClock.GetLocalNow(timeProvider) < MinimumNotice)
            return AppointmentCancellationResult.Rejected("too_late", "Lịch hẹn còn dưới 120 phút. Vui lòng gọi điện cho tiệm để được hỗ trợ.");

        appointment.Status = "Cancelled";
        var cancelledAt = SalonClock.GetLocalNow(timeProvider);
        appointment.CancelledAt = cancelledAt;
        appointment.CancelledBy = "Khách hàng";
        appointment.UpdatedAt = cancelledAt;
        await db.SaveChangesAsync();
        return AppointmentCancellationResult.Success();
    }
}
