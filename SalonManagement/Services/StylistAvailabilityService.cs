using Microsoft.EntityFrameworkCore;
using SalonManagement.Data;
using SalonManagement.Models;

namespace SalonManagement.Services;

public sealed record BookingStylist(int Id, string Name, string? Image, string? Specialty);
public sealed record StylistSlot(string Start, string End);
public sealed record StylistAssignment(int StylistId, string Name, DateOnly Date, string Start, string End);

public sealed class StylistAvailabilityService(ApplicationDbContext db, TimeProvider clock)
{
    public const int AnyStylist = 0;
    public DateTime SalonNow => TimeZoneInfo.ConvertTime(clock.GetUtcNow(),
        TimeZoneInfo.FindSystemTimeZoneById(BusinessHour.SalonTimeZone)).DateTime;

    private async Task<List<Service>> SelectedServices(IEnumerable<int> serviceIds)
    {
        var ids = serviceIds.Distinct().ToArray();
        if (ids.Length == 0 || ids.Any(id => id <= 0))
            throw new ArgumentException("Vui lòng chọn dịch vụ hợp lệ.");
        var services = await db.Services.AsNoTracking()
            .Where(s => ids.Contains(s.ServiceId) && s.IsActive).ToListAsync();
        if (services.Count != ids.Length || services.Any(s => s.DurationMinutes <= 0))
            throw new ArgumentException("Dịch vụ đã thay đổi hoặc ngừng bán. Vui lòng chọn lại.");
        return services;
    }

    public async Task<List<BookingStylist>> GetStylistsAsync(IEnumerable<int> serviceIds)
    {
        var services = await SelectedServices(serviceIds);
        var ids = services.Select(s => s.ServiceId).ToArray();
        return await db.Stylists.AsNoTracking()
            .Where(s => s.IsActive && s.Services.Count(link => ids.Contains(link.ServiceId)) == ids.Length)
            .OrderBy(s => s.FullName).ThenBy(s => s.StylistId)
            .Select(s => new BookingStylist(s.StylistId, s.FullName,
                s.ProfileImagePath == null ? null : "/" + s.ProfileImagePath.TrimStart('/'), s.Specialty))
            .ToListAsync();
    }

    public async Task<List<StylistSlot>> GetSlotsAsync(IEnumerable<int> serviceIds, int stylistId, DateOnly date)
        => (await GetStylistSlotsAsync(serviceIds, stylistId, date))
            .Select(s => new StylistSlot(s.Start, s.End)).Distinct()
            .OrderBy(s => s.Start, StringComparer.Ordinal).ThenBy(s => s.End, StringComparer.Ordinal).ToList();

    // Read-only assignment decision. The eventual appointment writer must revalidate under its transaction.
    public async Task<StylistAssignment?> AssignAsync(IEnumerable<int> serviceIds, int stylistId, DateOnly date, TimeOnly start)
    {
        var startText = start.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture);
        if (start.Second != 0 || start.Ticks % TimeSpan.TicksPerMinute != 0) return null;
        var candidates = (await GetStylistSlotsAsync(serviceIds, stylistId, date)).Where(s => s.Start == startText).ToList();
        if (candidates.Count == 0) return null;
        var candidateIds = candidates.Select(s => s.StylistId).Distinct().ToArray();
        var day = date.ToDateTime(TimeOnly.MinValue);
        var nextDay = day.AddDays(1);
        // Proposed PO policy: appointment count, Pending + Confirmed, stable ID tie-break.
        var ranked = await db.Stylists.AsNoTracking().Where(s => candidateIds.Contains(s.StylistId) && s.IsActive)
            .Select(s => new
            {
                s.StylistId, s.FullName,
                Count = s.Appointments.Count(a => a.AppointmentDate >= day && a.AppointmentDate < nextDay &&
                    (a.Status == "Pending" || a.Status == "Confirmed"))
            }).OrderBy(s => s.Count).ThenBy(s => s.StylistId).FirstOrDefaultAsync();
        if (ranked == null) return null;
        var slot = candidates.First(s => s.StylistId == ranked.StylistId);
        return new(ranked.StylistId, ranked.FullName, date, slot.Start, slot.End);
    }

    private sealed record AvailableStylistSlot(int StylistId, string Start, string End);

    private async Task<List<AvailableStylistSlot>> GetStylistSlotsAsync(IEnumerable<int> serviceIds, int stylistId, DateOnly date)
    {
        var services = await SelectedServices(serviceIds);
        var ids = services.Select(s => s.ServiceId).ToArray();
        if (stylistId < AnyStylist) throw new ArgumentException("Lựa chọn thợ không hợp lệ.");
        var qualifiedIds = await db.Stylists.AsNoTracking()
            .Where(s => (stylistId == AnyStylist || s.StylistId == stylistId) && s.IsActive &&
                s.Services.Count(link => ids.Contains(link.ServiceId)) == ids.Length)
            .Select(s => s.StylistId).ToListAsync();
        if (stylistId != AnyStylist && qualifiedIds.Count == 0)
            throw new ArgumentException("Thợ đã chọn không còn phù hợp. Vui lòng chọn lại thợ.");
        if (qualifiedIds.Count == 0) return [];

        var day = date.ToDateTime(TimeOnly.MinValue);
        var now = SalonNow;
        if (day < now.Date || date == DateOnly.MaxValue) return [];
        var nextDay = day.AddDays(1);
        var hours = await db.BusinessHours.AsNoTracking().SingleOrDefaultAsync(h => h.DayOfWeek == day.DayOfWeek);
        if (hours == null || hours.IsClosed || hours.OpensAt == null || hours.ClosesAt == null) return [];
        var open = hours.OpensAt.Value.ToTimeSpan();
        var close = hours.ClosesAt.Value.ToTimeSpan();
        if (open >= close) return [];
        var shifts = await db.WorkSchedules.AsNoTracking().Where(s => qualifiedIds.Contains(s.StylistId) &&
            s.WorkDate >= day && s.WorkDate < nextDay && s.Status == "Working")
            .OrderBy(s => s.StartTime).Select(s => new { s.StylistId, s.StartTime, s.EndTime }).ToListAsync();
        var busy = await db.Appointments.AsNoTracking().Where(a => qualifiedIds.Contains(a.StylistId) &&
            a.AppointmentDate >= day && a.AppointmentDate < nextDay && a.Status != "Cancelled")
            .Select(a => new { a.StylistId, a.StartTime, a.EndTime }).ToListAsync();
        var timeOffs = await db.StylistTimeOffs.AsNoTracking().Where(timeOff =>
                qualifiedIds.Contains(timeOff.StylistId) && timeOff.OffDate == date)
            .Select(timeOff => new
            {
                timeOff.StylistId,
                timeOff.IsFullDay,
                timeOff.StartTime,
                timeOff.EndTime
            }).ToListAsync();

        var duration = services.Sum(s => (long)s.DurationMinutes);
        if (duration > 24 * 60) return [];
        var length = TimeSpan.FromMinutes(duration);
        var busyByStylist = busy.ToLookup(a => a.StylistId);
        var timeOffByStylist = timeOffs.ToLookup(timeOff => timeOff.StylistId);
        var slots = new HashSet<AvailableStylistSlot>();
        // Calculate each stylist independently: never stitch two people's free time together.
        foreach (var stylistShifts in shifts.GroupBy(s => s.StylistId))
        {
            var stylistBusy = busyByStylist[stylistShifts.Key].ToList();
            var stylistTimeOffs = timeOffByStylist[stylistShifts.Key].ToList();

            if (stylistTimeOffs.Any(timeOff => timeOff.IsFullDay)) continue;

            // Union overlapping/adjacent shifts, but never bridge a break.
            var windows = new List<(TimeSpan Start, TimeSpan End)>();
            foreach (var shift in stylistShifts)
            {
                var start = shift.StartTime > open ? shift.StartTime : open;
                var end = shift.EndTime < close ? shift.EndTime : close;
                if (start >= end) continue;
                if (windows.Count > 0 && start <= windows[^1].End)
                    windows[^1] = (windows[^1].Start, end > windows[^1].End ? end : windows[^1].End);
                else windows.Add((start, end));
            }
            foreach (var window in windows)
            {
                // Quarter-hour starts; an appointment ending exactly at start does not overlap.
                var first = TimeSpan.FromMinutes(Math.Ceiling(window.Start.TotalMinutes / 15) * 15);
                for (var start = first; start + length <= window.End; start += TimeSpan.FromMinutes(15))
                {
                    var end = start + length;
                    var overlapsTimeOff = stylistTimeOffs.Any(timeOff =>
                        !timeOff.IsFullDay &&
                        timeOff.StartTime.HasValue &&
                        timeOff.EndTime.HasValue &&
                        start < timeOff.EndTime.Value.ToTimeSpan() &&
                        end > timeOff.StartTime.Value.ToTimeSpan());
                    if (day + start <= now ||
                        stylistBusy.Any(a => start < a.EndTime && end > a.StartTime) ||
                        overlapsTimeOff) continue;
                    slots.Add(new(stylistShifts.Key, start.ToString(@"hh\:mm"), end.ToString(@"hh\:mm")));
                }
            }
        }
        return slots.OrderBy(s => s.Start, StringComparer.Ordinal).ThenBy(s => s.End, StringComparer.Ordinal).ToList();
    }
}
