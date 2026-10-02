using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using SalonManagement.Data;
using SalonManagement.Models.ViewModels.Booking;
using SalonManagement.Services;

namespace SalonManagement.Controllers.Booking;

public sealed record ConfirmBookingInput(DateTime Date, string StartTime, int[] ServiceIds, string FullName, string Phone, string? Email, string? Notes, string? Website);

[AllowAnonymous]
[Route("booking")]
public class BookingController : Controller
{
    private readonly IBookingService _bookingService;
    private readonly AvailabilityService _availability;
    private readonly TimeProvider _timeProvider;
    private readonly AppointmentBookingService? _appointmentBookingService;

    public BookingController(IBookingService bookingService)
        : this(bookingService, null!, TimeProvider.System)
    {
    }

    [ActivatorUtilitiesConstructor]
    public BookingController(IBookingService bookingService, AvailabilityService availability, TimeProvider timeProvider, AppointmentBookingService? appointmentBookingService = null)
    {
        _bookingService = bookingService;
        _availability = availability;
        _timeProvider = timeProvider;
        _appointmentBookingService = appointmentBookingService;
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

    [HttpPost("confirm")]
    public async Task<IActionResult> Confirm([FromBody] ConfirmBookingInput input)
    {
        if (!string.IsNullOrWhiteSpace(input.Website)) return Conflict(new { code = "bot_detected", message = "Yêu cầu đặt lịch không hợp lệ." });
        if (!TimeSpan.TryParse(input.StartTime, out var startTime)) return BadRequest(new { message = "Giờ hẹn không hợp lệ." });
        if (_appointmentBookingService is null) return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Dịch vụ đặt lịch chưa sẵn sàng." });
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.MapToIPv4().ToString() ?? "unknown";
        var limiter = new BookingRateLimiter(_timeProvider);
        var limit = limiter.TryReserve(ipAddress);
        if (!limit.Allowed) return StatusCode(StatusCodes.Status429TooManyRequests, new { code = "ip_rate_limited", message = $"Bạn đã đạt giới hạn 5 lượt đặt trong một giờ. Vui lòng thử lại sau {limit.RetryAt!.Value.LocalDateTime:HH:mm}." });
        var result = await _appointmentBookingService.CreateAsync(new BookingRequest(input.Date, startTime, input.ServiceIds, input.FullName, input.Phone, input.Email, input.Notes));
        if (result.Confirmation is null)
        {
            limiter.Release(ipAddress);
            return Conflict(new
            {
                code = result.ErrorCode,
                message = result.Message,
                errors = result.FieldErrors,
                reloadSlots = result.ErrorCode == "slot_unavailable"
            });
        }
        var confirmation = result.Confirmation;
        return Ok(new { confirmation.Reference, confirmation.StylistName, confirmation.Services, date = confirmation.Date.ToString("dd/MM/yyyy"), startTime = confirmation.StartTime.ToString(@"hh\:mm"), endTime = confirmation.EndTime.ToString(@"hh\:mm") });
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
