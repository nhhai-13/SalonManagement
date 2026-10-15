namespace SalonManagement.Models;

public static class AppointmentStatuses
{
    public const string Pending = "Pending", Confirmed = "Confirmed", Arrived = "Arrived",
        Completed = "Completed", Cancelled = "Cancelled", NoShow = "NoShow";
    public static bool BlocksTime(string status) => status != Cancelled && status != NoShow;
}
public class AppointmentAudit
{
    public int Id { get; set; }
    public int AppointmentId { get; set; }
    public string ActorId { get; set; } = "";
    public string Action { get; set; } = "";
    public string PreviousStatus { get; set; } = "";
    public string NewStatus { get; set; } = "";
    public DateTimeOffset OccurredAt { get; set; }
}
public class StylistNotification
{
    public int Id { get; set; }
    public int StylistId { get; set; }
    public int AppointmentId { get; set; }
    public string Message { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
}
