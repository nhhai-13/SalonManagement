namespace SalonManagement.Models;

public class StylistBreak
{
    public int StylistBreakId { get; set; }
    public int StylistId { get; set; }
    public DateTime BreakDate { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public Stylist Stylist { get; set; } = null!;
}

public class StylistDayOff
{
    public int StylistDayOffId { get; set; }
    public int StylistId { get; set; }
    public DateTime OffDate { get; set; }
    public Stylist Stylist { get; set; } = null!;
}
