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
            .GroupBy(a => a.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        return new(request.StartDate.Value, request.EndDate.Value,
            groups.Sum(g => g.Count),
            groups.Where(g => string.Equals(g.Status, AppointmentStatuses.NoShow, StringComparison.OrdinalIgnoreCase)).Sum(g => g.Count),
            groups.Where(g => string.Equals(g.Status, AppointmentStatuses.Cancelled, StringComparison.OrdinalIgnoreCase)
                || string.Equals(g.Status, "Canceled", StringComparison.OrdinalIgnoreCase)).Sum(g => g.Count));
    }
}
