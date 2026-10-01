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

    [HttpGet("slots")]
    public async Task<IActionResult> Slots([FromQuery] int[] serviceIds, int stylistId, DateOnly date)
    {
        if (!ModelState.IsValid || date == default || stylistId <= 0)
            return BadRequest(new { message = "Vui lòng chọn ngày và thợ hợp lệ." });
        try { return Ok(await availability.GetSlotsAsync(serviceIds, stylistId, date)); }
        catch (ArgumentException e) { return BadRequest(new { message = e.Message }); }
    }
}
