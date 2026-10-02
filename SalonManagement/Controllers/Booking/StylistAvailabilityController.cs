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
    public async Task<IActionResult> Slots([FromQuery] int[] serviceIds, int? stylistId, DateOnly date)
    {
        if (!ModelState.IsValid || date == default || stylistId == null || stylistId < 0)
            return BadRequest(new { message = "Vui lòng chọn ngày và thợ hợp lệ." });
        try { return Ok(await availability.GetSlotsAsync(serviceIds, stylistId.Value, date)); }
        catch (ArgumentException e) { return BadRequest(new { message = e.Message }); }
    }

    [HttpGet("assignment")]
    public async Task<IActionResult> Assignment([FromQuery] int[] serviceIds, int? stylistId, DateOnly date, TimeOnly? start)
    {
        if (!ModelState.IsValid || date == default || stylistId == null || stylistId < 0 || start == null)
            return BadRequest(new { message = "Vui lòng chọn đầy đủ dịch vụ, thợ, ngày và giờ." });
        try
        {
            var assignment = await availability.AssignAsync(serviceIds, stylistId.Value, date, start.Value);
            return assignment == null
                ? Conflict(new { message = "Khung giờ này không còn thợ phù hợp. Vui lòng chọn lại giờ." })
                : Ok(assignment);
        }
        catch (ArgumentException e) { return BadRequest(new { message = e.Message }); }
    }
}
