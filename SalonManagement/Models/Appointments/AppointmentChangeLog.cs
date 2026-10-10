namespace SalonManagement.Models;

public sealed class AppointmentChangeLog
{
    public int AppointmentChangeLogId { get; set; }
    public int AppointmentId { get; set; }
    public string ActorId { get; set; } = string.Empty;
    public string ActorName { get; set; } = string.Empty;
    public DateTime ChangedAt { get; set; }
    public int? OldStylistId { get; set; }
    public int? NewStylistId { get; set; }
    public TimeSpan? OldStartTime { get; set; }
    public TimeSpan? NewStartTime { get; set; }
}
