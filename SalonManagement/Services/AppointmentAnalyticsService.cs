using Microsoft.EntityFrameworkCore;
using SalonManagement.Data;
using SalonManagement.Models;
using System.ComponentModel.DataAnnotations;

namespace SalonManagement.Services;

public class AppointmentAnalyticsService(ApplicationDbContext db)
{
    public async Task<AppointmentAnalyticsResponse> GetAsync(
        AppointmentAnalyticsRequest request, CancellationToken cancellationToken = default)
    {
        Validator.ValidateObject(request, new ValidationContext(request), validateAllProperties: true);
        var start = request.StartDate!.Value.ToDateTime(TimeOnly.MinValue);
        var end = request.EndDate!.Value.ToDateTime(TimeOnly.MinValue);
        // AppointmentDate and StartTime are salon-local wall-clock values, not UTC.
        // Group only appointments: service joins would multiply the denominator.
        var groups = await db.Appointments.AsNoTracking()
            .Where(a => a.AppointmentDate >= start && a.AppointmentDate.Date <= end)
            .GroupBy(a => new { a.Status, a.StartTime, Date = a.AppointmentDate.Date })
            .Select(g => new { g.Key.Status, g.Key.StartTime, g.Key.Date, Count = g.Count() })
            .ToListAsync(cancellationToken);
        // One query supplies both metrics and chart, even when appointments change concurrently.
        // Bucket in memory because SQLite cannot translate TimeSpan arithmetic.
        var counts = new int[24];
        var unassigned = 0;
        foreach (var group in groups)
        {
            if (group.StartTime < TimeSpan.Zero || group.StartTime >= TimeSpan.FromDays(1))
                unassigned += group.Count;
            else counts[(int)group.StartTime.TotalHours] += group.Count;
        }
        var dayCounts = new int[3];
        for (var day = request.StartDate.Value.DayNumber; day <= request.EndDate.Value.DayNumber; day++)
            dayCounts[DayGroup(DateOnly.FromDayNumber(day).DayOfWeek)]++;
        var keys = new[] { "weekday", "saturday", "sunday" };
        var labels = new[] { "Ngày thường", "Thứ bảy", "Chủ nhật" };
        var comparisons = Enumerable.Range(0, 3).Select(index =>
        {
            var subset = groups.Where(group => DayGroup(group.Date.DayOfWeek) == index).ToList();
            var hours = new int[24];
            var invalid = 0;
            foreach (var group in subset)
            {
                if (group.StartTime < TimeSpan.Zero || group.StartTime >= TimeSpan.FromDays(1)) invalid += group.Count;
                else hours[(int)group.StartTime.TotalHours] += group.Count;
            }
            var peak = hours.Max();
            return new AppointmentDayTypeComparison(keys[index], labels[index], dayCounts[index], subset.Sum(g => g.Count),
                subset.Where(g => string.Equals(g.Status, AppointmentStatuses.NoShow, StringComparison.OrdinalIgnoreCase)).Sum(g => g.Count),
                subset.Where(g => string.Equals(g.Status, AppointmentStatuses.Cancelled, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(g.Status, "Canceled", StringComparison.OrdinalIgnoreCase)).Sum(g => g.Count),
                dayCounts[index] == 0 ? null : Enumerable.Range(0, 24).Select(hour => new AppointmentComparisonBucket(
                    $"{hour:00}:00", $"{hour + 1:00}:00", hours[hour], hours[hour] / (decimal)dayCounts[index],
                    peak > 0 && hours[hour] == peak)).ToArray(), invalid);
        }).ToArray();
        return new(request.StartDate.Value, request.EndDate.Value,
            groups.Sum(g => g.Count),
            groups.Where(g => string.Equals(g.Status, AppointmentStatuses.NoShow, StringComparison.OrdinalIgnoreCase)).Sum(g => g.Count),
            groups.Where(g => string.Equals(g.Status, AppointmentStatuses.Cancelled, StringComparison.OrdinalIgnoreCase)
                || string.Equals(g.Status, "Canceled", StringComparison.OrdinalIgnoreCase)).Sum(g => g.Count))
        {
            HourlyBuckets = Enumerable.Range(0, 24)
                .Select(hour => new AppointmentHourlyBucket($"{hour:00}:00", $"{hour + 1:00}:00", counts[hour])).ToArray(),
            UnassignedTimeCount = unassigned,
            DayTypes = comparisons
        };
    }

    private static int DayGroup(DayOfWeek day) => day == DayOfWeek.Saturday ? 1 : day == DayOfWeek.Sunday ? 2 : 0;
}
