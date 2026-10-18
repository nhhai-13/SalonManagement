namespace SalonManagement.Models;

public sealed class CustomerProfileDetailsViewModel
{
    public Customer Customer { get; init; } = null!;
    public int VisitCount { get; init; }
    public int NoShowCount { get; init; }
    public int OnlineVisitCount { get; init; }
    public int CounterVisitCount { get; init; }
    public string? ShopNotes { get; init; }
}
