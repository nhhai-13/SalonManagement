namespace SalonManagement.Models;

public class StylistTimeOff
{
    public int StylistTimeOffId { get; set; }

    // Thợ được nghỉ
    public int StylistId { get; set; }

    // Ngày nghỉ
    public DateOnly OffDate { get; set; }

    // true = nghỉ cả ngày
    // false = chỉ nghỉ theo khoảng giờ
    public bool IsFullDay { get; set; }

    // Chỉ sử dụng khi IsFullDay = false
    public TimeOnly? StartTime { get; set; }
    public TimeOnly? EndTime { get; set; }

    // Lý do nghỉ (nếu có)
    public string? Reason { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Liên kết với thợ
    public Stylist Stylist { get; set; } = null!;
}