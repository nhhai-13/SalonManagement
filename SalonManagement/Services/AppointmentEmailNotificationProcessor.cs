using Microsoft.EntityFrameworkCore;
using SalonManagement.Data;

namespace SalonManagement.Services;

public sealed class AppointmentEmailNotificationProcessor(ApplicationDbContext db, IEmailService emailService, TimeProvider timeProvider)
{
    public async Task ProcessPendingAsync(CancellationToken cancellationToken = default)
    {
        var now = SalonClock.GetLocalNow(timeProvider);
        var notifications = await db.AppointmentEmailNotifications
            .Include(item => item.Appointment).ThenInclude(item => item.Customer)
            .Include(item => item.Appointment).ThenInclude(item => item.Stylist)
            .Include(item => item.Appointment).ThenInclude(item => item.AppointmentServices).ThenInclude(item => item.Service)
            .Where(item => (item.Status == "Queued" || item.Status == "Retrying") && item.NextAttemptAt <= now)
            .OrderBy(item => item.NextAttemptAt).Take(20).ToListAsync(cancellationToken);

        foreach (var notification in notifications)
        {
            if (notification.Appointment.Status is "Cancelled" or "Rejected" or "NoShow")
            {
                notification.Status = "Cancelled";
                notification.CancelledAt = now;
                notification.LastError = "Lịch hẹn đã hủy hoặc không còn hiệu lực.";
                continue;
            }
            if (string.IsNullOrWhiteSpace(notification.RecipientEmail)) continue;

            notification.Status = "Sending";
            notification.AttemptCount++;
            await db.SaveChangesAsync(cancellationToken);
            try
            {
                var appointment = notification.Appointment;
                await emailService.SendAppointmentNotificationEmailAsync(
                    notification.RecipientEmail,
                    appointment.CustomerNameOverride ?? appointment.Customer.FullName,
                    appointment.BookingReference,
                    string.Join(", ", appointment.AppointmentServices.Select(item => item.Service.ServiceName)),
                    appointment.Stylist.FullName,
                    appointment.AppointmentDate,
                    appointment.StartTime,
                    notification.NotificationType == "Reminder");
                notification.Status = "Sent";
                notification.SentAt = SalonClock.GetLocalNow(timeProvider);
                notification.LastError = null;
            }
            catch (Exception exception)
            {
                notification.LastError = exception.Message[..Math.Min(exception.Message.Length, 1000)];
                if (notification.AttemptCount >= 3)
                {
                    notification.Status = "Failed";
                    notification.LastError = $"Đã thử 3 lần: {notification.LastError}";
                }
                else
                {
                    notification.Status = "Retrying";
                    notification.NextAttemptAt = SalonClock.GetLocalNow(timeProvider).AddMinutes(10);
                }
            }
            await db.SaveChangesAsync(cancellationToken);
        }
        await db.SaveChangesAsync(cancellationToken);
    }
}
