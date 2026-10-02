using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalonManagement.Models;
using SalonManagement.Services;

namespace SalonManagement.Controllers
{
    /// <summary>
    /// API lấy khung giờ trống cho trang đặt lịch công khai.
    /// </summary>
    [Route("api/availability")]
    [ApiController]
    [AllowAnonymous]
    public class AvailabilityController : ControllerBase
    {
        private readonly IAvailabilityService _availability;

        public AvailabilityController(IAvailabilityService availability)
        {
            _availability = availability;
        }

        /// <summary>
        /// GET: /api/availability?date=2026-10-05&serviceIds=1&serviceIds=2&stylistId=3
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetSlots(
            [FromQuery] DateTime date,
            [FromQuery] List<int> serviceIds,
            [FromQuery] int? stylistId = null)
        {
            if (serviceIds == null || serviceIds.Count == 0)
                return BadRequest(new { error = "Vui lòng chọn ít nhất 1 dịch vụ" });

            if (date.Date < DateTime.Now.Date)
                return BadRequest(new { error = "Ngày không hợp lệ" });

            var slots = await _availability.GetAvailableSlotsAsync(
                date, serviceIds, stylistId);

            var nextDates = new List<DateTime>();
            if (!slots.Any())
            {
                // Cho AC5: tìm 2 ngày gần nhất còn slot
                nextDates = await _availability.FindNextAvailableDatesAsync(
                    date, 2, serviceIds, stylistId);
            }

            return Ok(new
            {
                date = date.ToString("yyyy-MM-dd"),
                totalSlots = slots.Count,
                slots = slots.Select(s => new
                {
                    startTime = s.DisplayTime,
                    endTime = $"{s.EndTime.Hours:D2}:{s.EndTime.Minutes:D2}",
                    stylistIds = s.AvailableStylistIds
                }),
                nextAvailableDates = nextDates.Select(d => d.ToString("yyyy-MM-dd"))
            });
        }
    }
}