using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalonManagement.Data;
using SalonManagement.Services;

namespace SalonManagement.Controllers;

[AllowAnonymous]
[Route("booking")]
public sealed record ConfirmBookingInput(DateTime Date, string StartTime, int[] ServiceIds, string FullName, string Phone);

public sealed class BookingController(ApplicationDbContext db, AvailabilityService availability, TimeProvider timeProvider) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        ViewData["BookingMinDate"] = SalonClock.GetLocalNow(timeProvider).ToString("yyyy-MM-dd");
        return View(await db.Services.AsNoTracking().Where(s => s.IsActive).OrderBy(s => s.ServiceName).ToListAsync());
    }

    [HttpGet("slots")]
    public async Task<IActionResult> Slots(DateTime date, [FromQuery] int[] serviceIds)
    {
        if (date.Date < SalonClock.GetLocalNow(timeProvider).Date || serviceIds.Length == 0) return BadRequest(new { message = "Vui lòng chọn ngày hợp lệ và ít nhất một dịch vụ." });
        var result = await availability.GetSlotsAsync(date.Date, serviceIds);
        var suggestions = result.Slots.Count == 0
            ? await availability.GetSuggestedDatesAsync(date.Date, serviceIds)
            : [];
        return Ok(new
        {
            result.TotalDurationMinutes,
            slots = result.Slots.Select(time => time.ToString(@"hh\:mm")),
            suggestions = suggestions.Select(item => new
            {
                date = item.Date.ToString("yyyy-MM-dd"),
                label = item.Date.ToString("dd/MM/yyyy"),
                item.SlotCount,
                earliestSlot = item.EarliestSlot.ToString(@"hh\:mm")
            })
        });
    }

    [HttpPost("confirm")]
    public async Task<IActionResult> Confirm([FromBody] ConfirmBookingInput input)
    {
        if (!TimeSpan.TryParse(input.StartTime, out var startTime))
            return BadRequest(new { message = "Giờ hẹn không hợp lệ." });
        var result = await new BookingService(db, timeProvider).CreateAsync(new BookingRequest(input.Date, startTime, input.ServiceIds, input.FullName, input.Phone));
        if (result.Confirmation is null)
            return Conflict(new { code = result.ErrorCode, message = result.Message });
        var confirmation = result.Confirmation;
        return Ok(new
        {
            confirmation.Reference,
            confirmation.StylistName,
            confirmation.Services,
            date = confirmation.Date.ToString("dd/MM/yyyy"),
            startTime = confirmation.StartTime.ToString(@"hh\:mm"),
            endTime = confirmation.EndTime.ToString(@"hh\:mm")
        });
    }
}
