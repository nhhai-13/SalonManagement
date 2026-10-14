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
            .GroupBy(a => new { a.Status, a.StartTime })
            .Select(g => new { g.Key.Status, g.Key.StartTime, Count = g.Count() })
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
        return new(request.StartDate.Value, request.EndDate.Value,
            groups.Sum(g => g.Count),
            groups.Where(g => string.Equals(g.Status, AppointmentStatuses.NoShow, StringComparison.OrdinalIgnoreCase)).Sum(g => g.Count),
            groups.Where(g => string.Equals(g.Status, AppointmentStatuses.Cancelled, StringComparison.OrdinalIgnoreCase)
                || string.Equals(g.Status, "Canceled", StringComparison.OrdinalIgnoreCase)).Sum(g => g.Count))
        {
            HourlyBuckets = Enumerable.Range(0, 24)
                .Select(hour => new AppointmentHourlyBucket($"{hour:00}:00", $"{hour + 1:00}:00", counts[hour])).ToArray(),
            UnassignedTimeCount = unassigned
        };
    }
}
