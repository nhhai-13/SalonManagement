using Microsoft.EntityFrameworkCore;
using SalonManagement.Data;
using SalonManagement.Models;

namespace SalonManagement.Services;

public sealed record AvailabilityResult(int TotalDurationMinutes, IReadOnlyList<TimeSpan> Slots);

public sealed class AvailabilityService(ApplicationDbContext db)
{
    public async Task<AvailabilityResult> GetSlotsAsync(DateTime date, IReadOnlyCollection<int> serviceIds)
    {
        var services = await db.Services.AsNoTracking().Where(s => s.IsActive && serviceIds.Contains(s.ServiceId)).ToListAsync();
        if (services.Count != serviceIds.Distinct().Count()) return new AvailabilityResult(0, []);
        var duration = services.Sum(s => s.DurationMinutes);
        var requiredIds = serviceIds.Distinct().ToHashSet();
        var candidates = await db.Stylists.AsNoTracking().Where(s => s.IsActive)
            .Include(s => s.Services)
            .Include(s => s.WorkSchedules.Where(w => w.WorkDate == date.Date)).ToListAsync();
        var eligible = candidates.Where(s => requiredIds.All(id => s.Services.Any(skill => skill.ServiceId == id))).ToList();
        var appointments = await db.Appointments.AsNoTracking().Where(a => a.AppointmentDate == date.Date && a.Status != "Cancelled" && a.Status != "NoShow").ToListAsync();
        var slots = eligible.SelectMany(stylist => stylist.WorkSchedules.SelectMany(shift => SlotsForShift(shift, duration)
                .Where(slot => !appointments.Where(a => a.StylistId == stylist.StylistId).Any(a => a.StartTime < slot + TimeSpan.FromMinutes(duration) && slot < a.EndTime))))
            .Distinct().OrderBy(time => time).ToList();
        return new AvailabilityResult(duration, slots);
    }

    private static IEnumerable<TimeSpan> SlotsForShift(WorkSchedule shift, int duration)
    {
        for (var start = shift.StartTime; start + TimeSpan.FromMinutes(duration) <= shift.EndTime; start += TimeSpan.FromMinutes(15))
            yield return start;
    }
}
