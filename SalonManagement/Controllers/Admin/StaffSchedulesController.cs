using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalonManagement.Data;
using SalonManagement.Models;
using SalonManagement.Services;

namespace SalonManagement.Controllers;

[Authorize(Roles = UserRoles.Owner)]
[Route("management/staff-schedules")]
public sealed class StaffSchedulesController(ApplicationDbContext db, TimeProvider timeProvider) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(DateTime? weekStart)
    {
        var start = StartOfWeek(weekStart ?? SalonClock.GetLocalNow(timeProvider).Date);
        var staff = await (
            from user in db.Users.AsNoTracking()
            join userRole in db.UserRoles on user.Id equals userRole.UserId
            join role in db.Roles on userRole.RoleId equals role.Id
            where user.IsActive && (role.Name == UserRoles.Receptionist || role.Name == UserRoles.Stylist)
            orderby role.Name, user.FullName
            select new { user.Id, user.FullName, user.Email, Role = role.Name })
            .Select(item => new StaffSchedulePerson(item.Id, item.FullName, item.Email ?? string.Empty, item.Role!))
            .ToListAsync();

        var ids = staff.Select(person => person.UserId).ToArray();
        var shifts = await db.StaffWorkSchedules.AsNoTracking()
            .Where(shift => ids.Contains(shift.UserId) && shift.WorkDate >= start && shift.WorkDate < start.AddDays(7))
            .OrderBy(shift => shift.WorkDate)
            .ToListAsync();

        return View(new StaffScheduleWeekViewModel(start, staff, shifts));
    }

    private static DateTime StartOfWeek(DateTime day)
    {
        var date = day.Date;
        var offset = ((int)date.DayOfWeek + 6) % 7;
        return date.AddDays(-offset);
    }
}

public sealed record StaffSchedulePerson(string UserId, string FullName, string Email, string Role);
public sealed record StaffScheduleWeekViewModel(DateTime WeekStart, IReadOnlyList<StaffSchedulePerson> Staff, IReadOnlyList<StaffWorkSchedule> Shifts);
