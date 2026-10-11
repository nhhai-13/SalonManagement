using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalonManagement.Data;
using SalonManagement.Models;
using SalonManagement.Services;

namespace SalonManagement.Controllers;

[Authorize(Roles = RoleGroups.FrontDesk)]
public class CheckInController(ApplicationDbContext db, CheckInService operations, TimeProvider clock) : Controller
{
    public async Task<IActionResult> Index(DateTime? date)
    {
        var day = (date ?? clock.GetUtcNow().ToOffset(TimeSpan.FromHours(7)).DateTime).Date;
        ViewBag.Day = day;
        ViewBag.Now = clock.GetUtcNow();
        var items = await db.Appointments.AsNoTracking().Include(a => a.Customer).Include(a => a.Stylist)
            .Where(a => a.AppointmentDate >= day && a.AppointmentDate < day.AddDays(1)).ToListAsync();
        items = items.OrderBy(a => a.StartTime).ToList();
        return View(items);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Change(int id, string operation, DateTime date)
    {
        try { await operations.Apply(id, operation, User.FindFirstValue(ClaimTypes.NameIdentifier) ?? ""); TempData["Success"] = "Đã cập nhật lịch hẹn."; }
        catch (KeyNotFoundException e) { return NotFound(e.Message); }
        catch (InvalidOperationException e) { TempData["Error"] = e.Message; }
        catch (DbUpdateConcurrencyException) { TempData["Error"] = "Lịch hẹn vừa được thay đổi. Vui lòng tải lại trang."; }
        return RedirectToAction(nameof(Index), new { date = date.ToString("yyyy-MM-dd") });
    }
}

[ApiController, Route("api/appointments"), Authorize(Roles = RoleGroups.FrontDesk)]
public class CheckInOperationsController(CheckInService operations) : ControllerBase
{
    [HttpPost("{id:int}/check-in")]
    public async Task<IActionResult> Change(int id)
    {
        try
        {
            var a = await operations.Apply(id, "check-in", User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "");
            return Ok(new { a.AppointmentId, a.Status, a.CheckedInAt, a.LateMinutes, a.NoShowAt });
        }
        catch (KeyNotFoundException e) { return NotFound(new { message = e.Message }); }
        catch (InvalidOperationException e) { return Conflict(new { message = e.Message }); }
        catch (DbUpdateConcurrencyException) { return Conflict(new { message = "Lịch hẹn vừa được thay đổi. Vui lòng tải lại." }); }
    }
}
