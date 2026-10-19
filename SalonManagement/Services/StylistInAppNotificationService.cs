using Microsoft.EntityFrameworkCore;
using SalonManagement.Data;
using SalonManagement.Models;

namespace SalonManagement.Services;

/// <summary>Creates lightweight, appointment-focused inbox events for stylists.</summary>
public sealed class StylistInAppNotificationService(ApplicationDbContext db, TimeProvider timeProvider)
{
    public void Add(string? recipientEmail, int appointmentId, string type, string message)
    {
        if (string.IsNullOrWhiteSpace(recipientEmail)) return;
        db.StaffNotifications.Add(new StaffNotification
        {
            RecipientEmail = recipientEmail.Trim(),
            AppointmentId = appointmentId,
            Type = type,
            Message = message,
            CreatedAt = SalonClock.GetLocalNow(timeProvider).ToUniversalTime()
        });
    }

    public Task<int> DeleteOlderThanThirtyDaysAsync(CancellationToken cancellationToken = default)
    {
        var cutoff = SalonClock.GetLocalNow(timeProvider).ToUniversalTime().AddDays(-30);
        return db.StaffNotifications.Where(item => item.CreatedAt < cutoff).ExecuteDeleteAsync(cancellationToken);
    }
}
