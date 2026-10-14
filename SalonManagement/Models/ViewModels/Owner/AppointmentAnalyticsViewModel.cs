namespace SalonManagement.Models.ViewModels.Owner;

public sealed class AppointmentAnalyticsViewModel
{
    public DateTime From { get; init; }
    public DateTime To { get; init; }
    public IReadOnlyList<HourlyCount> TotalByHour { get; init; } = [];
    public IReadOnlyList<DayTypeSummary> DayTypes { get; init; } = [];
    public int TotalAppointments { get; init; }
    public int NoShows { get; init; }
    public int Cancellations { get; init; }
    public int ReviewCount { get; init; }
    public double? AverageRating { get; init; }
    public string? Error { get; init; }
}

public sealed record HourlyCount(int Hour, int Count);
public sealed record DayTypeSummary(string Name, int Days, int Appointments, double PerDay, double NoShowRate, double CancellationRate, IReadOnlyList<double> HourlyPerDay, int PeakHour);

public sealed class OwnerRevenueViewModel
{
    public DateTime From { get; init; }
    public DateTime To { get; init; }
    public int ClosedInvoiceCount { get; init; }
    public decimal ActualRevenue { get; init; }
    public IReadOnlyList<StylistRevenueRow> ByStylist { get; init; } = [];
    public IReadOnlyList<ServiceRevenueRow> ByService { get; init; } = [];
}

public sealed record StylistRevenueRow(string StylistName, int CompletedAppointments, decimal ActualRevenue, decimal AverageTicket);
public sealed record ServiceRevenueRow(string ServiceName, int Count, decimal Amount);

public sealed class OwnerTodayDashboardViewModel
{
    public DateTime Date { get; init; }
    public IReadOnlyDictionary<string, int> StatusCounts { get; init; } = new Dictionary<string, int>();
    public decimal CollectedRevenue { get; init; }
    public IReadOnlyList<WorkingStylistRow> WorkingStylists { get; init; } = [];
}

public sealed record WorkingStylistRow(string Name, TimeSpan Start, TimeSpan End);
