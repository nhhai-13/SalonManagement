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
    string Status);

public sealed record ScheduleCoordinationAppointmentsResponse(
    int StylistId,
    DateOnly Date,
    int TotalCount,
    IReadOnlyList<ScheduleCoordinationAppointment> Appointments);
