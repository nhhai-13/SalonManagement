using System.ComponentModel.DataAnnotations;

namespace SalonManagement.Models.ViewModels;

public sealed class WeeklyWorkScheduleViewModel
{
    public int? StylistId { get; set; }
    public DateTime WeekStart { get; set; }
    public IReadOnlyList<Stylist> Stylists { get; set; } = [];
    public IReadOnlyList<WorkSchedule> Schedules { get; set; } = [];
    public IReadOnlyDictionary<DayOfWeek, BusinessHour> BusinessHours { get; set; }
        = new Dictionary<DayOfWeek, BusinessHour>();

    public DateTime WeekEnd => WeekStart.AddDays(6);
}

public sealed class CreateWorkScheduleViewModel : IValidatableObject
{
    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn thợ cần xếp ca.")]
    public int StylistId { get; set; }

    [DataType(DataType.Date)]
    public DateTime WorkDate { get; set; }

    [DataType(DataType.Time)]
    public TimeSpan StartTime { get; set; }

    [DataType(DataType.Time)]
    public TimeSpan EndTime { get; set; }

    [Display(Name = "Ghi chú nghỉ giữa ca")]
    [StringLength(500, ErrorMessage = "Ghi chú nghỉ giữa ca không được vượt quá 500 ký tự.")]
    public string? Notes { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (StartTime >= EndTime)
            yield return new ValidationResult(
                "Giờ bắt đầu phải sớm hơn giờ kết thúc.",
                [nameof(StartTime), nameof(EndTime)]);
    }
}

public sealed class EditWorkScheduleViewModel : IValidatableObject
{
    [Range(1, int.MaxValue)]
    public int WorkScheduleId { get; set; }

    [DataType(DataType.Time)]
    public TimeSpan StartTime { get; set; }

    [DataType(DataType.Time)]
    public TimeSpan EndTime { get; set; }

    [Display(Name = "Ghi chú nghỉ giữa ca")]
    [StringLength(500, ErrorMessage = "Ghi chú nghỉ giữa ca không được vượt quá 500 ký tự.")]
    public string? Notes { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (StartTime >= EndTime)
            yield return new ValidationResult(
                "Giờ bắt đầu phải sớm hơn giờ kết thúc.",
                [nameof(StartTime), nameof(EndTime)]);
    }
}

public enum CopyConflictResolution
{
    Skip,
    Overwrite
}

public sealed class CopyWorkWeekViewModel
{
    [Range(1, int.MaxValue)]
    public int StylistId { get; set; }
    public DateTime SourceWeekStart { get; set; }
    public CopyConflictResolution ConflictResolution { get; set; } = CopyConflictResolution.Skip;
}

public sealed record CopyWeekPreviewResponse(
    DateTime TargetWeekStart,
    IReadOnlyList<CopyWeekConflictDay> ConflictDays);

public sealed record CopyWeekConflictDay(DateTime Date, int ExistingShiftCount);

public sealed record DeleteWorkSchedulePreviewResponse(
    int WorkScheduleId,
    IReadOnlyList<BlockedAppointmentViewModel> RelatedAppointments);

public sealed record BlockedAppointmentViewModel(
    int AppointmentId,
    string CustomerName,
    TimeSpan StartTime,
    TimeSpan EndTime,
    IReadOnlyList<string> ServiceNames);
