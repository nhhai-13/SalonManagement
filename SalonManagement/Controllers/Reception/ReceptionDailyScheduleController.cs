using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using SalonManagement.Data;
using SalonManagement.Models;
using SalonManagement.Services;

namespace SalonManagement.Controllers.Reception;

[Authorize(Roles = UserRoles.Receptionist + "," + UserRoles.Owner)]
[Route("reception/daily-schedule")]
public sealed class ReceptionDailyScheduleController(ApplicationDbContext db, AppointmentLifecycleService lifecycle, TimeProvider timeProvider) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(DateTime? date)
    {
        var selected = (date ?? DateTime.Today).Date;
        var hours = await db.BusinessHours.AsNoTracking().SingleOrDefaultAsync(item => item.DayOfWeek == selected.DayOfWeek);
        var opening = hours is { IsClosed: false, OpensAt: { } opensAt } ? opensAt.ToTimeSpan() : TimeSpan.FromHours(8);
        var closing = hours is { IsClosed: false, ClosesAt: { } closesAt } ? closesAt.ToTimeSpan() : TimeSpan.FromHours(20);
        var stylists = await db.Stylists.AsNoTracking().Where(item => item.IsActive).OrderBy(item => item.FullName).ToListAsync();
        var schedules = await db.WorkSchedules.AsNoTracking()
            .Where(item => item.WorkDate == selected && item.Status == "Working")
            .ToListAsync();
        // SQLite does not translate ORDER BY for TimeSpan. Load the appointments for
        // the selected day first, then order the small in-memory list by start time.
        var appointments = (await db.Appointments.AsNoTracking()
            .Include(item => item.Customer)
            .Include(item => item.AppointmentServices).ThenInclude(item => item.Service)
            .Where(item => item.AppointmentDate == selected && item.Status != "Cancelled" && item.Status != "Rejected")
            .ToListAsync())
            .OrderBy(item => item.StartTime)
            .ThenBy(item => item.EndTime)
            .ThenBy(item => item.AppointmentId)
            .ToList();
        var now = SalonClock.GetLocalNow(timeProvider);

        return View(new ReceptionDailyScheduleViewModel
        {
            Date = selected,
            OpeningTime = opening,
            ClosingTime = closing,
            Stylists = stylists.Select(stylist => new ReceptionScheduleStylistColumn
            {
                StylistId = stylist.StylistId,
                StylistName = stylist.FullName,
                WorkingRanges = schedules.Where(item => item.StylistId == stylist.StylistId)
                    .Select(item => new ReceptionScheduleWorkingRange { StartTime = item.StartTime, EndTime = item.EndTime }).ToList(),
                Appointments = appointments.Where(item => item.StylistId == stylist.StylistId)
                    .Select(item => new ReceptionScheduleAppointment
                    {
                        AppointmentId = item.AppointmentId,
                        CustomerId = item.CustomerId,
                        CustomerName = item.CustomerNameOverride ?? item.Customer.FullName,
                        Services = string.Join(", ", item.AppointmentServices.Select(service => service.Service.ServiceName)),
                        Status = item.Status,
                        StartTime = item.StartTime,
                        EndTime = item.EndTime,
                        IsLate = item.IsLate,
                        LateMinutes = item.LateMinutes,
                        NoShowAt = item.NoShowAt,
                        NoShowByUserId = item.NoShowByUserId,
                        NoShowAvailableAt = item.AppointmentDate.Date.Add(item.StartTime).AddMinutes(30),
                        CanMarkNoShow = item.Status == "Confirmed" && now >= item.AppointmentDate.Date.Add(item.StartTime).AddMinutes(30),
                        UndoNoShowDeadline = item.NoShowAt?.AddMinutes(15),
                        CanUndoNoShow = item.Status == "NoShow" && item.NoShowAt.HasValue && now <= item.NoShowAt.Value.AddMinutes(15)
                    }).ToList()
            }).ToList()
        });
    }

    [HttpPost("{appointmentId:int}/check-in")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CheckIn(int appointmentId, DateTime date)
        => await ExecuteAndReturn(appointmentId, date, lifecycle.CheckInAsync(appointmentId), "Đã check-in khách.");

    [HttpPost("{appointmentId:int}/no-show")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkNoShow(int appointmentId, DateTime date)
        => await ExecuteAndReturn(appointmentId, date, lifecycle.MarkNoShowAsync(appointmentId, CurrentUserId()), "Đã đánh dấu khách không đến. Khung giờ còn lại đã được mở lại.");

    [HttpPost("{appointmentId:int}/undo-no-show")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UndoNoShow(int appointmentId, DateTime date)
        => await ExecuteAndReturn(appointmentId, date, lifecycle.UndoNoShowAsync(appointmentId), "Đã hoàn tác trạng thái không đến.");

    private string? CurrentUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);

    private async Task<IActionResult> ExecuteAndReturn(int appointmentId, DateTime date, Task<AppointmentLifecycleResult> operation, string success)
    {
        var result = await operation;
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded ? success : result.Error;
        return RedirectToAction(nameof(Index), new { date = date.ToString("yyyy-MM-dd") });
    }
}
