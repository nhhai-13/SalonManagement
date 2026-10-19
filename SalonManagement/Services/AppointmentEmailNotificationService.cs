using Microsoft.EntityFrameworkCore;
using SalonManagement.Data;
using SalonManagement.Models;

namespace SalonManagement.Services;

public sealed class AppointmentEmailNotificationService(ApplicationDbContext db, TimeProvider timeProvider)
{
    public Task QueueInitialNotificationsAsync(Appointment appointment)
    {
        var now = SalonClock.GetLocalNow(timeProvider);
        if (!HasValidEmail(appointment.Customer.Email))
        {
            db.AppointmentEmailNotifications.Add(new AppointmentEmailNotification
            {
                AppointmentId = appointment.AppointmentId,
                NotificationType = "ManualCall",
                Status = "ManualCallRequired",
                ScheduledAt = now,
                NextAttemptAt = now,
                LastError = "Khách không cung cấp email; cần lễ tân gọi điện xác nhận thủ công."
            });
            return Task.CompletedTask;
        }

        db.AppointmentEmailNotifications.Add(new AppointmentEmailNotification
        {
            AppointmentId = appointment.AppointmentId,
            NotificationType = "Confirmation",
            RecipientEmail = appointment.Customer.Email!.Trim(),
            Status = "Queued",
            ScheduledAt = now,
            NextAttemptAt = now
        });
        QueueReminder(appointment, now);
        return Task.CompletedTask;
    }

    public async Task CancelPendingRemindersAsync(int appointmentId, string reason)
    {
        var now = SalonClock.GetLocalNow(timeProvider);
        var pending = await db.AppointmentEmailNotifications
            .Where(item => item.AppointmentId == appointmentId && item.NotificationType == "Reminder" && (item.Status == "Queued" || item.Status == "Retrying"))
            .ToListAsync();
        foreach (var item in pending)
        {
            item.Status = "Cancelled";
            item.CancelledAt = now;
            item.LastError = reason;
        }
    }

    public Task QueueReminderForRescheduledAppointmentAsync(Appointment appointment)
    {
        if (HasValidEmail(appointment.Customer.Email)) QueueReminder(appointment, SalonClock.GetLocalNow(timeProvider));
        return Task.CompletedTask;
    }

    private void QueueReminder(Appointment appointment, DateTime now)
    {
        var appointmentAt = appointment.AppointmentDate.Date.Add(appointment.StartTime);
        var scheduledAt = appointmentAt.AddHours(-24);
        if (scheduledAt < now) scheduledAt = now;
        db.AppointmentEmailNotifications.Add(new AppointmentEmailNotification
        {
            AppointmentId = appointment.AppointmentId,
            NotificationType = "Reminder",
            RecipientEmail = appointment.Customer.Email!.Trim(),
            Status = "Queued",
            ScheduledAt = scheduledAt,
            NextAttemptAt = scheduledAt
        });
    }

    private static bool HasValidEmail(string? email) => !string.IsNullOrWhiteSpace(email) && System.Net.Mail.MailAddress.TryCreate(email.Trim(), out _);
}
