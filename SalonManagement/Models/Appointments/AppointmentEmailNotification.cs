namespace SalonManagement.Models;

/// <summary>Durable queue and audit log for confirmation/reminder email delivery.</summary>
public sealed class AppointmentEmailNotification
{
    public int AppointmentEmailNotificationId { get; set; }
    public int AppointmentId { get; set; }
    public string NotificationType { get; set; } = string.Empty;
    public string? RecipientEmail { get; set; }
    public string Status { get; set; } = "Queued";
    public int AttemptCount { get; set; }
    public DateTime ScheduledAt { get; set; }
    public DateTime NextAttemptAt { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? LastError { get; set; }
    public Appointment Appointment { get; set; } = null!;
}
