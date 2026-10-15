namespace SalonManagement.Models;

public class BusinessHour
{
    public const string SalonTimeZone = "Asia/Ho_Chi_Minh";

    public int BusinessHourId { get; set; }
    public DayOfWeek DayOfWeek { get; set; }
    public bool IsClosed { get; set; }
    public TimeOnly? OpensAt { get; set; }
    public TimeOnly? ClosesAt { get; set; }
    public string TimeZoneId { get; set; } = SalonTimeZone;
}
