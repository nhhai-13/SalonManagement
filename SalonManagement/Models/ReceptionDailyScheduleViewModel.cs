namespace SalonManagement.Models;

public sealed class ReceptionDailyScheduleViewModel
{
    public DateTime Date { get; init; }
    public TimeSpan OpeningTime { get; init; }
    public TimeSpan ClosingTime { get; init; }
    public IReadOnlyList<ReceptionScheduleStylistColumn> Stylists { get; init; } = [];
}

public sealed class ReceptionScheduleStylistColumn
{
    public int StylistId { get; init; }
    public string StylistName { get; init; } = string.Empty;
    public IReadOnlyList<ReceptionScheduleWorkingRange> WorkingRanges { get; init; } = [];
    public IReadOnlyList<ReceptionScheduleAppointment> Appointments { get; init; } = [];
}

public sealed class ReceptionScheduleWorkingRange
{
    public TimeSpan StartTime { get; init; }
    public TimeSpan EndTime { get; init; }
}

public sealed class ReceptionScheduleAppointment
{
    public int AppointmentId { get; init; }
    public int CustomerId { get; init; }
    public string CustomerName { get; init; } = string.Empty;
    public string Services { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public TimeSpan StartTime { get; init; }
    public TimeSpan EndTime { get; init; }
    public bool IsLate { get; init; }
    public int? LateMinutes { get; init; }
    public DateTimeOffset? NoShowAt { get; init; }
    public string? NoShowByUserId { get; init; }
    public DateTime? NoShowAvailableAt { get; init; }
    public bool CanMarkNoShow { get; init; }
    public DateTimeOffset? UndoNoShowDeadline { get; init; }
    public bool CanUndoNoShow { get; init; }
}
