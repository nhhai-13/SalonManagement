using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalonManagement.Models.ViewModels.Booking;
using SalonManagement.Services;

namespace SalonManagement.Controllers.Booking;

[AllowAnonymous]
[Route("booking")]
public class BookingController : Controller
{
    private readonly IBookingService _bookingService;

    public BookingController(IBookingService bookingService)
    {
        _bookingService = bookingService;
    }

    /// <summary>
    /// GET: /booking hoặc /booking/select-services
    /// Màn hình chọn dịch vụ và xem tổng thời lượng, tổng tiền tạm tính (AC2, AC3).
    /// </summary>
    [HttpGet("")]
    [HttpGet("select-services")]
    public async Task<IActionResult> Index([FromQuery] List<int>? serviceIds, [FromQuery] int? serviceId)
    {
        var preselected = new HashSet<int>();
        if (serviceIds != null && serviceIds.Count > 0)
        {
            foreach (var id in serviceIds)
            {
                preselected.Add(id);
            }
        }

        if (serviceId.HasValue)
        {
            preselected.Add(serviceId.Value);
        }

        var viewModel = await _bookingService.GetSelectServicesViewModelAsync(preselected);
        return View("~/Views/Booking/Index.cshtml", viewModel);
    }

    /// <summary>
    /// POST: /booking/calculate-totals
    /// Endpoint AJAX tính toán lại tổng thời lượng và tổng tiền tạm tính thời gian thực từ phía server.
    /// </summary>
    [HttpPost("calculate-totals")]
    public async Task<IActionResult> CalculateTotals([FromBody] CalculateBookingTotalsRequest? request)
    {
        var serviceIds = request?.ServiceIds ?? new List<int>();
        var totals = await _bookingService.CalculateTotalsAsync(serviceIds);
        return Ok(totals);
    }

    /// <summary>
    /// POST: /booking/select-services
    /// Tiếp nhận danh sách dịch vụ đã chọn từ form submit.
    /// </summary>
    [HttpPost("select-services")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SelectServices([FromForm] List<int> selectedServiceIds)
    {
        var viewModel = await _bookingService.GetSelectServicesViewModelAsync(selectedServiceIds);
        return View("~/Views/Booking/Index.cshtml", viewModel);
    }
}
