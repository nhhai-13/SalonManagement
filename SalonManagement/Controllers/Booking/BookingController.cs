using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalonManagement.Data;
using SalonManagement.Services;

namespace SalonManagement.Controllers;

[AllowAnonymous]
[Route("booking")]
public sealed class BookingController(ApplicationDbContext db, AvailabilityService availability) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index() => View(await db.Services.AsNoTracking().Where(s => s.IsActive).OrderBy(s => s.ServiceName).ToListAsync());

    [HttpGet("slots")]
    public async Task<IActionResult> Slots(DateTime date, [FromQuery] int[] serviceIds)
    {
        if (date.Date < DateTime.Today || serviceIds.Length == 0) return BadRequest(new { message = "Vui lòng chọn ngày và ít nhất một dịch vụ." });
        var result = await availability.GetSlotsAsync(date.Date, serviceIds);
        return Ok(new { result.TotalDurationMinutes, slots = result.Slots.Select(time => time.ToString(@"hh\:mm")) });
    }
}
