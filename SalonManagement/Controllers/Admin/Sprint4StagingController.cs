using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalonManagement.Data;
using SalonManagement.Models;
using SalonManagement.Services;

namespace SalonManagement.Controllers.Admin;

[Authorize(Roles = UserRoles.Admin)]
public class Sprint4StagingController(ApplicationDbContext db, IWebHostEnvironment environment, StylistAvailabilityService salonClock) : Controller
{
    private static readonly string[] Steps =
    [
        "Đặt lịch khách hàng", "Email xác nhận", "Gán thợ và thông báo", "Check-in",
        "Ghi chú kỹ thuật", "Phiếu và mã giảm giá", "Thanh toán và đóng lịch",
        "Email đánh giá", "Gửi đánh giá", "Báo cáo và tổng quan"
    ];

    [HttpGet]
    public IActionResult Index() => IsStaging() ? View("Demo", Steps) : NotFound();

    [HttpGet]
    public async Task<IActionResult> Health()
    {
        if (!IsStaging()) return NotFound();
        var canConnect = await db.Database.CanConnectAsync();
        var checks = new Dictionary<string, int>();
        if (canConnect)
        {
            checks["Khách hàng"] = await db.Customers.CountAsync();
            checks["Thợ"] = await db.Stylists.CountAsync();
            checks["Dịch vụ"] = await db.Services.CountAsync();
            checks["Lịch hẹn"] = await db.Appointments.CountAsync();
            checks["Phiếu"] = await db.Invoices.CountAsync();
            checks["Mã giảm giá"] = await db.DiscountCoupons.CountAsync();
            checks["Đánh giá"] = await db.StylistReviews.CountAsync();
            checks["Email mô phỏng"] = await db.StagingEmailMessages.CountAsync();
        }
        return View("Health", new StagingHealthViewModel(
            Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "unknown",
            canConnect, "Log-only (không gửi email ra ngoài)", checks));
    }

    [HttpGet]
    public async Task<IActionResult> Emails()
    {
        if (!IsStaging()) return NotFound();
        var messages = await db.StagingEmailMessages.AsNoTracking()
            .OrderByDescending(item => item.CreatedAt).Take(200).ToListAsync();
        return View("Emails", messages);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetDemo()
    {
        if (!IsStaging()) return NotFound();
        var demoAppointments = await db.Appointments
            .Where(item => item.BookingReference.StartsWith("S4HIST") || item.BookingReference.StartsWith("S4DAY") || item.BookingReference == "S4NEW01" || item.Notes!.StartsWith("Sprint 4 demo status:"))
            .ToListAsync();
        var appointmentIds = demoAppointments.Select(item => item.AppointmentId).ToArray();
        var demoInvoices = await db.Invoices.Where(item => appointmentIds.Contains(item.AppointmentId)).ToListAsync();
        db.Invoices.RemoveRange(demoInvoices);
        await db.SaveChangesAsync();
        db.Appointments.RemoveRange(demoAppointments);
        var demoCustomers = await db.Customers.Where(item => item.Notes == "Sprint 4 synthetic demo customer" || item.Notes == "Sprint 4 new customer sample").ToListAsync();
        db.Customers.RemoveRange(demoCustomers);
        var unusedCoupons = await db.DiscountCoupons.Where(item => item.Code.StartsWith("S4-") && !item.Invoices.Any()).ToListAsync();
        db.DiscountCoupons.RemoveRange(unusedCoupons);
        db.StagingEmailMessages.RemoveRange(await db.StagingEmailMessages.ToListAsync());
        await db.SaveChangesAsync();
        await Sprint4DemoSeed.SeedAsync(db, salonClock.SalonNow.Date);
        TempData["Success"] = "Đã nạp lại dữ liệu mẫu Sprint 4 theo ngày hiện tại.";
        return RedirectToAction(nameof(Health));
    }

    [HttpGet]
    public async Task<IActionResult> Rehearsals(string? step)
    {
        if (!IsStaging()) return NotFound();
        var runs = await db.RehearsalRuns.AsNoTracking().Include(run => run.Issues)
            .OrderByDescending(run => run.PerformedAt).ToListAsync();
        ViewBag.Step = step;
        ViewBag.Account = User.Identity?.Name;
        return View("Rehearsals", runs);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateRun(string? notes, string? nextStep)
    {
        if (!IsStaging()) return NotFound();
        db.RehearsalRuns.Add(new RehearsalRun { PerformedBy = User.Identity?.Name ?? "Admin", Notes = notes?.Trim() });
        await db.SaveChangesAsync();
        return RedirectToAction(nameof(Rehearsals), new { step = nextStep });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddIssue(int runId, string? step, string? account, string? observation,
        string? severity, string? assignee)
    {
        if (!IsStaging()) return NotFound();
        if (string.IsNullOrWhiteSpace(step) || string.IsNullOrWhiteSpace(account) || string.IsNullOrWhiteSpace(observation) ||
            severity is not ("Blocker" or "Normal" or "Minor") || !await db.RehearsalRuns.AnyAsync(run => run.RehearsalRunId == runId))
        {
            TempData["Error"] = "Hãy nhập bước, tài khoản, hiện tượng và mức độ hợp lệ.";
            return RedirectToAction(nameof(Rehearsals));
        }
        db.RehearsalIssues.Add(new RehearsalIssue
        {
            RehearsalRunId = runId, Step = step.Trim(), Account = account.Trim(), Observation = observation.Trim(),
            Severity = severity, Assignee = assignee?.Trim(), Status = "New"
        });
        await db.SaveChangesAsync();
        return RedirectToAction(nameof(Rehearsals));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeIssueStatus(int id, string status)
    {
        if (!IsStaging()) return NotFound();
        var issue = await db.RehearsalIssues.FindAsync(id);
        if (issue is null) return NotFound();
        var allowed = issue.Status switch
        {
            "New" => status is "InProgress" or "Accepted",
            "InProgress" => status is "Fixed" or "Accepted",
            "Fixed" => status is "Retested" or "InProgress" or "Accepted",
            "Retested" => status == "Accepted",
            "Accepted" => status is "InProgress" or "Retested",
            _ => false
        };
        if (!allowed)
        {
            TempData["Error"] = "Chỉ xác nhận lại lỗi sau khi đã chuyển sang Đã sửa.";
            return RedirectToAction(nameof(Rehearsals));
        }
        issue.Status = status;
        issue.LastChangedBy = User.Identity?.Name;
        issue.LastChangedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return RedirectToAction(nameof(Rehearsals));
    }

    private bool IsStaging() => environment.IsStaging();
}

public sealed record StagingHealthViewModel(string Version, bool DatabaseConnected, string EmailMode, IReadOnlyDictionary<string, int> Counts);
