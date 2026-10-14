namespace SalonManagement.Models;

public static class ScheduleChangeNotificationStatuses
{
    public const string Pending = "Pending";
    public const string Sending = "Sending";
    public const string Sent = "Sent";
    public const string Failed = "Failed";
}

public class ScheduleChangeNotification
{
    public int ScheduleChangeNotificationId { get; set; }
    public int AppointmentId { get; set; }
    public int Version { get; set; }
    public string RecipientEmail { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string ServiceNames { get; set; } = string.Empty;
    public DateTime AppointmentDate { get; set; }
    public string OldStylistName { get; set; } = string.Empty;
    public string NewStylistName { get; set; } = string.Empty;
    public TimeSpan OldStartTime { get; set; }
    public TimeSpan NewStartTime { get; set; }
    public DateTime TransferredAt { get; set; }
    public string Status { get; set; } = ScheduleChangeNotificationStatuses.Pending;
    public int AttemptCount { get; set; }
    public DateTime? LastAttemptAt { get; set; }
    public DateTime? NextAttemptAt { get; set; }
    public DateTime? SentAt { get; set; }
    public string? LastError { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public Appointment Appointment { get; set; } = null!;
}
