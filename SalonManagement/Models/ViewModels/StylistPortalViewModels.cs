namespace SalonManagement.Models.ViewModels;

public sealed class StylistWorkScheduleViewModel
{
    public string StylistName { get; init; } = string.Empty;
    public DateTime Date { get; init; }
    public IReadOnlyList<WorkSchedule> Shifts { get; init; } = [];
}

public sealed class StylistServiceHistoryViewModel
{
    public string StylistName { get; init; } = string.Empty;
    public IReadOnlyList<Appointment> Appointments { get; init; } = [];
}

public sealed class StylistWeeklyScheduleViewModel
{
    public string StylistName { get; init; } = string.Empty;
    public DateTime WeekStart { get; init; }
    public IReadOnlyList<StylistWeekDayViewModel> Days { get; init; } = [];
    public IReadOnlyList<StylistLeaveRequest> LeaveRequests { get; init; } = [];
}

public sealed class StylistWeekDayViewModel
{
    public DateTime Date { get; init; }
    public IReadOnlyList<WorkSchedule> Shifts { get; init; } = [];
    public IReadOnlyList<StylistTimeOff> ApprovedTimeOffs { get; init; } = [];
}
