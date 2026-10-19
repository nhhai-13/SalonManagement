namespace SalonManagement.Models;

public sealed class StaffNotification
{
    public int StaffNotificationId { get; set; }
    public string RecipientEmail { get; set; } = string.Empty;
    public int AppointmentId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReadAt { get; set; }
}
