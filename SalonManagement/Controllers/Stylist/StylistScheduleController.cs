using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalonManagement.Data;
using SalonManagement.Models;
using SalonManagement.Models.ViewModels;

namespace SalonManagement.Controllers.StylistPortal;

[Authorize(Roles = UserRoles.Stylist)]
[Route("stylist/schedule")]
public sealed class StylistScheduleController(ApplicationDbContext db) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(DateTime? date)
    {
        var email = CurrentUserEmail();
        var stylist = await db.Stylists.AsNoTracking().SingleOrDefaultAsync(item => item.Email == email && item.IsActive);
        if (stylist is null) return Forbid();
        var selected = (date ?? DateTime.Today).Date;
        // SQLite cannot sort TimeSpan values in SQL.  Load the stylist's small
        // daily agenda first, then order it in memory.
        var appointments = (await db.Appointments.AsNoTracking().Include(item => item.Customer).Include(item => item.AppointmentServices).ThenInclude(item => item.Service)
            .Where(item => item.StylistId == stylist.StylistId && item.AppointmentDate == selected)
            .ToListAsync())
            .OrderBy(item => item.StartTime)
            .ThenBy(item => item.EndTime)
            .ToList();
        return View((stylist.FullName, selected, (IReadOnlyList<Appointment>)appointments));
    }

    [HttpGet("~/stylist/work-schedule")]
    public async Task<IActionResult> WorkSchedule(DateTime? date)
    {
        var stylist = await GetCurrentStylistAsync();
        if (stylist is null) return Forbid();

        var selected = (date ?? DateTime.Today).Date;
        var shifts = (await db.WorkSchedules.AsNoTracking()
                .Where(item => item.StylistId == stylist.StylistId && item.WorkDate == selected && item.Status == "Working")
                .ToListAsync())
            .OrderBy(item => item.StartTime)
            .ThenBy(item => item.EndTime)
            .ToList();

        return View(new StylistWorkScheduleViewModel
        {
            StylistName = stylist.FullName,
            Date = selected,
            Shifts = shifts
        });
    }

    [HttpGet("~/stylist/service-history")]
    public async Task<IActionResult> ServiceHistory()
    {
        var stylist = await GetCurrentStylistAsync();
        if (stylist is null) return Forbid();

        var appointments = (await db.Appointments.AsNoTracking()
                .Include(item => item.Customer)
                .Include(item => item.AppointmentServices).ThenInclude(item => item.Service)
                .Where(item => item.StylistId == stylist.StylistId && item.Status == "Completed")
                .ToListAsync())
            .OrderByDescending(item => item.AppointmentDate)
            .ThenByDescending(item => item.StartTime)
            .Take(50)
            .ToList();

        return View(new StylistServiceHistoryViewModel
        {
            StylistName = stylist.FullName,
            Appointments = appointments
        });
    }

    [HttpGet("~/stylist/week-schedule")]
    public async Task<IActionResult> WeekSchedule(DateTime? weekStart)
    {
        var stylist = await GetCurrentStylistAsync();
        if (stylist is null) return Forbid();
        var selected = (weekStart ?? DateTime.Today).Date;
        var monday = selected.AddDays(-((7 + (int)selected.DayOfWeek - (int)DayOfWeek.Monday) % 7));
        var end = monday.AddDays(7);
        var shifts = await db.WorkSchedules.AsNoTracking().Where(item => item.StylistId == stylist.StylistId && item.WorkDate >= monday && item.WorkDate < end).ToListAsync();
        var timeOffs = await db.StylistTimeOffs.AsNoTracking().Where(item => item.StylistId == stylist.StylistId && item.OffDate >= DateOnly.FromDateTime(monday) && item.OffDate < DateOnly.FromDateTime(end)).ToListAsync();
        var requests = await db.StylistLeaveRequests.AsNoTracking().Where(item => item.StylistId == stylist.StylistId && item.OffDate >= DateOnly.FromDateTime(monday) && item.OffDate < DateOnly.FromDateTime(end)).OrderBy(item => item.OffDate).ToListAsync();
        return View(new StylistWeeklyScheduleViewModel
        {
            StylistName = stylist.FullName,
            WeekStart = monday,
            LeaveRequests = requests,
            Days = Enumerable.Range(0, 7).Select(offset =>
            {
                var date = monday.AddDays(offset);
                return new StylistWeekDayViewModel { Date = date, Shifts = shifts.Where(item => item.WorkDate.Date == date).OrderBy(item => item.StartTime).ToList(), ApprovedTimeOffs = timeOffs.Where(item => item.OffDate == DateOnly.FromDateTime(date)).ToList() };
            }).ToList()
        });
    }

    [HttpPost("{appointmentId:int}/start")]
    public Task<IActionResult> Start(int appointmentId) => ChangeStatus(appointmentId, "Arrived", "InProgress", true);
    [HttpPost("{appointmentId:int}/complete")]
    public Task<IActionResult> Complete(int appointmentId) => ChangeStatus(appointmentId, "InProgress", "Completed", false);

    private async Task<Stylist?> GetCurrentStylistAsync()
    {
        var email = CurrentUserEmail();
        return await db.Stylists.AsNoTracking().SingleOrDefaultAsync(item => item.Email == email && item.IsActive);
    }

    private string? CurrentUserEmail() =>
        User.FindFirstValue(JwtRegisteredClaimNames.Email) ?? User.FindFirstValue(ClaimTypes.Email);

    private async Task<IActionResult> ChangeStatus(int appointmentId, string expected, string next, bool starting)
    {
        var email = CurrentUserEmail(); var stylist=await db.Stylists.SingleOrDefaultAsync(x=>x.Email==email&&x.IsActive);
        var appointment=stylist is null?null:await db.Appointments.SingleOrDefaultAsync(x=>x.AppointmentId==appointmentId&&x.StylistId==stylist.StylistId);
        if(appointment is null) return NotFound(); if(appointment.Status!=expected) return BadRequest(new{error="Trạng thái lịch hẹn không hợp lệ."});
        appointment.Status=next; if(starting) appointment.ActualStartedAt=DateTime.Now; else appointment.ActualCompletedAt=DateTime.Now; appointment.UpdatedAt=DateTime.Now; await db.SaveChangesAsync(); return Ok(new{appointment.Status});
    }
}
