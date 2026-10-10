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
public class NoShowsController(ApplicationDbContext db, NoShowService operations, TimeProvider clock) : Controller
{
    public async Task<IActionResult> Index(DateTime? date)
    {
        var day = (date ?? clock.GetUtcNow().ToOffset(TimeSpan.FromHours(7)).DateTime).Date;
        ViewBag.Day = day;
        ViewBag.Now = clock.GetUtcNow();
        var items = await db.Appointments.AsNoTracking().Include(a => a.Customer).Include(a => a.Stylist)
            .Where(a => a.AppointmentDate >= day && a.AppointmentDate < day.AddDays(1)).ToListAsync();
        items = items.OrderBy(a => a.StartTime).ToList();
        var phones = items.Select(a => a.Customer.Phone).Distinct().ToArray();
        ViewBag.NoShowCounts = await db.Appointments.Where(a => phones.Contains(a.Customer.Phone) && a.Status == AppointmentStatuses.NoShow)
            .GroupBy(a => a.Customer.Phone).Select(g => new { Phone = g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Phone, x => x.Count);
        return View(items);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await BookingLists();
        return View(new DeskBookingRequest { StartsAt = clock.GetUtcNow().ToOffset(TimeSpan.FromHours(7)).DateTime.AddMinutes(15) });
    }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(DeskBookingRequest request, [FromServices] DeskBookingService bookings)
    {
        if (ModelState.IsValid)
        {
            try
            {
                var appointment = await bookings.Create(request, User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "");
                TempData["Success"] = "Đã đặt lịch tại quầy.";
                return RedirectToAction(nameof(Index), new { date = appointment.AppointmentDate.ToString("yyyy-MM-dd") });
            }
            catch (InvalidOperationException e) { ModelState.AddModelError("", e.Message); }
        }
        await BookingLists();
        return View(request);
    }
    private async Task BookingLists()
    {
        ViewBag.Stylists = await db.Stylists.Where(s => s.IsActive).OrderBy(s => s.FullName).ToListAsync();
        ViewBag.Services = await db.Services.Where(s => s.IsActive).OrderBy(s => s.ServiceName).ToListAsync();
    }

    [HttpGet]
    public async Task<IActionResult> CustomerWarning(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone) || phone.Length > 20) return BadRequest(new { message = "Số điện thoại không hợp lệ." });
        var count = await operations.NoShowCount(phone.Trim());
        return Json(new { message = count > 0 ? $"Cảnh báo: khách đã không đến {count} lần." : "Khách chưa có lần không đến." });
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
public class NoShowOperationsController(NoShowService operations) : ControllerBase
{
    [HttpPost("{id:int}/no-show")]
    public Task<IActionResult> Mark(int id) => Change(id, "no-show");
    [HttpPost("{id:int}/undo-no-show")]
    public Task<IActionResult> Undo(int id) => Change(id, "undo-no-show");
    private async Task<IActionResult> Change(int id, string operation)
    {
        try
        {
            var a = await operations.Apply(id, operation, User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "");
            return Ok(new { a.AppointmentId, a.Status, a.CheckedInAt, a.LateMinutes, a.NoShowAt });
        }
        catch (KeyNotFoundException e) { return NotFound(new { message = e.Message }); }
        catch (InvalidOperationException e) { return Conflict(new { message = e.Message }); }
        catch (DbUpdateConcurrencyException) { return Conflict(new { message = "Lịch hẹn vừa được thay đổi. Vui lòng tải lại." }); }
    }
    [HttpGet("occupied-slots")]
    public async Task<IActionResult> OccupiedSlots(int stylistId, DateTime date)
    {
        if (stylistId <= 0 || date == default) return BadRequest(new { message = "Vui lòng chọn thợ và ngày hợp lệ." });
        var slots = await operations.OccupiedSlots(stylistId, date);
        return Ok(slots.OrderBy(a => a.StartTime).Select(a => new { a.AppointmentId, a.StartTime, a.EndTime }));
    }

    [HttpGet("customer-warning")]
    public async Task<IActionResult> CustomerWarning([FromQuery] string phone)
    {
        if (string.IsNullOrWhiteSpace(phone) || phone.Length > 20) return BadRequest(new { message = "Số điện thoại không hợp lệ." });
        var count = await operations.NoShowCount(phone.Trim());
        return Ok(new { noShowCount = count, warning = count > 0 ? $"Khách đã không đến {count} lần." : null });
    }
}
