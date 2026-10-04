using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalonManagement.Services;

namespace SalonManagement.Controllers.Booking;

[AllowAnonymous]
[Route("booking")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class StylistAvailabilityController(StylistAvailabilityService availability) : Controller
{
    [HttpGet("stylists")]
    public async Task<IActionResult> Stylists([FromQuery] int[] serviceIds)
    {
        if (!ModelState.IsValid) return BadRequest(new { message = "Danh sách dịch vụ không hợp lệ." });
        try { return Ok(await availability.GetStylistsAsync(serviceIds)); }
        catch (ArgumentException e) { return BadRequest(new { message = e.Message }); }
    }

    [HttpGet("stylist-slots")]
    public async Task<IActionResult> Slots([FromQuery] int[] serviceIds, int? stylistId, DateOnly date)
    {
        if (!ModelState.IsValid || date == default || stylistId == null || stylistId < 0)
            return BadRequest(new { message = "Vui lòng chọn ngày và thợ hợp lệ." });
        try { return Ok(await availability.GetSlotsAsync(serviceIds, stylistId.Value, date)); }
        catch (ArgumentException e) { return BadRequest(new { message = e.Message }); }
    }

    [HttpGet("stylist-dates")]
    public async Task<IActionResult> Dates([FromQuery] int[] serviceIds, int? stylistId, DateOnly date)
    {
        if (!ModelState.IsValid || date == default || stylistId == null || stylistId < 0 || date.DayNumber > DateOnly.MaxValue.DayNumber - 14)
            return BadRequest(new { message = "Vui lòng chọn ngày và thợ hợp lệ." });
        try
        {
            var slots = await availability.GetSlotsAsync(serviceIds, stylistId.Value, date);
            var suggestions = new List<object>();
            if (slots.Count == 0)
                for (var i = 1; i <= 14 && suggestions.Count < 2; i++)
                {
                    var day = date.AddDays(i);
                    var next = await availability.GetSlotsAsync(serviceIds, stylistId.Value, day);
                    if (next.Count > 0) suggestions.Add(new { date = day.ToString("yyyy-MM-dd"), label = day.ToString("dd/MM/yyyy"), earliestSlot = next[0].Start });
                }
            return Ok(new { slotCount = slots.Count, suggestions });
        }
        catch (ArgumentException e) { return BadRequest(new { message = e.Message }); }
    }

    [HttpGet("stylist-assignment")]
    public async Task<IActionResult> Assignment([FromQuery] int[] serviceIds, int? stylistId, DateOnly date, TimeOnly? start)
    {
        if (!ModelState.IsValid || date == default || stylistId == null || stylistId < 0 || start == null)
            return BadRequest(new { message = "Vui lòng chọn đầy đủ dịch vụ, thợ, ngày và giờ." });
        try
        {
            var openingError = await availability.ValidateOpeningTimeAsync(serviceIds, date, start.Value);
            if (openingError != null) return Conflict(new { message = openingError });
            var assignment = await availability.AssignAsync(serviceIds, stylistId.Value, date, start.Value);
            return assignment == null
                ? Conflict(new { message = "Khung giờ này không còn thợ phù hợp. Vui lòng chọn lại giờ." })
                : Ok(assignment);
        }
        catch (ArgumentException e) { return BadRequest(new { message = e.Message }); }
    }
}
