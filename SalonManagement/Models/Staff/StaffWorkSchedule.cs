namespace SalonManagement.Models;

/// <summary>Ca làm chung cho mọi tài khoản nhân viên, gồm Lễ tân và Thợ.</summary>
public sealed class StaffWorkSchedule
{
    public int StaffWorkScheduleId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public DateTime WorkDate { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public string Status { get; set; } = "Working";
    public string? Notes { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public ApplicationUser User { get; set; } = null!;
}
