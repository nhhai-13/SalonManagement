namespace SalonManagement.Models;

/// <summary>Pending absence or shift-change request submitted by a stylist for reception to coordinate.</summary>
public sealed class StylistLeaveRequest
{
    public int StylistLeaveRequestId { get; set; }
    public int StylistId { get; set; }
    public DateOnly OffDate { get; set; }
    public bool IsFullDay { get; set; } = true;
    public TimeOnly? StartTime { get; set; }
    public TimeOnly? EndTime { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Stylist Stylist { get; set; } = null!;
}
