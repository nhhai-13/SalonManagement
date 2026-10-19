using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalonManagement.Data;
using SalonManagement.Models;

namespace SalonManagement.Controllers.Owner;

[Authorize(Roles = UserRoles.Owner)]
public sealed class PeakHoursReportPageController : Controller
{
    [HttpGet("owner/peak-hours-report")]
    public IActionResult Index() => View("~/Views/PeakHoursReport/Index.cshtml");
}

[Authorize(Roles = UserRoles.Owner)]
[ApiController]
[Route("api/owner/peak-hours-report")]
public sealed class PeakHoursReportController(ApplicationDbContext db) : ControllerBase
{
    private static readonly int[] SlotHours = [8, 10, 12, 14, 16, 18, 20];

    [HttpGet]
    public async Task<ActionResult<PeakHoursReportSnapshot>> Get(DateTime? from, DateTime? to)
    {
        var range = NormalizeRange(from, to);
        if (range.Error is not null) return BadRequest(new { error = range.Error });
        var appointments = await db.Appointments.AsNoTracking()
            .Where(item => item.AppointmentDate >= range.From && item.AppointmentDate < range.To.AddDays(1))
            .ToListAsync();
        var weekdayDays = 0;
        var weekendDays = 0;
        for (var date = range.From; date <= range.To; date = date.AddDays(1))
            if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) weekendDays++; else weekdayDays++;

        var slots = SlotHours.Select(hour =>
        {
            var weekday = appointments.Count(item => IsWeekday(item.AppointmentDate) && SlotFor(item.StartTime) == hour);
            var weekend = appointments.Count(item => !IsWeekday(item.AppointmentDate) && SlotFor(item.StartTime) == hour);
            return new PeakHourSlot($"{hour:00}:00", weekdayDays == 0 ? 0 : Math.Round((decimal)weekday / weekdayDays, 2), weekendDays == 0 ? 0 : Math.Round((decimal)weekend / weekendDays, 2));
        }).ToList();
        var total = appointments.Count;
        var noShows = appointments.Count(item => item.Status == "NoShow");
        var cancelled = appointments.Count(item => item.Status is "Cancelled" or "Rejected");
        var busiest = slots.OrderByDescending(item => item.WeekdayAverage + item.WeekendAverage).First();
        var ratio = busiest.WeekdayAverage == 0 ? 0 : Math.Round(busiest.WeekendAverage / busiest.WeekdayAverage, 1);
        var recommendation = total == 0
            ? "Chưa có lịch hẹn trong khoảng thời gian này nên chưa thể gợi ý xếp ca."
            : busiest.WeekendAverage > busiest.WeekdayAverage
                ? busiest.WeekdayAverage == 0
                    ? $"Khung {busiest.Time} chưa ghi nhận khách ngày thường nhưng có khách cuối tuần. Nên theo dõi nhu cầu cuối tuần khi xếp ca."
                    : $"Khung {busiest.Time} có lượng khách cuối tuần cao hơn ngày thường {ratio:0.0} lần. Nên tăng thợ cho ca chiều Thứ 7 và Chủ Nhật."
                : $"Khung {busiest.Time} đang có lượng khách cao nhất. Nên ưu tiên phân ca đủ thợ ở khung này.";
        return Ok(new PeakHoursReportSnapshot(range.From, range.To, total, Rate(noShows, total), Rate(cancelled, total), slots, recommendation));
    }

    private static (DateTime From, DateTime To, string? Error) NormalizeRange(DateTime? from, DateTime? to)
    {
        var today = DateTime.Today;
        var start = (from ?? new DateTime(today.Year, today.Month, 1)).Date;
        var end = (to ?? today).Date;
        if (end < start) return (start, end, "Ngày kết thúc phải sau hoặc bằng ngày bắt đầu.");
        if ((end - start).TotalDays + 1 > 365) return (start, end, "Khoảng thời gian xem báo cáo tối đa là 365 ngày.");
        return (start, end, null);
    }

    private static bool IsWeekday(DateTime date) => date.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday;
    private static int SlotFor(TimeSpan startTime)
    {
        var hour = startTime.Hours;
        if (hour <= 8) return 8;
        if (hour >= 20) return 20;
        return 8 + ((hour - 8) / 2 * 2);
    }
    private static decimal Rate(int count, int total) => total == 0 ? 0 : Math.Round((decimal)count / total * 100, 1);
}

public sealed record PeakHoursReportSnapshot(DateTime From, DateTime To, int TotalAppointments, decimal NoShowRate, decimal CancellationRate, IReadOnlyList<PeakHourSlot> Slots, string Recommendation);
public sealed record PeakHourSlot(string Time, decimal WeekdayAverage, decimal WeekendAverage);
