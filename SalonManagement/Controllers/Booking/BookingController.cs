using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalonManagement.Data;
using SalonManagement.Models.ViewModels.Booking;
using SalonManagement.Services;

namespace SalonManagement.Controllers.Booking;

[AllowAnonymous]
[Route("booking")]
public class BookingController : Controller
{
    private readonly IBookingService _bookingService;
    private readonly AvailabilityService _availability;
    private readonly TimeProvider _timeProvider;

    public BookingController(IBookingService bookingService)
        : this(bookingService, null!, TimeProvider.System)
    {
    }

    public BookingController(IBookingService bookingService, AvailabilityService availability, TimeProvider timeProvider)
    {
        _bookingService = bookingService;
        _availability = availability;
        _timeProvider = timeProvider;
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

        if (preselected.Count > IBookingService.MaxServicesLimit)
        {
            ModelState.AddModelError(string.Empty, "Số lượng dịch vụ trong một lượt đặt không được vượt quá 5");
        }

        var viewModel = await _bookingService.GetSelectServicesViewModelAsync(preselected);
        return View("~/Views/Booking/Index.cshtml", viewModel);
    }

    [HttpGet("slots")]
    public async Task<IActionResult> Slots(DateTime date, [FromQuery] int[] serviceIds)
    {
        if (date.Date < SalonClock.GetLocalNow(_timeProvider).Date || serviceIds.Length == 0)
            return BadRequest(new { message = "Vui lòng chọn ngày hợp lệ và ít nhất một dịch vụ." });

        var result = await _availability.GetSlotsAsync(date.Date, serviceIds);
        var suggestions = result.Slots.Count == 0
            ? await _availability.GetSuggestedDatesAsync(date.Date, serviceIds)
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

    /// <summary>
    /// POST: /booking/calculate-totals
    /// Endpoint AJAX tính toán lại tổng thời lượng và tổng tiền tạm tính thời gian thực từ phía server.
    /// Từ chối tính toán nếu vượt quá 5 dịch vụ (AC1).
    /// </summary>
    [HttpPost("calculate-totals")]
    public async Task<IActionResult> CalculateTotals([FromBody] CalculateBookingTotalsRequest? request)
    {
        var serviceIds = request?.ServiceIds ?? new List<int>();

        if (serviceIds.Count > IBookingService.MaxServicesLimit)
        {
            return BadRequest(new { message = "Số lượng dịch vụ trong một lượt đặt không được vượt quá 5" });
        }

        try
        {
            var totals = await _bookingService.CalculateTotalsAsync(serviceIds);
            return Ok(totals);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// POST: /booking/select-services
    /// Tiếp nhận danh sách dịch vụ đã chọn từ form submit.
    /// Kiểm tra xác thực không cho phép vượt quá 5 dịch vụ (AC1).
    /// </summary>
    [HttpPost("select-services")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SelectServices([FromForm] List<int>? selectedServiceIds)
    {
        var ids = selectedServiceIds ?? new List<int>();

        if (ids.Count > IBookingService.MaxServicesLimit)
        {
            ModelState.AddModelError(string.Empty, "Số lượng dịch vụ trong một lượt đặt không được vượt quá 5");
            var errorViewModel = await _bookingService.GetSelectServicesViewModelAsync(ids.Take(IBookingService.MaxServicesLimit));
            errorViewModel.ErrorMessage = "Số lượng dịch vụ trong một lượt đặt không được vượt quá 5";
            return View("~/Views/Booking/Index.cshtml", errorViewModel);
        }

        var viewModel = await _bookingService.GetSelectServicesViewModelAsync(ids);
        return View("~/Views/Booking/Index.cshtml", viewModel);
    }
}
