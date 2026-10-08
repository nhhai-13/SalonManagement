namespace SalonManagement.Models;

public sealed class StylistService
{
    public int StylistId { get; set; }
    public Stylist Stylist { get; set; } = null!;

    public int ServiceId { get; set; }
    public Service Service { get; set; } = null!;
}
