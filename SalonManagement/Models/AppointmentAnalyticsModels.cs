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

    private decimal? Rate(int count) => Total == 0
        ? null
        : Math.Round(count * 100m / Total, 1, MidpointRounding.AwayFromZero);
}
