using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SalonManagement.Data;
using SalonManagement.Models;

namespace SalonManagement.Services;

public sealed record AvailabilityResult(int TotalDurationMinutes, IReadOnlyList<TimeSpan> Slots);
public sealed record SuggestedAvailability(DateTime Date, int SlotCount, TimeSpan EarliestSlot);

 feature/s4-04/submit-review-via-booking-reference
public sealed class AvailabilityService : IAvailabilityService

public sealed class AvailabilityService(ApplicationDbContext db, TimeProvider timeProvider) : IAvailabilityService
 develop
{
    private readonly ApplicationDbContext db;
    private readonly TimeProvider timeProvider;
    private const int SlotStepMinutes = 15;
    private const int MinLeadTimeMinutes = 60;
    private const int MaxDaysToLookAhead = 14;

    public AvailabilityService(ApplicationDbContext db, TimeProvider timeProvider)
    {
        this.db = db;
        this.timeProvider = timeProvider;
    }

    public AvailabilityService(ApplicationDbContext db) : this(db, TimeProvider.System) { }

    public async Task<AvailabilityResult> GetSlotsAsync(DateTime date, IReadOnlyCollection<int> serviceIds)
    {
        var services = await db.Services.AsNoTracking().Where(s => s.IsActive && serviceIds.Contains(s.ServiceId)).ToListAsync();
        if (services.Count != serviceIds.Distinct().Count()) return new AvailabilityResult(0, []);
        var duration = services.Sum(s => s.DurationMinutes);
        var shopHolidayDates = await db.ShopHolidays.AsNoTracking()
            .Select(holiday => holiday.HolidayDate)
            .ToListAsync();
        if (shopHolidayDates.Contains(DateOnly.FromDateTime(date)))
            return new AvailabilityResult(duration, []);
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
        var appointments = await db.Appointments.AsNoTracking().Where(a => a.AppointmentDate == date.Date && a.Status != "Cancelled" && a.Status != "NoShow" && a.Status != "Rejected").ToListAsync();
        var slots = eligible.SelectMany(stylist => stylist.WorkSchedules.SelectMany(shift => SlotsForShift(shift, duration, opensAt, closesAt)
                .Where(slot => !appointments.Where(a => a.StylistId == stylist.StylistId).Any(a => a.StartTime < slot + TimeSpan.FromMinutes(duration) && slot < a.EndTime) && !breaks.Where(b => b.StylistId == stylist.StylistId).Any(b => b.StartTime < slot + TimeSpan.FromMinutes(duration) && slot < b.EndTime))))
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

< feature/s4-04/submit-review-via-booking-reference
    // =============================================================
    // IAvailabilityService implementation (for public booking API)
    // =============================================================

    // --- IAvailabilityService methods ---

 develop
    public async Task<List<AvailabilitySlot>> GetAvailableSlotsAsync(
        DateTime date,
        List<int> serviceIds,
        int? stylistId = null)
    {
 feature/s4-04/submit-review-via-booking-reference
        if (serviceIds == null || serviceIds.Count == 0)
            return new List<AvailabilitySlot>();

        date = date.Date;

        var totalDuration = await GetTotalDurationAsync(serviceIds);
        if (totalDuration <= 0)
            return new List<AvailabilitySlot>();

        var businessHour = await GetBusinessHourAsync(date);
        if (businessHour == null || businessHour.IsClosed)
            return new List<AvailabilitySlot>();

        var eligibleStylists = await GetEligibleStylistsAsync(
            date, serviceIds, stylistId);

        if (!eligibleStylists.Any())
            return new List<AvailabilitySlot>();

        var slotsByStylist = new Dictionary<int, List<AvailabilitySlot>>();

        foreach (var stylist in eligibleStylists)
        {
            var stylistSlots = await CalculateSlotsForStylistAsync(
                stylist.StylistId, date, totalDuration, businessHour);

            if (stylistSlots.Any())
                slotsByStylist[stylist.StylistId] = stylistSlots;
        }

        if (!slotsByStylist.Any())
            return new List<AvailabilitySlot>();

        return MergeSlots(slotsByStylist);

        var result = await GetSlotsAsync(date, serviceIds);
        var endOffset = TimeSpan.FromMinutes(result.TotalDurationMinutes);
        var requiredIds = serviceIds.Distinct().ToArray();
        var day = date.Date;

        // Thợ đủ kỹ năng (và đúng thợ được chọn, nếu có)
        var candidateStylistIds = await db.Stylists.AsNoTracking()
            .Where(s => s.IsActive
                && (stylistId == null || s.StylistId == stylistId)
                && s.Services.Count(link => requiredIds.Contains(link.ServiceId)) == requiredIds.Length)
            .Select(s => s.StylistId).ToListAsync();

        var breaks = await db.StylistBreaks.AsNoTracking().Where(b => b.BreakDate == day).ToListAsync();
        var appointments = await db.Appointments.AsNoTracking()
            .Where(a => a.AppointmentDate == day && a.Status != "Cancelled" && a.Status != "NoShow" && a.Status != "Rejected")
            .ToListAsync();
        var daysOff = await db.StylistDaysOff.AsNoTracking().Where(d => d.OffDate == day).Select(d => d.StylistId).ToListAsync();
        var workSchedules = await db.WorkSchedules.AsNoTracking().Where(w => w.WorkDate == day && w.Status == "Working").ToListAsync();

        var slotsWithIds = new List<AvailabilitySlot>(result.Slots.Count);
        foreach (var slotStart in result.Slots)
        {
            var slotEnd = slotStart + endOffset;
            var availabilitySlot = new AvailabilitySlot
            {
                StartTime = slotStart,
                EndTime = slotEnd,
                AvailableStylistIds = new List<int>()
            };

            foreach (var sid in candidateStylistIds)
            {
                if (daysOff.Contains(sid)) continue;
                if (!workSchedules.Any(w => w.StylistId == sid && w.StartTime <= slotStart && slotEnd <= w.EndTime)) continue;
                if (breaks.Any(b => b.StylistId == sid && b.StartTime < slotEnd && slotStart < b.EndTime)) continue;
                if (appointments.Any(a => a.StylistId == sid && a.StartTime < slotEnd && slotStart < a.EndTime)) continue;
                availabilitySlot.AvailableStylistIds.Add(sid);
            }

            // Chỉ trả slot còn ít nhất 1 thợ rảnh
            if (availabilitySlot.AvailableStylistIds.Count > 0)
                slotsWithIds.Add(availabilitySlot);
        }

        return slotsWithIds;
 develop
    }

    public async Task<List<DateTime>> FindNextAvailableDatesAsync(
        DateTime fromDate,
        int count,
        List<int> serviceIds,
        int? stylistId = null)
    {
feature/s4-04/submit-review-via-booking-reference
        var result = new List<DateTime>();
        fromDate = fromDate.Date;

        for (int i = 1; i <= MaxDaysToLookAhead && result.Count < count; i++)
        {
            var candidateDate = fromDate.AddDays(i);
            var slots = await GetAvailableSlotsAsync(
                candidateDate, serviceIds, stylistId);

            if (slots.Any())
                result.Add(candidateDate);
        }

        return result;
    }

    private async Task<int> GetTotalDurationAsync(List<int> serviceIds)
    {
        var durations = await db.Services
            .AsNoTracking()
            .Where(s => serviceIds.Contains(s.ServiceId) && s.IsActive)
            .Select(s => s.DurationMinutes)
            .ToListAsync();

        return durations.Sum();
    }

    private async Task<BusinessHour?> GetBusinessHourAsync(DateTime date)
    {
        var dayOfWeek = date.DayOfWeek;

        return await db.BusinessHours
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.DayOfWeek == dayOfWeek);
    }

    private async Task<List<Stylist>> GetEligibleStylistsAsync(
        DateTime date,
        List<int> serviceIds,
        int? stylistId)
    {
        var query = db.Stylists
            .AsNoTracking()
            .Where(s => s.IsActive);

        if (stylistId.HasValue)
        {
            query = query.Where(s => s.StylistId == stylistId.Value);
        }

        query = query.Where(s => s.WorkSchedules
            .Any(w => w.WorkDate.Date == date.Date
                   && w.Status == "Working"));

        query = query.Where(s =>
            serviceIds.All(sid => s.Services.Any(ss => ss.ServiceId == sid)));

        return await query.ToListAsync();
    }

    private async Task<List<AvailabilitySlot>> CalculateSlotsForStylistAsync(
        int stylistId,
        DateTime date,
        int totalDuration,
        BusinessHour businessHour)
    {
        var workSchedules = await db.WorkSchedules
            .AsNoTracking()
            .Where(w => w.StylistId == stylistId
                     && w.WorkDate.Date == date.Date
                     && w.Status == "Working")
            .OrderBy(w => w.StartTime)
            .ToListAsync();

        if (!workSchedules.Any())
            return new List<AvailabilitySlot>();

        var busyAppointments = await db.Appointments
            .AsNoTracking()
            .Where(a => a.StylistId == stylistId
                     && a.AppointmentDate.Date == date.Date
                     && AppointmentStatus.BusyStatuses.Contains(a.Status))
            .Select(a => new { a.StartTime, a.EndTime })
            .ToListAsync();

        var result = new List<AvailabilitySlot>();

        var openTime = businessHour.OpensAt!.Value.ToTimeSpan();
        var closeTime = businessHour.ClosesAt!.Value.ToTimeSpan();

        var now = DateTime.Now;
        TimeSpan? minStartTime = null;
        if (date.Date == now.Date)
        {
            minStartTime = now.TimeOfDay + TimeSpan.FromMinutes(MinLeadTimeMinutes);
        }

        foreach (var work in workSchedules)
        {
            var caStart = work.StartTime > openTime ? work.StartTime : openTime;
            var caEnd = work.EndTime < closeTime ? work.EndTime : closeTime;

            if (caEnd <= caStart)
                continue;

            for (var slotStart = caStart;
                 slotStart + TimeSpan.FromMinutes(totalDuration) <= caEnd;
                 slotStart = slotStart.Add(TimeSpan.FromMinutes(SlotStepMinutes)))
            {
                var slotEnd = slotStart + TimeSpan.FromMinutes(totalDuration);

                if (minStartTime.HasValue && slotStart < minStartTime.Value)
                    continue;

                bool hasConflict = busyAppointments.Any(b =>
                    slotStart < b.EndTime && slotEnd > b.StartTime);

                if (hasConflict)
                    continue;

                result.Add(new AvailabilitySlot
                {
                    StartTime = slotStart,
                    EndTime = slotEnd,
                    AvailableStylistIds = new List<int> { stylistId }
                });
            }
        }

        return result;
    }

    private static List<AvailabilitySlot> MergeSlots(
        Dictionary<int, List<AvailabilitySlot>> slotsByStylist)
    {
        var merged = slotsByStylist
            .SelectMany(kv => kv.Value)
            .GroupBy(s => s.StartTime)
            .Select(g => new AvailabilitySlot
            {
                StartTime = g.Key,
                EndTime = g.First().EndTime,
                AvailableStylistIds = g.SelectMany(s => s.AvailableStylistIds)
                                        .Distinct()
                                        .ToList()
            })
            .OrderBy(s => s.StartTime)
            .ToList();

        return merged;
    }
}

        var results = new List<DateTime>();
        var attempted = 0;
        var maxLookahead = 60; // reasonable upper bound
        while (results.Count < count && attempted < maxLookahead)
        {
            attempted++;
            var candidate = fromDate.Date.AddDays(attempted);
            var available = await GetAvailableSlotsAsync(candidate, serviceIds, stylistId);
            if (available.Any())
                results.Add(candidate);
        }
        return results;
    }
}
 develop
