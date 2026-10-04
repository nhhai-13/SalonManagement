namespace SalonManagement.Models;

public class ShopHoliday
{
    public int ShopHolidayId { get; set; }

    // Ngày toàn bộ salon nghỉ
    public DateOnly HolidayDate { get; set; }

    // Lý do nghỉ, ví dụ: Tết, bảo trì...
    public string? Reason { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}