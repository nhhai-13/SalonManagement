using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;
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
    public async Task<IActionResult> Revenue(DateTime? from, DateTime? to)
    {
        var end = (to ?? VietnamTime.Today).Date;
        var start = (from ?? new DateTime(end.Year, end.Month, 1)).Date;
        if (!IsValidRange(start, end))
            return View(new OwnerRevenueViewModel { From = start, To = end });

        var (startUtc, endUtc) = UtcRange(start, end);
        var invoices = await db.Invoices.AsNoTracking()
            .Where(invoice => invoice.ClosedAt >= startUtc && invoice.ClosedAt < endUtc)
            .Include(invoice => invoice.Appointment).ThenInclude(appointment => appointment.Stylist)
            .Include(invoice => invoice.Lines).ThenInclude(line => line.AppointmentService).ThenInclude(item => item.Service)
            .Include(invoice => invoice.Lines).ThenInclude(line => line.AppointmentService).ThenInclude(item => item.Stylist)
            .ToListAsync();

        var stylistRows = invoices.SelectMany(invoice => invoice.Lines
                .GroupBy(line => line.AppointmentService.Stylist?.StylistId ?? invoice.Appointment.StylistId)
                .Select(group => new
                {
                    StylistId = group.Key,
                    StylistName = group.Select(line => line.AppointmentService.Stylist?.FullName)
                        .FirstOrDefault(name => !string.IsNullOrWhiteSpace(name)) ?? invoice.Appointment.Stylist.FullName,
                    InvoiceId = invoice.InvoiceId,
                    Amount = group.Sum(line => line.AppointmentService.CollectedAmount)
                }))
            .GroupBy(item => new { item.StylistId, item.StylistName })
            .Select(group => new StylistRevenueRow(group.Key.StylistName,
                group.Select(item => item.InvoiceId).Distinct().Count(),
                group.Sum(item => item.Amount),
                group.Select(item => item.InvoiceId).Distinct().Count() == 0 ? 0 :
                    group.Sum(item => item.Amount) / group.Select(item => item.InvoiceId).Distinct().Count()))
            .OrderByDescending(item => item.ActualRevenue).ToList();

        var serviceRows = invoices.SelectMany(invoice => invoice.Lines)
            .GroupBy(line => line.ServiceName)
            .Select(group => new ServiceRevenueRow(group.Key, group.Count(), group.Sum(line => line.AppointmentService.CollectedAmount)))
            .OrderByDescending(item => item.Amount).ToList();

        return View(new OwnerRevenueViewModel
        {
            From = start, To = end, ClosedInvoiceCount = invoices.Count,
            ActualRevenue = invoices.Sum(invoice => invoice.AdjustedTotal),
            ByStylist = stylistRows, ByService = serviceRows
        });
    }

    [HttpGet]
    public async Task<IActionResult> ExportRevenueCsv(DateTime? from, DateTime? to)
    {
        var end = (to ?? VietnamTime.Today).Date;
        var start = (from ?? new DateTime(end.Year, end.Month, 1)).Date;
        if (!IsValidRange(start, end)) return BadRequest("Khoảng ngày phải hợp lệ và tối đa 365 ngày.");

        var (startUtc, endUtc) = UtcRange(start, end);
        var invoices = await db.Invoices.AsNoTracking()
            .Where(invoice => invoice.ClosedAt >= startUtc && invoice.ClosedAt < endUtc)
            .Include(invoice => invoice.Appointment).ThenInclude(appointment => appointment.Stylist)
            .Include(invoice => invoice.Lines).ThenInclude(line => line.AppointmentService).ThenInclude(item => item.Stylist)
            .ToListAsync();
        var rows = new List<string> { "Nhóm,Tên,Số lượt,Doanh thu thực tế (VND),Giá trị trung bình (VND)" };
        foreach (var row in invoices.SelectMany(invoice => invoice.Lines
                     .GroupBy(line => line.AppointmentService.Stylist?.StylistId ?? invoice.Appointment.StylistId)
                     .Select(group => new { Name = group.Select(line => line.AppointmentService.Stylist?.FullName).FirstOrDefault(name => !string.IsNullOrWhiteSpace(name)) ?? invoice.Appointment.Stylist.FullName, InvoiceId = invoice.InvoiceId, Amount = group.Sum(line => line.AppointmentService.CollectedAmount) }))
                 .GroupBy(item => item.Name)
                 .Select(group => new { group.Key, Count = group.Select(item => item.InvoiceId).Distinct().Count(), Amount = group.Sum(item => item.Amount) }))
            rows.Add($"{Csv("Nhân viên")},{Csv(row.Key)},{row.Count},{row.Amount.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)},{(row.Count == 0 ? 0 : row.Amount / row.Count).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}");
        foreach (var row in invoices.SelectMany(invoice => invoice.Lines).GroupBy(line => line.ServiceName)
                     .Select(group => new { group.Key, Count = group.Count(), Amount = group.Sum(line => line.AppointmentService.CollectedAmount) }).OrderByDescending(row => row.Amount))
            rows.Add($"{Csv("Dịch vụ")},{Csv(row.Key)},{row.Count},{row.Amount.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)},");
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(string.Join("\r\n", rows))).ToArray();
        return File(bytes, "text/csv; charset=utf-8", $"bao-cao-doanh-thu-{start:yyyyMMdd}-{end:yyyyMMdd}.csv");
    }

    [HttpGet]
    public async Task<IActionResult> Today()
    {
        var today = VietnamTime.Today;
        var appointments = await db.Appointments.AsNoTracking()
            .Where(item => item.AppointmentDate >= today && item.AppointmentDate < today.AddDays(1))
            .Select(item => item.Status).ToListAsync();
        var statuses = new Dictionary<string, int>
        {
            ["Pending"] = appointments.Count(status => status is "Pending" or "PendingConfirmation"),
            ["Confirmed"] = appointments.Count(status => status == "Confirmed"),
            ["Arrived"] = appointments.Count(status => status == "Arrived"),
            ["Completed"] = appointments.Count(status => status is "Completed" or "Closed"),
            ["Cancelled"] = appointments.Count(status => status is "Cancelled" or "Canceled"),
            ["NoShow"] = appointments.Count(status => status == "NoShow")
        };
        var (startUtc, endUtc) = UtcRange(today, today);
        var revenue = await db.Payments.AsNoTracking()
            .Where(payment => payment.PaymentStatus == "Paid" && payment.PaidAt >= startUtc && payment.PaidAt < endUtc)
            .SumAsync(payment => (decimal?)payment.Amount) ?? 0;
        var shifts = await db.WorkSchedules.AsNoTracking()
            .Where(schedule => schedule.WorkDate == today && schedule.Status == "Working")
            .OrderBy(schedule => schedule.StartTime)
            .Select(schedule => new WorkingStylistRow(schedule.Stylist.FullName, schedule.StartTime, schedule.EndTime))
            .ToListAsync();
        return View(new OwnerTodayDashboardViewModel { Date = today, StatusCounts = statuses, CollectedRevenue = revenue, WorkingStylists = shifts });
    }

    private static bool IsValidRange(DateTime start, DateTime end) => end >= start && (end - start).TotalDays <= 364;
    private static (DateTime Start, DateTime End) UtcRange(DateTime start, DateTime end) =>
        (DateTime.SpecifyKind(start.AddHours(-7), DateTimeKind.Utc), DateTime.SpecifyKind(end.AddDays(1).AddHours(-7), DateTimeKind.Utc));
    private static string Csv(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";

    [HttpGet]
    public async Task<IActionResult> Index(DateTime? from, DateTime? to)
    {
        var end = (to ?? VietnamTime.Today).Date;
        var start = (from ?? new DateTime(end.Year, end.Month, 1)).Date;
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
        var ratings = await db.StylistReviews.AsNoTracking()
            .Where(review => review.Rating.HasValue)
            .Select(review => review.Rating!.Value)
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
            Cancellations = data.Count(item => item.Status is "Cancelled" or "Canceled"),
            ReviewCount = ratings.Count,
            AverageRating = ratings.Count >= 5 ? ratings.Average() : null
        });
    }
}
