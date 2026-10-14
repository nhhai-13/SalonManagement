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
    public string? Error { get; init; }
}

public sealed record HourlyCount(int Hour, int Count);
public sealed record DayTypeSummary(string Name, int Days, int Appointments, double PerDay, double NoShowRate, double CancellationRate, IReadOnlyList<double> HourlyPerDay, int PeakHour);
