namespace SalonManagement.Models.ViewModels;

public sealed class HomeViewModel
{
    public IReadOnlyList<Service> Services { get; init; } = [];
    public IReadOnlyList<Service> BookingServices { get; init; } = [];
    public IReadOnlyList<Stylist> Stylists { get; init; } = [];
}
