namespace SalonManagement.Models;

/// <summary>
/// Statuses that still need front-desk review when a stylist is unavailable.
/// Started work is deliberately excluded from reallocation.
/// </summary>
public static class ScheduleCoordinationStatuses
{
    public static readonly string[] NeedsReview = ["Pending", "Confirmed", "Arrived"];
}

public sealed record ScheduleCoordinationStylist(int Id, string Name);

public sealed record ScheduleCoordinationAppointment(
    int Id,
    string CustomerName,
    string CustomerPhone,
    string? CustomerEmail,
    string[] Services,
    string StartTime,
    int DurationMinutes,
    string Status,
    ScheduleCoordinationPlan Plan);

public sealed record ScheduleCoordinationPlan(
    IReadOnlyList<ScheduleCoordinationStylistOption> Stylists,
    bool HasOptions);

public sealed record ScheduleCoordinationStylistOption(
    int StylistId,
    string StylistName,
    bool AvailableAtOriginalTime,
    IReadOnlyList<string> ReasonsUnavailableAtOriginalTime,
    IReadOnlyList<string> SuggestedStartTimes);

public sealed record ScheduleCoordinationAppointmentsResponse(
    int StylistId,
    DateOnly Date,
    int TotalCount,
    IReadOnlyList<ScheduleCoordinationAppointment> Appointments);
