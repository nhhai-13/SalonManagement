namespace SalonManagement.Models;

public class StylistService
{
    public int StylistId { get; set; }
    public int ServiceId { get; set; }

    public Stylist Stylist { get; set; } = null!;
    public Service Service { get; set; } = null!;
}
