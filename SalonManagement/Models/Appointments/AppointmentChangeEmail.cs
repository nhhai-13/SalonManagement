namespace SalonManagement.Models;

public sealed class AppointmentChangeEmail
{
    public int AppointmentChangeEmailId { get; set; }
    public int AppointmentChangeLogId { get; set; }
    public AppointmentChangeLog AppointmentChangeLog { get; set; } = null!;
    public string RecipientEmail { get; set; } = string.Empty;
    public string StylistName { get; set; } = string.Empty;
    public DateTime AppointmentDate { get; set; }
    public TimeSpan StartTime { get; set; }
    public string Status { get; set; } = "Queued";
    public int AttemptCount { get; set; }
    public DateTime NextAttemptAt { get; set; }
    public DateTime? SentAt { get; set; }
    public string? LastError { get; set; }
}
