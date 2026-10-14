namespace SalonManagement.Models;

public static class ScheduleContactCategories
{
    public const string NoAlternative = "NoAlternative";
    public const string TransferFailed = "TransferFailed";
}

public static class ScheduleContactStatuses
{
    public const string Pending = "Pending";
    public const string Contacted = "Contacted";
}

public class ScheduleContactFollowUp
{
    public int ScheduleContactFollowUpId { get; set; }
    public int AppointmentId { get; set; }
    public string Category { get; set; } = ScheduleContactCategories.NoAlternative;
    public string Reason { get; set; } = string.Empty;
    public DateTime OriginalAppointmentDate { get; set; }
    public TimeSpan OriginalStartTime { get; set; }
    public string ContactStatus { get; set; } = ScheduleContactStatuses.Pending;
    public string? ContactNote { get; set; }
    public DateTime? ContactedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? UpdatedAt { get; set; }
    public Appointment Appointment { get; set; } = null!;
}
