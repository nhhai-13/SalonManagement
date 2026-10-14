using System.ComponentModel.DataAnnotations;

namespace SalonManagement.Models;

public class AppointmentAnalyticsRequest : IValidatableObject
{
    public const int MaximumDays = 366;

    [Required(ErrorMessage = "Vui lòng chọn ngày bắt đầu.")]
    public DateOnly? StartDate { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn ngày kết thúc.")]
    public DateOnly? EndDate { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (StartDate is not { } start || EndDate is not { } end) yield break;
        if (end < start)
            yield return new ValidationResult("Ngày kết thúc phải từ ngày bắt đầu trở đi.", [nameof(EndDate)]);
        else if (end.DayNumber - start.DayNumber + 1 > MaximumDays)
            yield return new ValidationResult($"Khoảng ngày tối đa {MaximumDays} ngày.", [nameof(EndDate)]);
    }
}

public record AppointmentAnalyticsResponse(
    DateOnly StartDate, DateOnly EndDate, int Total, int NoShowCount, int CancelledCount)
{
    public decimal? NoShowRate => Rate(NoShowCount);
    public decimal? CancellationRate => Rate(CancelledCount);
    public string? EmptyReason => Total == 0 ? "Không có lịch hẹn trong khoảng ngày này" : null;
    public bool IsSmallSample => Total is > 0 and < 20;
    public string TimeZone => "Asia/Ho_Chi_Minh";
    public IReadOnlyList<AppointmentHourlyBucket> HourlyBuckets { get; init; } = [];
    public int UnassignedTimeCount { get; init; }
    public IReadOnlyList<AppointmentDayTypeComparison> DayTypes { get; init; } = [];

    private decimal? Rate(int count) => Total == 0
        ? null
        : Math.Round(count * 100m / Total, 1, MidpointRounding.AwayFromZero);
}

public record AppointmentHourlyBucket(string Start, string End, int Count);

public record AppointmentComparisonBucket(string Start, string End, int Count, decimal AveragePerDay, bool IsPeak);

public record AppointmentDayTypeComparison(
    string Key, string Label, int DayCount, int Total, int NoShowCount, int CancelledCount,
    IReadOnlyList<AppointmentComparisonBucket>? HourlyBuckets, int UnassignedTimeCount)
{
    public decimal? AveragePerDay => DayCount == 0 ? null : Math.Round(Total / (decimal)DayCount, 1, MidpointRounding.AwayFromZero);
    public decimal? NoShowRate => Total == 0 ? null : Math.Round(NoShowCount * 100m / Total, 1, MidpointRounding.AwayFromZero);
    public decimal? CancellationRate => Total == 0 ? null : Math.Round(CancelledCount * 100m / Total, 1, MidpointRounding.AwayFromZero);
    public string? EmptyReason => DayCount == 0
        ? $"Khoảng ngày chưa có {Label.ToLowerInvariant()}"
        : Total == 0 ? "Không có lịch hẹn trong nhóm ngày này" : null;
}
