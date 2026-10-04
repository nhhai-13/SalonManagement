using System.Text.Json;
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

        var configuredHours = await db.BusinessHours.AsNoTracking()
            .ToDictionaryAsync(hours => hours.DayOfWeek);
        var businessHours = Enum.GetValues<DayOfWeek>().ToDictionary(day => day,
            day => configuredHours.GetValueOrDefault(day) ?? new BusinessHour
            {
                DayOfWeek = day,
                OpensAt = new TimeOnly(8, 0),
                ClosesAt = new TimeOnly(17, 0),
                TimeZoneId = BusinessHour.SalonTimeZone
            });

        return View(new WeeklyWorkScheduleViewModel
        {
            StylistId = selectedStylistId,
            WeekStart = start,
            Stylists = stylists,
            Schedules = schedules,
            BusinessHours = businessHours
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

        var conflictingShift = await FindConflictingShiftAsync(
            model.StylistId, model.WorkDate.Date, model.StartTime, model.EndTime);
        if (conflictingShift is not null)
            ModelState.AddModelError(string.Empty, ConflictMessage(conflictingShift));

        var businessHoursError = await ValidateBusinessHoursAsync(model.WorkDate.Date, model.StartTime, model.EndTime);
        if (businessHoursError is not null)
            ModelState.AddModelError(string.Empty, businessHoursError);

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

    [HttpPost("{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, EditWorkScheduleViewModel model)
    {
        var schedule = await db.WorkSchedules.SingleOrDefaultAsync(item => item.WorkScheduleId == id);
        if (schedule is null)
            return NotFound();

        model.Notes = string.IsNullOrWhiteSpace(model.Notes) ? null : model.Notes.Trim();
        if (model.WorkScheduleId != id)
            ModelState.AddModelError(nameof(model.WorkScheduleId), "Ca làm không hợp lệ.");
        if (model.StartTime >= model.EndTime)
            ModelState.AddModelError(nameof(model.StartTime), "Giờ bắt đầu phải sớm hơn giờ kết thúc.");

        var conflictingShift = await FindConflictingShiftAsync(
            schedule.StylistId, schedule.WorkDate.Date, model.StartTime, model.EndTime, schedule.WorkScheduleId);
        if (conflictingShift is not null)
            ModelState.AddModelError(string.Empty, ConflictMessage(conflictingShift));

        var businessHoursError = await ValidateBusinessHoursAsync(schedule.WorkDate.Date, model.StartTime, model.EndTime);
        if (businessHoursError is not null)
            ModelState.AddModelError(string.Empty, businessHoursError);

        if (!ModelState.IsValid)
        {
            TempData["Error"] = ModelState.Values.SelectMany(value => value.Errors)
                .Select(error => error.ErrorMessage).FirstOrDefault() ?? "Dữ liệu ca làm không hợp lệ.";
            return RedirectToAction(nameof(Index), new { stylistId = schedule.StylistId, weekStart = StartOfWeek(schedule.WorkDate) });
        }

        schedule.StartTime = model.StartTime;
        schedule.EndTime = model.EndTime;
        schedule.Notes = model.Notes;
        await db.SaveChangesAsync();

        TempData["Success"] = "Đã cập nhật ca làm và lịch tuần.";
        return RedirectToAction(nameof(Index), new { stylistId = schedule.StylistId, weekStart = StartOfWeek(schedule.WorkDate) });
    }

    [HttpGet("copy-preview")]
    public async Task<ActionResult<CopyWeekPreviewResponse>> CopyPreview(int stylistId, DateTime weekStart)
    {
        var sourceStart = StartOfWeek(weekStart);
        var sourceDates = await db.WorkSchedules.AsNoTracking()
            .Where(schedule => schedule.StylistId == stylistId &&
                               schedule.WorkDate >= sourceStart && schedule.WorkDate < sourceStart.AddDays(7))
            .Select(schedule => schedule.WorkDate.Date)
            .Distinct()
            .ToListAsync();

        var targetStart = sourceStart.AddDays(7);
        var targetDates = sourceDates.Select(date => date.AddDays(7)).ToHashSet();
        var targetWeekShifts = await db.WorkSchedules.AsNoTracking()
            .Where(schedule => schedule.StylistId == stylistId &&
                               schedule.WorkDate >= targetStart && schedule.WorkDate < targetStart.AddDays(7))
            .ToListAsync();
        var conflicts = targetWeekShifts
            .Where(schedule => targetDates.Contains(schedule.WorkDate.Date))
            .GroupBy(schedule => schedule.WorkDate.Date)
            .Select(group => new CopyWeekConflictDay(group.Key, group.Count()))
            .OrderBy(day => day.Date)
            .ToList();

        return Ok(new CopyWeekPreviewResponse(targetStart, conflicts));
    }

    [HttpPost("copy-week")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CopyWeek(CopyWorkWeekViewModel model)
    {
        var sourceStart = StartOfWeek(model.SourceWeekStart);
        if (!await db.Stylists.AnyAsync(stylist => stylist.StylistId == model.StylistId && stylist.IsActive))
        {
            TempData["Error"] = "Thợ được chọn không tồn tại hoặc đã nghỉ việc.";
            return RedirectToAction(nameof(Index), new { stylistId = model.StylistId, weekStart = sourceStart });
        }

        var sourceShifts = await db.WorkSchedules.AsNoTracking()
            .Where(schedule => schedule.StylistId == model.StylistId &&
                               schedule.WorkDate >= sourceStart && schedule.WorkDate < sourceStart.AddDays(7))
            .OrderBy(schedule => schedule.WorkDate)
            .ThenBy(schedule => schedule.StartTime)
            .ToListAsync();

        if (sourceShifts.Count == 0)
        {
            TempData["Error"] = "Tuần hiện tại chưa có ca làm để sao chép.";
            return RedirectToAction(nameof(Index), new { stylistId = model.StylistId, weekStart = sourceStart });
        }

        var sourceDays = sourceShifts.GroupBy(schedule => schedule.WorkDate.Date).ToList();
        var targetStart = sourceStart.AddDays(7);
        var targetDates = sourceDays.Select(group => group.Key.AddDays(7)).ToHashSet();
        var targetShifts = (await db.WorkSchedules
            .Where(schedule => schedule.StylistId == model.StylistId &&
                               schedule.WorkDate >= targetStart && schedule.WorkDate < targetStart.AddDays(7))
            .ToListAsync())
            .Where(schedule => targetDates.Contains(schedule.WorkDate.Date))
            .ToList();

        var copiedCount = 0;
        var overwrittenDays = 0;
        var skippedDays = 0;
        foreach (var sourceDay in sourceDays)
        {
            var targetDate = sourceDay.Key.AddDays(7);
            var existingForDay = targetShifts.Where(schedule => schedule.WorkDate.Date == targetDate).ToList();
            if (existingForDay.Count > 0)
            {
                if (model.ConflictResolution == CopyConflictResolution.Skip)
                {
                    skippedDays++;
                    continue;
                }

                db.WorkSchedules.RemoveRange(existingForDay);
                overwrittenDays++;
            }

            foreach (var sourceShift in sourceDay)
            {
                db.WorkSchedules.Add(new WorkSchedule
                {
                    StylistId = model.StylistId,
                    WorkDate = targetDate,
                    StartTime = sourceShift.StartTime,
                    EndTime = sourceShift.EndTime,
                    Notes = sourceShift.Notes
                });
                copiedCount++;
            }
        }

        await db.SaveChangesAsync();
        TempData["Success"] = $"Đã sao chép {copiedCount} ca; ghi đè {overwrittenDays} ngày; bỏ qua {skippedDays} ngày.";
        return RedirectToAction(nameof(Index), new { stylistId = model.StylistId, weekStart = sourceStart.AddDays(7) });
    }

    [HttpGet("{id:int}/delete-preview")]
    public async Task<ActionResult<DeleteWorkSchedulePreviewResponse>> DeletePreview(int id)
    {
        var schedule = await db.WorkSchedules.AsNoTracking()
            .SingleOrDefaultAsync(item => item.WorkScheduleId == id);
        if (schedule is null)
            return NotFound();

        return Ok(new DeleteWorkSchedulePreviewResponse(id, await GetRelatedAppointmentsAsync(schedule)));
    }

    [HttpPost("{id:int}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var schedule = await db.WorkSchedules.SingleOrDefaultAsync(item => item.WorkScheduleId == id);
        if (schedule is null)
            return NotFound();

        var relatedAppointments = await GetRelatedAppointmentsAsync(schedule);
        if (relatedAppointments.Count > 0)
        {
            TempData["Error"] = $"Không thể xóa ca vì có {relatedAppointments.Count} lịch hẹn liên quan.";
            TempData["BlockedAppointments"] = JsonSerializer.Serialize(relatedAppointments);
            return RedirectToAction(nameof(Index), new { stylistId = schedule.StylistId, weekStart = StartOfWeek(schedule.WorkDate) });
        }

        db.WorkSchedules.Remove(schedule);
        await db.SaveChangesAsync();
        TempData["Success"] = "Đã xóa ca làm.";
        return RedirectToAction(nameof(Index), new { stylistId = schedule.StylistId, weekStart = StartOfWeek(schedule.WorkDate) });
    }

    private async Task<List<BlockedAppointmentViewModel>> GetRelatedAppointmentsAsync(WorkSchedule schedule)
    {
        var appointments = await db.Appointments.AsNoTracking()
            .Include(appointment => appointment.Customer)
            .Include(appointment => appointment.AppointmentServices)
            .ThenInclude(item => item.Service)
            .Where(appointment => appointment.StylistId == schedule.StylistId &&
                                  appointment.AppointmentDate == schedule.WorkDate.Date)
            .ToListAsync();

        return appointments
            .Where(appointment => appointment.StartTime < schedule.EndTime && schedule.StartTime < appointment.EndTime)
            .OrderBy(appointment => appointment.StartTime)
            .Select(appointment => new BlockedAppointmentViewModel(
                appointment.AppointmentId,
                appointment.Customer.FullName,
                appointment.StartTime,
                appointment.EndTime,
                appointment.AppointmentServices.Select(item => item.Service.ServiceName).OrderBy(name => name).ToList()))
            .ToList();
    }

    private async Task<WorkSchedule?> FindConflictingShiftAsync(
        int stylistId, DateTime workDate, TimeSpan startTime, TimeSpan endTime, int? excludedScheduleId = null) =>
        await db.WorkSchedules.AsNoTracking()
            .Where(schedule => schedule.StylistId == stylistId &&
                               schedule.WorkDate == workDate &&
                               (!excludedScheduleId.HasValue || schedule.WorkScheduleId != excludedScheduleId) &&
                               schedule.StartTime < endTime && startTime < schedule.EndTime)
            .OrderBy(schedule => schedule.StartTime)
            .FirstOrDefaultAsync();

    private static string ConflictMessage(WorkSchedule schedule) =>
        $"Ca làm bị chồng lấn với ca {schedule.StartTime:hh\\:mm}–{schedule.EndTime:hh\\:mm} ngày {schedule.WorkDate:dd/MM/yyyy}.";

    private async Task<string?> ValidateBusinessHoursAsync(DateTime workDate, TimeSpan startTime, TimeSpan endTime)
    {
        var hours = await db.BusinessHours.AsNoTracking()
            .SingleOrDefaultAsync(item => item.DayOfWeek == workDate.DayOfWeek);

        if (hours?.IsClosed == true)
            return $"Tiệm không hoạt động vào ngày {workDate:dd/MM/yyyy}.";

        var opensAt = (hours?.OpensAt ?? new TimeOnly(8, 0)).ToTimeSpan();
        var closesAt = (hours?.ClosesAt ?? new TimeOnly(17, 0)).ToTimeSpan();
        return startTime < opensAt || endTime > closesAt
            ? $"Ca làm phải trong giờ hoạt động {opensAt:hh\\:mm}–{closesAt:hh\\:mm}."
            : null;
    }

    internal static DateTime StartOfWeek(DateTime date)
    {
        var offset = ((int)date.DayOfWeek + 6) % 7;
        return date.Date.AddDays(-offset);
    }
}
