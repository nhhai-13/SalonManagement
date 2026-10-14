using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalonManagement.Data;
using SalonManagement.Models;
using SalonManagement.Models.ViewModels.Owner;
using SalonManagement.Services;

namespace SalonManagement.Controllers.Owner;

[Authorize(Roles = UserRoles.Owner)]
public class OwnerReportsController(ApplicationDbContext db) : Controller
{
    private static readonly string[] Categories = ["Ngày thường", "Thứ bảy", "Chủ nhật"];

    [HttpGet]
    public async Task<IActionResult> Index(DateTime? from, DateTime? to)
    {
        var end = (to ?? VietnamTime.Today).Date;
        var start = (from ?? end.AddDays(-29)).Date;
        if (end < start || (end - start).TotalDays > 364)
        {
            return View(new AppointmentAnalyticsViewModel
            {
                From = start, To = end,
                Error = "Khoảng ngày phải hợp lệ và tối đa 365 ngày."
            });
        }

        var data = await db.Appointments.AsNoTracking()
            .Where(item => item.AppointmentDate >= start && item.AppointmentDate < end.AddDays(1))
            .Select(item => new { item.AppointmentDate, item.StartTime, item.Status })
            .ToListAsync();

        var summaries = new List<DayTypeSummary>();
        var kinds = new[]
        {
            new { Name = Categories[0], Match = (Func<DayOfWeek, bool>)(day => day is not DayOfWeek.Saturday and not DayOfWeek.Sunday) },
            new { Name = Categories[1], Match = (Func<DayOfWeek, bool>)(day => day == DayOfWeek.Saturday) },
            new { Name = Categories[2], Match = (Func<DayOfWeek, bool>)(day => day == DayOfWeek.Sunday) }
        };
        foreach (var kind in kinds)
        {
            var days = Enumerable.Range(0, (end - start).Days + 1)
                .Select(offset => start.AddDays(offset)).Count(date => kind.Match(date.DayOfWeek));
            var group = data.Where(item => kind.Match(item.AppointmentDate.DayOfWeek)).ToList();
            var hourly = Enumerable.Range(9, 12).Select(hour => days == 0 ? 0d : group.Count(item => item.StartTime.Hours == hour) / (double)days).ToArray();
            var denominator = group.Count;
            summaries.Add(new DayTypeSummary(kind.Name, days, denominator,
                days == 0 ? 0 : Math.Round(denominator / (double)days, 2),
                denominator == 0 ? 0 : 100d * group.Count(item => item.Status == "NoShow") / denominator,
                denominator == 0 ? 0 : 100d * group.Count(item => item.Status is "Cancelled" or "Canceled") / denominator,
                hourly, hourly.Length == 0 ? 9 : 9 + Array.IndexOf(hourly, hourly.Max())));
        }

        return View(new AppointmentAnalyticsViewModel
        {
            From = start,
            To = end,
            TotalByHour = Enumerable.Range(9, 12).Select(hour => new HourlyCount(hour, data.Count(item => item.StartTime.Hours == hour))).ToList(),
            DayTypes = summaries,
            TotalAppointments = data.Count,
            NoShows = data.Count(item => item.Status == "NoShow"),
            Cancellations = data.Count(item => item.Status is "Cancelled" or "Canceled")
        });
    }
}
