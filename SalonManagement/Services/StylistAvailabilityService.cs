using Microsoft.EntityFrameworkCore;
using SalonManagement.Data;
using SalonManagement.Models;

namespace SalonManagement.Services;

public sealed record BookingStylist(int Id, string Name, string? Image, string? Specialty);
public sealed record StylistSlot(string Start, string End);

public sealed class StylistAvailabilityService(ApplicationDbContext db, TimeProvider clock)
{
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
    {
        var services = await SelectedServices(serviceIds);
        var ids = services.Select(s => s.ServiceId).ToArray();
        if (!await db.Stylists.AnyAsync(s => s.StylistId == stylistId && s.IsActive &&
            s.Services.Count(link => ids.Contains(link.ServiceId)) == ids.Length))
            throw new ArgumentException("Thợ đã chọn không còn phù hợp. Vui lòng chọn lại thợ.");

        var day = date.ToDateTime(TimeOnly.MinValue);
        var now = SalonNow;
        if (day < now.Date || date == DateOnly.MaxValue) return [];
        var nextDay = day.AddDays(1);
        var hours = await db.BusinessHours.AsNoTracking().SingleOrDefaultAsync(h => h.DayOfWeek == day.DayOfWeek);
        if (hours == null || hours.IsClosed || hours.OpensAt == null || hours.ClosesAt == null) return [];
        var open = hours.OpensAt.Value.ToTimeSpan();
        var close = hours.ClosesAt.Value.ToTimeSpan();
        if (open >= close) return [];
        var shifts = await db.WorkSchedules.AsNoTracking().Where(s => s.StylistId == stylistId &&
            s.WorkDate >= day && s.WorkDate < nextDay && s.Status == "Working")
            .OrderBy(s => s.StartTime).Select(s => new { s.StartTime, s.EndTime }).ToListAsync();
        var busy = await db.Appointments.AsNoTracking().Where(a => a.StylistId == stylistId &&
            a.AppointmentDate >= day && a.AppointmentDate < nextDay && a.Status != "Cancelled")
            .Select(a => new { a.StartTime, a.EndTime }).ToListAsync();

        // Union overlapping/adjacent shifts, but never bridge a break.
        var windows = new List<(TimeSpan Start, TimeSpan End)>();
        foreach (var shift in shifts)
        {
            var start = shift.StartTime > open ? shift.StartTime : open;
            var end = shift.EndTime < close ? shift.EndTime : close;
            if (start >= end) continue;
            if (windows.Count > 0 && start <= windows[^1].End)
                windows[^1] = (windows[^1].Start, end > windows[^1].End ? end : windows[^1].End);
            else windows.Add((start, end));
        }
        var duration = services.Sum(s => (long)s.DurationMinutes);
        if (duration > 24 * 60) return [];
        var length = TimeSpan.FromMinutes(duration);
        var slots = new List<StylistSlot>();
        foreach (var window in windows)
        {
            // Quarter-hour starts; an appointment ending exactly at start does not overlap.
            var first = TimeSpan.FromMinutes(Math.Ceiling(window.Start.TotalMinutes / 15) * 15);
            for (var start = first; start + length <= window.End; start += TimeSpan.FromMinutes(15))
            {
                var end = start + length;
                if (day + start <= now || busy.Any(a => start < a.EndTime && end > a.StartTime)) continue;
                slots.Add(new(start.ToString(@"hh\:mm"), end.ToString(@"hh\:mm")));
            }
        }
        return slots;
    }
}
