using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalonManagement.Data;
using SalonManagement.Models;

namespace SalonManagement.Controllers.Owner;

[Authorize(Roles = UserRoles.Owner)]
public sealed class RevenueReportPageController : Controller
{
    [HttpGet("owner/revenue-report")]
    public IActionResult Index() => View("~/Views/RevenueReport/Index.cshtml");
}

[Authorize(Roles = UserRoles.Owner)]
[ApiController]
[Route("api/owner/revenue-report")]
public sealed class RevenueReportController(ApplicationDbContext db) : ControllerBase
{
    private const int MaximumRangeDays = 365;

    [HttpGet]
    public async Task<ActionResult<RevenueReportSnapshot>> Get(DateTime? from, DateTime? to)
    {
        var range = NormalizeRange(from, to);
        if (range.Error is not null) return BadRequest(new { error = range.Error });
        return Ok(await BuildAsync(range.From, range.To));
    }

    [HttpGet("export")]
    public async Task<IActionResult> Export(DateTime? from, DateTime? to)
    {
        var range = NormalizeRange(from, to);
        if (range.Error is not null) return BadRequest(new { error = range.Error });
        var report = await BuildAsync(range.From, range.To);
        var csv = new StringBuilder();
        csv.AppendLine("BÁO CÁO DOANH THU THEO THỢ VÀ DỊCH VỤ (S4-07)");
        csv.AppendLine($"Khoảng ngày;{range.From:dd/MM/yyyy};{range.To:dd/MM/yyyy}");
        csv.AppendLine($"Tổng doanh thu thực thu;{report.TotalRevenue.ToString(CultureInfo.InvariantCulture)}");
        csv.AppendLine($"Tổng lượt hoàn tất;{report.TotalCompleted}");
        csv.AppendLine($"Doanh thu trung bình / lượt;{report.AverageRevenue.ToString(CultureInfo.InvariantCulture)}");
        csv.AppendLine();
        csv.AppendLine("DOANH THU THEO THỢ");
        csv.AppendLine("Thợ / Kỹ thuật viên;Số lượt hoàn tất;Tổng doanh thu thực thu;Doanh thu trung bình mỗi lượt");
        foreach (var row in report.ByStylist)
            csv.AppendLine($"{Escape(row.Name)};{row.CompletedCount};{row.Revenue.ToString(CultureInfo.InvariantCulture)};{row.AverageRevenue.ToString(CultureInfo.InvariantCulture)}");
        csv.AppendLine();
        csv.AppendLine("DOANH THU THEO DỊCH VỤ");
        csv.AppendLine("Tên dịch vụ;Số lượt thực hiện;Tổng tiền;Tỷ trọng (%)");
        foreach (var row in report.ByService)
            csv.AppendLine($"{Escape(row.Name)};{row.CompletedCount};{row.Revenue.ToString(CultureInfo.InvariantCulture)};{row.SharePercent.ToString("0.##", CultureInfo.InvariantCulture)}");
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
        return File(bytes, "text/csv; charset=utf-8", $"bao-cao-doanh-thu-{range.From:yyyyMMdd}-{range.To:yyyyMMdd}.csv");
    }

    private async Task<RevenueReportSnapshot> BuildAsync(DateTime from, DateTime to)
    {
        var until = to.AddDays(1);
        // A payment is the financial source of truth.  Draft/unpaid invoices never enter this report.
        var payments = await db.Payments.AsNoTracking()
            .Include(item => item.Invoice).ThenInclude(item => item.Appointment).ThenInclude(item => item.Stylist)
            .Include(item => item.Invoice).ThenInclude(item => item.Appointment).ThenInclude(item => item.AppointmentServices).ThenInclude(item => item.Service)
            .Where(item => item.PaymentStatus == "Paid" && item.PaidAt != null && item.PaidAt >= from && item.PaidAt < until && item.Invoice.Status == "Paid")
            .ToListAsync();

        var total = payments.Sum(item => item.Amount);
        var byStylist = payments.GroupBy(item => item.Invoice.Appointment.Stylist?.FullName ?? "Chưa phân công")
            .Select(group => new RevenueByStylistRow(group.Key, group.Count(), group.Sum(item => item.Amount)))
            .OrderByDescending(item => item.Revenue).ThenBy(item => item.Name).ToList();

        var serviceRows = new List<(string Name, decimal Revenue)>();
        foreach (var payment in payments)
        {
            var lines = payment.Invoice.Appointment.AppointmentServices.ToList();
            var baseTotal = lines.Sum(line => line.Price);
            if (lines.Count == 0) continue;
            decimal allocated = 0;
            for (var index = 0; index < lines.Count; index++)
            {
                var amount = index == lines.Count - 1
                    ? payment.Amount - allocated
                    : baseTotal > 0 ? Math.Round(payment.Amount * lines[index].Price / baseTotal, 2, MidpointRounding.AwayFromZero) : 0;
                allocated += amount;
                serviceRows.Add((lines[index].Service.ServiceName, amount));
            }
        }
        var byService = serviceRows.GroupBy(item => item.Name)
            .Select(group => new RevenueByServiceRow(group.Key, group.Count(), group.Sum(item => item.Revenue), total == 0 ? 0 : Math.Round(group.Sum(item => item.Revenue) / total * 100, 2)))
            .OrderByDescending(item => item.Revenue).ThenBy(item => item.Name).ToList();

        return new RevenueReportSnapshot(from, to, total, payments.Count, payments.Count == 0 ? 0 : Math.Round(total / payments.Count, 2), byStylist, byService);
    }

    private static (DateTime From, DateTime To, string? Error) NormalizeRange(DateTime? from, DateTime? to)
    {
        var today = DateTime.Today;
        var start = (from ?? new DateTime(today.Year, today.Month, 1)).Date;
        var end = (to ?? today).Date;
        if (end < start) return (start, end, "Ngày kết thúc phải sau hoặc bằng ngày bắt đầu.");
        if ((end - start).TotalDays + 1 > MaximumRangeDays) return (start, end, "Khoảng thời gian xem báo cáo tối đa là 365 ngày.");
        return (start, end, null);
    }

    private static string Escape(string value)
    {
        var formulaCandidate = value.TrimStart(' ', '\t', '\r', '\n');
        if (formulaCandidate.Length > 0 && formulaCandidate[0] is '=' or '+' or '-' or '@')
            value = $"'{value}";

        return value.Contains(';') || value.Contains('"') || value.Contains('\n') || value.Contains('\r')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
    }
}

public sealed record RevenueReportSnapshot(DateTime From, DateTime To, decimal TotalRevenue, int TotalCompleted, decimal AverageRevenue, IReadOnlyList<RevenueByStylistRow> ByStylist, IReadOnlyList<RevenueByServiceRow> ByService);
public sealed record RevenueByStylistRow(string Name, int CompletedCount, decimal Revenue)
{
    public decimal AverageRevenue => CompletedCount == 0 ? 0 : Math.Round(Revenue / CompletedCount, 2);
}
public sealed record RevenueByServiceRow(string Name, int CompletedCount, decimal Revenue, decimal SharePercent);
