using Microsoft.EntityFrameworkCore;
using SalonManagement.Data;
using SalonManagement.Models;

namespace SalonManagement.Services;

public sealed record AvailabilityResult(int TotalDurationMinutes, IReadOnlyList<TimeSpan> Slots);
public sealed record SuggestedAvailability(DateTime Date, int SlotCount, TimeSpan EarliestSlot);

public sealed class AvailabilityService(ApplicationDbContext db, TimeProvider timeProvider)
{
    public AvailabilityService(ApplicationDbContext db) : this(db, TimeProvider.System) { }

    public async Task<AvailabilityResult> GetSlotsAsync(DateTime date, IReadOnlyCollection<int> serviceIds)
    {
        var services = await db.Services.AsNoTracking().Where(s => s.IsActive && serviceIds.Contains(s.ServiceId)).ToListAsync();
        if (services.Count != serviceIds.Distinct().Count()) return new AvailabilityResult(0, []);
        var duration = services.Sum(s => s.DurationMinutes);
        if (await db.ShopHolidays.AnyAsync(h => h.HolidayDate == DateOnly.FromDateTime(date)))
            return new AvailabilityResult(duration, []);
        var timeOffs = await db.StylistTimeOffs.AsNoTracking().Where(t => t.OffDate == DateOnly.FromDateTime(date)).ToListAsync();
        var requiredIds = serviceIds.Distinct().ToHashSet();
        var candidates = await db.Stylists.AsNoTracking().Where(s => s.IsActive)
            .Include(s => s.Services)
            .Include(s => s.WorkSchedules.Where(w => w.WorkDate == date.Date)).ToListAsync();
        var hours = await db.BusinessHours.AsNoTracking().SingleOrDefaultAsync(item => item.DayOfWeek == date.DayOfWeek);
        if (hours?.IsClosed == true) return new AvailabilityResult(duration, []);
        var opensAt = (hours?.OpensAt ?? new TimeOnly(8, 0)).ToTimeSpan();
        var closesAt = (hours?.ClosesAt ?? new TimeOnly(17, 0)).ToTimeSpan();
        var daysOff = await db.StylistDaysOff.AsNoTracking().Where(item => item.OffDate == date.Date).Select(item => item.StylistId).ToListAsync();
        var breaks = await db.StylistBreaks.AsNoTracking().Where(item => item.BreakDate == date.Date).ToListAsync();
        var eligible = candidates.Where(s => !daysOff.Contains(s.StylistId) && requiredIds.All(id => s.Services.Any(skill => skill.ServiceId == id))).ToList();
        var appointments = await db.Appointments.AsNoTracking().Where(a => a.AppointmentDate == date.Date && a.Status != "Cancelled" && a.Status != "NoShow").ToListAsync();
        var slots = eligible.SelectMany(stylist => stylist.WorkSchedules.SelectMany(shift => SlotsForShift(shift, duration, opensAt, closesAt)
                .Where(slot => !timeOffs.Any(t => t.StylistId == stylist.StylistId && (t.IsFullDay || (t.StartTime.HasValue && t.EndTime.HasValue && t.StartTime.Value.ToTimeSpan() < slot + TimeSpan.FromMinutes(duration) && slot < t.EndTime.Value.ToTimeSpan()))) && !appointments.Where(a => a.StylistId == stylist.StylistId).Any(a => a.StartTime < slot + TimeSpan.FromMinutes(duration) && slot < a.EndTime) && !breaks.Where(b => b.StylistId == stylist.StylistId).Any(b => b.StartTime < slot + TimeSpan.FromMinutes(duration) && slot < b.EndTime))))
            .Distinct().OrderBy(time => time).ToList();

        var now = SalonClock.GetLocalNow(timeProvider);
        if (date.Date == now.Date)
            slots = slots.Where(slot => slot >= now.TimeOfDay.Add(TimeSpan.FromHours(1))).ToList();

        return new AvailabilityResult(duration, slots);
    }

    public async Task<IReadOnlyList<SuggestedAvailability>> GetSuggestedDatesAsync(DateTime selectedDate, IReadOnlyCollection<int> serviceIds, int maximumDaysToSearch = 14)
    {
        var suggestions = new List<SuggestedAvailability>();
        for (var offset = 1; offset <= maximumDaysToSearch && suggestions.Count < 2; offset++)
        {
            var candidate = selectedDate.Date.AddDays(offset);
            var availability = await GetSlotsAsync(candidate, serviceIds);
            if (availability.Slots.Count > 0)
                suggestions.Add(new SuggestedAvailability(candidate, availability.Slots.Count, availability.Slots[0]));
        }

        return suggestions;
    }

    private static IEnumerable<TimeSpan> SlotsForShift(WorkSchedule shift, int duration, TimeSpan opensAt, TimeSpan closesAt)
    {
        var earliest = shift.StartTime < opensAt ? opensAt : shift.StartTime;
        var latest = shift.EndTime > closesAt ? closesAt : shift.EndTime;
        for (var start = earliest; start + TimeSpan.FromMinutes(duration) <= latest; start += TimeSpan.FromMinutes(15))
            yield return start;
    }
}
