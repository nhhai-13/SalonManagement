namespace SalonManagement.Models;

public sealed class ReceptionShiftReportViewModel
{
    public DateTime Date { get; init; }
    public decimal TotalCollected { get; init; }
    public IReadOnlyList<ReceptionShiftPaymentItem> Payments { get; init; } = [];
}

public sealed class ReceptionShiftPaymentItem
{
    public string CustomerName { get; init; } = string.Empty;
    public string StylistName { get; init; } = string.Empty;
    public string Services { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public string Method { get; init; } = string.Empty;
    public DateTime PaidAt { get; init; }
}
