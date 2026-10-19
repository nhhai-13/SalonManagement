using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SalonManagement.Data;
using SalonManagement.Models;

namespace SalonManagement.Services;

public sealed record BookingStylist(int Id, string Name, string? Image, string? Specialty);
public sealed record StylistSlot(string Start, string End);

// Result for assignment API/tests
public sealed record StylistAssignment(int StylistId, string End);

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
            a.AppointmentDate >= day && a.AppointmentDate < nextDay && a.Status != "Cancelled" && a.Status != AppointmentStatuses.NoShow)
            .Select(a => new { a.StylistId, a.StartTime, a.EndTime }).ToListAsync();
 feature/s4-04/submit-review-via-booking-reference
        var timeOffs = await db.StylistTimeOffs.AsNoTracking().Where(timeOff =>
                qualifiedIds.Contains(timeOff.StylistId) && timeOff.OffDate == date)
            .Select(timeOff => new
            {
                timeOff.StylistId,
                timeOff.IsFullDay,
                timeOff.StartTime,
                timeOff.EndTime
            }).ToListAsync();

 develop

        var duration = services.Sum(s => (long)s.DurationMinutes);
        if (duration > 24 * 60) return [];
        var length = TimeSpan.FromMinutes(duration);
        var busyByStylist = busy.ToLookup(a => a.StylistId);
 feature/s4-04/submit-review-via-booking-reference
        var timeOffByStylist = timeOffs.ToLookup(timeOff => timeOff.StylistId);
        var slots = new HashSet<AvailableStylistSlot>();


        var slots = new HashSet<StylistSlot>();
 develop
        // Calculate each stylist independently: never stitch two people's free time together.
        foreach (var stylistShifts in shifts.GroupBy(s => s.StylistId))
        {
            var stylistBusy = busyByStylist[stylistShifts.Key].ToList();
 feature/s4-04/submit-review-via-booking-reference
            var stylistTimeOffs = timeOffByStylist[stylistShifts.Key].ToList();

            if (stylistTimeOffs.Any(t => t.IsFullDay))
                continue;

 develop

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
 feature/s4-04/submit-review-via-booking-reference
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

                    if (day + start <= now || stylistBusy.Any(a => start < a.EndTime && end > a.StartTime)) continue;
                    slots.Add(new(start.ToString(@"hh\:mm"), end.ToString(@"hh\:mm")));
develop
                }
            }
        }
        return slots.OrderBy(s => s.Start, StringComparer.Ordinal).ThenBy(s => s.End, StringComparer.Ordinal).ToList();
    }

    // Returns a human message for a given day (used by controller)
    public async Task<string> GetDayNoticeAsync(DateOnly date)
    {
        if (date == default) return string.Empty;
        var day = date.ToDateTime(TimeOnly.MinValue);
        var holiday = await db.ShopHolidays.AsNoTracking().FirstOrDefaultAsync(h => h.HolidayDate == date);
        if (holiday != null) return $"Tiệm nghỉ ngày {date:dd/MM/yyyy}: {holiday.Reason}";
        var hours = await db.BusinessHours.AsNoTracking().SingleOrDefaultAsync(h => h.DayOfWeek == day.DayOfWeek);
        if (hours == null || hours.IsClosed || hours.OpensAt == null || hours.ClosesAt == null) return "Tiệm đóng cửa";
        var opens = hours.OpensAt.Value.ToString("hh\\:mm");
        var closes = hours.ClosesAt.Value.ToString("hh\\:mm");
        return $"Giờ làm việc: {opens} – {closes}";
    }

    // Assign stylist for a specific requested slot.
    // Returns StylistAssignment with chosen stylist id and formatted End "HH:mm", or null if none.
    public async Task<StylistAssignment?> AssignAsync(IEnumerable<int> serviceIds, int stylistId, DateOnly date, TimeOnly start)
    {
        var services = await SelectedServices(serviceIds);
        var ids = services.Select(s => s.ServiceId).ToArray();
        if (stylistId < AnyStylist) throw new ArgumentException("Lựa chọn thợ không hợp lệ.");

        var day = date.ToDateTime(TimeOnly.MinValue);
        var now = SalonNow;
        if (day < now.Date || date == DateOnly.MaxValue) return null;

        var duration = services.Sum(s => s.DurationMinutes);
        var length = TimeSpan.FromMinutes(duration);
        var startTs = start.ToTimeSpan();
        var endTs = startTs + length;

        // Không cho đặt giờ đã qua (giống GetSlotsAsync)
        if (day + startTs <= now) return null;

        // Basic data for validation
        var hours = await db.BusinessHours.AsNoTracking().SingleOrDefaultAsync(h => h.DayOfWeek == day.DayOfWeek);
        if (hours == null || hours.IsClosed || hours.OpensAt == null || hours.ClosesAt == null) return null;
        var open = hours.OpensAt.Value.ToTimeSpan();
        var close = hours.ClosesAt.Value.ToTimeSpan();
        if (startTs < open || endTs > close || startTs >= endTs) return null;

        var candidateQuery = db.Stylists.AsNoTracking()
            .Where(s => s.IsActive && s.Services.Count(link => ids.Contains(link.ServiceId)) == ids.Length);
        if (stylistId != AnyStylist) candidateQuery = candidateQuery.Where(s => s.StylistId == stylistId);

        var candidateIds = await candidateQuery.Select(s => s.StylistId).ToListAsync();
        if (!candidateIds.Any()) return null;

        var dayStart = day;
        var dayEnd = day.AddDays(1);
        var shifts = await db.WorkSchedules.AsNoTracking()
            .Where(w => candidateIds.Contains(w.StylistId) && w.WorkDate >= dayStart && w.WorkDate < dayEnd && w.Status == "Working")
            .ToListAsync();

        // check working shift contains the requested interval
        var qualifiesByShift = shifts.Where(s => s.StartTime <= startTs && endTs <= s.EndTime).Select(s => s.StylistId).ToHashSet();
        if (!qualifiesByShift.Any()) return null;

        // day off check
        var dayOff = await db.StylistDaysOff.AsNoTracking()
            .Where(d => d.OffDate >= dayStart && d.OffDate < dayEnd)
            .Select(d => d.StylistId).ToListAsync();
        // breaks and appointments on that day
        var breaks = await db.StylistBreaks.AsNoTracking()
            .Where(b => b.BreakDate >= dayStart && b.BreakDate < dayEnd)
            .ToListAsync();
        var appointments = await db.Appointments.AsNoTracking()
            .Where(a => a.AppointmentDate >= dayStart && a.AppointmentDate < dayEnd)
            .ToListAsync();

        var availableCandidates = qualifiesByShift
            .Where(id => !dayOff.Contains(id))
            .Where(id => !breaks.Any(b => b.StylistId == id && b.StartTime < endTs && startTs < b.EndTime))
            .Where(id => !appointments.Any(a => a.StylistId == id && a.StartTime < endTs && startTs < a.EndTime && a.Status != "Cancelled" && a.Status != "NoShow" && a.Status != "Rejected"))
            .ToList();

        if (!availableCandidates.Any()) return null;

        // If a specific stylist requested, preserve that preference if available.
        if (stylistId != AnyStylist)
        {
            return availableCandidates.Contains(stylistId)
                ? new StylistAssignment(stylistId, endTs.ToString(@"hh\:mm"))
                : null;
        }

        // Choose least-loaded stylist (count of active appointments on that date)
        var counts = await db.Appointments.AsNoTracking()
            .Where(a => a.AppointmentDate >= dayStart && a.AppointmentDate < dayEnd && availableCandidates.Contains(a.StylistId))
            .GroupBy(a => a.StylistId)
            .Select(g => new { StylistId = g.Key, Count = g.Count(a => a.Status != "Cancelled" && a.Status != "NoShow" && a.Status != "Rejected" && a.Status != "Completed") })
            .ToListAsync();

        // Ensure every candidate has an entry
        var candidateCounts = availableCandidates.Select(id =>
            (StylistId: id, Count: counts.FirstOrDefault(c => c.StylistId == id)?.Count ?? 0))
            .OrderBy(t => t.Count).ThenBy(t => t.StylistId).ToList();

        var chosen = candidateCounts.First();
        return new StylistAssignment(chosen.StylistId, endTs.ToString(@"hh\:mm"));
    }
}