using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SalonManagement.Data;
using SalonManagement.Models;

namespace SalonManagement.Services;

public sealed record RescheduleAppointmentRequest(int StylistId, TimeSpan StartTime);
public sealed record AppointmentChangeActor(string Id, string Name);
public sealed record RescheduleAppointmentResult(bool Succeeded, string? ErrorCode, string? Error, Appointment? Appointment, string? NotificationStatus = null)
{
    public static RescheduleAppointmentResult Rejected(string code, string error) => new(false, code, error, null);
    public static RescheduleAppointmentResult Success(Appointment appointment, string? notificationStatus = null) => new(true, null, null, appointment, notificationStatus);
}

public sealed class AppointmentReschedulingService
{
    private readonly ApplicationDbContext db;
    private readonly TimeProvider timeProvider;
    private readonly AppointmentEmailNotificationService emailNotifications;
    private readonly StylistInAppNotificationService notifications;

    [ActivatorUtilitiesConstructor]
    public AppointmentReschedulingService(ApplicationDbContext db, TimeProvider timeProvider, AppointmentEmailNotificationService emailNotifications, StylistInAppNotificationService notifications)
    {
        this.db = db;
        this.timeProvider = timeProvider;
        this.emailNotifications = emailNotifications;
        this.notifications = notifications;
    }

    // Keeps existing isolated tests and maintenance tools source-compatible.
    public AppointmentReschedulingService(ApplicationDbContext db, TimeProvider timeProvider)
        : this(db, timeProvider, new AppointmentEmailNotificationService(db, timeProvider), new StylistInAppNotificationService(db, timeProvider)) { }

    private static readonly string[] UnavailableStatuses = ["Cancelled", "Rejected", "NoShow", "Completed", "InProgress"];

    public Task<RescheduleAppointmentResult> PreviewAsync(int appointmentId, RescheduleAppointmentRequest request)
        => RescheduleAsync(appointmentId, request, actor: null, queueCustomerEmail: false, persist: false);

    public async Task<RescheduleAppointmentResult> RescheduleAsync(
        int appointmentId,
        RescheduleAppointmentRequest request,
        AppointmentChangeActor? actor = null,
        bool queueCustomerEmail = true,
        bool persist = true)
    {
        var appointment = await db.Appointments
            .Include(item => item.AppointmentServices)
            .Include(item => item.Customer)
            .SingleOrDefaultAsync(item => item.AppointmentId == appointmentId);
        if (appointment is null) return RescheduleAppointmentResult.Rejected("not_found", "Không tìm thấy lịch hẹn.");
        if (UnavailableStatuses.Contains(appointment.Status, StringComparer.Ordinal))
            return RescheduleAppointmentResult.Rejected("status_not_editable", "Lịch hẹn ở trạng thái hiện tại không thể sửa.");

        var stylist = await db.Stylists
            .Include(item => item.Services)
            .SingleOrDefaultAsync(item => item.StylistId == request.StylistId && item.IsActive);
        if (stylist is null) return RescheduleAppointmentResult.Rejected("stylist_unavailable", "Thợ được chọn không còn hoạt động.");

        var requiredServiceIds = appointment.AppointmentServices.Select(item => item.ServiceId).Distinct().ToList();
        var missingServices = await db.Services.AsNoTracking()
            .Where(service => requiredServiceIds.Contains(service.ServiceId) && !stylist.Services.Select(skill => skill.ServiceId).Contains(service.ServiceId))
            .Select(service => service.ServiceName)
            .ToListAsync();
        if (missingServices.Count > 0)
            return RescheduleAppointmentResult.Rejected("F33", $"F33: Thợ không thực hiện được dịch vụ: {string.Join(", ", missingServices)}.");

        var duration = appointment.AppointmentServices.Sum(item => item.DurationMinutes);
        if (duration <= 0) return RescheduleAppointmentResult.Rejected("invalid_duration", "Lịch hẹn không có thời lượng dịch vụ hợp lệ.");
        var endTime = request.StartTime.Add(TimeSpan.FromMinutes(duration));
        var shifts = await db.WorkSchedules.AsNoTracking()
            .Where(shift => shift.StylistId == stylist.StylistId && shift.WorkDate.Date == appointment.AppointmentDate.Date && shift.Status == "Working")
            .ToListAsync();
        if (!shifts.Any(shift => shift.StartTime <= request.StartTime && endTime <= shift.EndTime))
        {
            var shiftDescription = shifts.Count == 0 ? "không có ca làm trong ngày này" : string.Join("; ", shifts.Select(shift => $"{shift.StartTime:hh\\:mm}–{shift.EndTime:hh\\:mm}"));
            return RescheduleAppointmentResult.Rejected("outside_shift", $"Khung giờ mới phải nằm trọn trong ca làm của thợ ({shiftDescription}).");
        }

        // SQLite cannot translate comparisons between TimeSpan values. Read the
        // small schedule for that stylist and day, then detect the overlap in memory.
        var appointmentsAtTarget = await db.Appointments.AsNoTracking()
            .Include(item => item.Customer)
            .Where(item => item.AppointmentId != appointment.AppointmentId && item.StylistId == stylist.StylistId && item.AppointmentDate == appointment.AppointmentDate.Date && !UnavailableStatuses.Contains(item.Status))
            .ToListAsync();
        var conflict = appointmentsAtTarget.FirstOrDefault(item => item.StartTime < endTime && request.StartTime < item.EndTime);
        if (conflict is not null)
            return RescheduleAppointmentResult.Rejected("overlap", $"Khung giờ bị chồng lấn với lịch của {conflict.Customer.FullName} lúc {conflict.StartTime:hh\\:mm}–{conflict.EndTime:hh\\:mm}.");

        var oldStylistId = appointment.StylistId;
        var oldStartTime = appointment.StartTime;
        var hasChanges = oldStylistId != stylist.StylistId || oldStartTime != request.StartTime;
        if (!hasChanges) return RescheduleAppointmentResult.Success(appointment, "NotRequired");
        if (!persist) return RescheduleAppointmentResult.Success(appointment, "NotRequired");

        appointment.StylistId = stylist.StylistId;
        appointment.StartTime = request.StartTime;
        appointment.EndTime = endTime;
        appointment.UpdatedAt = SalonClock.GetLocalNow(timeProvider);
        var changedAt = appointment.UpdatedAt.Value;
        var changedBy = actor ?? new AppointmentChangeActor("system", "Hệ thống");
        var changeLog = new AppointmentChangeLog
        {
            AppointmentId = appointment.AppointmentId,
            ActorId = changedBy.Id,
            ActorName = changedBy.Name,
            ChangedAt = changedAt,
            OldStylistId = oldStylistId == stylist.StylistId ? null : oldStylistId,
            NewStylistId = oldStylistId == stylist.StylistId ? null : stylist.StylistId,
            OldStartTime = oldStartTime == request.StartTime ? null : oldStartTime,
            NewStartTime = oldStartTime == request.StartTime ? null : request.StartTime,
            EmailStatus = queueCustomerEmail && HasValidEmail(appointment.Customer.Email) ? "Queued" : "NotRequired"
        };
        db.AppointmentChangeLogs.Add(changeLog);
        if (queueCustomerEmail && HasValidEmail(appointment.Customer.Email))
        {
            db.AppointmentChangeEmails.Add(new AppointmentChangeEmail
            {
                AppointmentChangeLog = changeLog,
                RecipientEmail = appointment.Customer.Email!.Trim(),
                StylistName = stylist.FullName,
                AppointmentDate = appointment.AppointmentDate,
                StartTime = request.StartTime,
                NextAttemptAt = changedAt
            });
        }
        await emailNotifications.CancelPendingRemindersAsync(appointment.AppointmentId, "Lịch hẹn đã đổi giờ hoặc đổi thợ; đã hủy email nhắc cũ.");
        await emailNotifications.QueueReminderForRescheduledAppointmentAsync(appointment);
        notifications.Add(stylist.Email, appointment.AppointmentId,
            oldStylistId == stylist.StylistId ? "AppointmentRescheduled" : "AppointmentAssigned",
            oldStylistId == stylist.StylistId
                ? $"Lịch của {appointment.CustomerNameOverride ?? appointment.Customer.FullName} đã dời từ {oldStartTime:hh\\:mm} sang {request.StartTime:hh\\:mm} ngày {appointment.AppointmentDate:dd/MM}."
                : $"Bạn được gán lịch hẹn mới của {appointment.CustomerNameOverride ?? appointment.Customer.FullName} lúc {request.StartTime:hh\\:mm} ngày {appointment.AppointmentDate:dd/MM}.");
        if (oldStylistId != stylist.StylistId)
        {
            var oldStylistEmail = await db.Stylists.Where(item => item.StylistId == oldStylistId).Select(item => item.Email).SingleOrDefaultAsync();
            notifications.Add(oldStylistEmail, appointment.AppointmentId, "AppointmentCancelled", $"Lịch của {appointment.CustomerNameOverride ?? appointment.Customer.FullName} đã được chuyển sang thợ khác.");
        }
        try
        {
            await db.SaveChangesAsync();
            return RescheduleAppointmentResult.Success(appointment, changeLog.EmailStatus);
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            return RescheduleAppointmentResult.Rejected("overlap", "Khung giờ này vừa được một lễ tân khác sử dụng. Vui lòng chọn giờ khác.");
        }
    }

    private static bool HasValidEmail(string? email) => !string.IsNullOrWhiteSpace(email) && System.Net.Mail.MailAddress.TryCreate(email.Trim(), out _);
}
