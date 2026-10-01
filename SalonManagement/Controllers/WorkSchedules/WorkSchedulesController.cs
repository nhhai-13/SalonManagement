using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalonManagement.Data;
using SalonManagement.Models;
using SalonManagement.Models.ViewModels;

namespace SalonManagement.Controllers;

[Authorize(Roles = UserRoles.Owner)]
[Route("management/work-schedules")]
public sealed class WorkSchedulesController(ApplicationDbContext db) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(int? stylistId, DateTime? weekStart)
    {
        var start = StartOfWeek(weekStart ?? DateTime.Today);
        var stylists = await db.Stylists.AsNoTracking()
            .Where(stylist => stylist.IsActive)
            .OrderBy(stylist => stylist.FullName)
            .ToListAsync();

        var selectedStylistId = stylistId is not null && stylists.Any(s => s.StylistId == stylistId)
            ? stylistId
            : stylists.Select(s => (int?)s.StylistId).FirstOrDefault();

        var schedules = selectedStylistId is null
            ? []
            : await db.WorkSchedules.AsNoTracking()
                .Where(schedule => schedule.StylistId == selectedStylistId &&
                                   schedule.WorkDate >= start && schedule.WorkDate < start.AddDays(7))
                .OrderBy(schedule => schedule.WorkDate)
                .ThenBy(schedule => schedule.StartTime)
                .ToListAsync();

        return View(new WeeklyWorkScheduleViewModel
        {
            StylistId = selectedStylistId,
            WeekStart = start,
            Stylists = stylists,
            Schedules = schedules
        });
    }

    [HttpPost("")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateWorkScheduleViewModel model)
    {
        model.Notes = string.IsNullOrWhiteSpace(model.Notes) ? null : model.Notes.Trim();

        if (model.StartTime >= model.EndTime)
            ModelState.AddModelError(nameof(model.StartTime), "Giờ bắt đầu phải sớm hơn giờ kết thúc.");

        if (!await db.Stylists.AnyAsync(stylist => stylist.StylistId == model.StylistId && stylist.IsActive))
            ModelState.AddModelError(nameof(model.StylistId), "Thợ được chọn không tồn tại hoặc đã nghỉ việc.");

        if (!ModelState.IsValid)
        {
            TempData["Error"] = ModelState.Values.SelectMany(value => value.Errors)
                .Select(error => error.ErrorMessage).FirstOrDefault() ?? "Dữ liệu ca làm không hợp lệ.";
            return RedirectToAction(nameof(Index), new { stylistId = model.StylistId, weekStart = StartOfWeek(model.WorkDate) });
        }

        db.WorkSchedules.Add(new WorkSchedule
        {
            StylistId = model.StylistId,
            WorkDate = model.WorkDate.Date,
            StartTime = model.StartTime,
            EndTime = model.EndTime,
            Notes = model.Notes
        });
        await db.SaveChangesAsync();

        TempData["Success"] = "Đã thêm ca làm và cập nhật lịch tuần.";
        return RedirectToAction(nameof(Index), new { stylistId = model.StylistId, weekStart = StartOfWeek(model.WorkDate) });
    }

    internal static DateTime StartOfWeek(DateTime date)
    {
        var offset = ((int)date.DayOfWeek + 6) % 7;
        return date.Date.AddDays(-offset);
    }
}
