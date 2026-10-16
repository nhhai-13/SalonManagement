using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalonManagement.Data;
using SalonManagement.Services;

namespace SalonManagement.Controllers;

[AllowAnonymous]
[Route("appointments/lookup")]
public sealed class AppointmentLookupController(ApplicationDbContext db, TimeProvider timeProvider, ILogger<AppointmentLookupController> logger) : Controller
{
    [HttpGet("")]
    public IActionResult Index() => View();

    [HttpGet("search")]
    public async Task<IActionResult> Search(string? query)
    {
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.MapToIPv4().ToString() ?? "unknown";
        var limiter = new AppointmentLookupRateLimiter(timeProvider);
        var block = limiter.Check(ipAddress);
        if (block.IsBlocked) return StatusCode(StatusCodes.Status429TooManyRequests, new { error = $"Bạn tạm thời bị chặn tra cứu. Vui lòng thử lại sau {block.RetryAt!.Value.LocalDateTime:HH:mm}." });
        var result = await new AppointmentLookupService(db).SearchAsync(query);
        if (result.Error is not null)
        {
            block = limiter.RegisterFailure(ipAddress);
            if (block.IsBlocked)
            {
                logger.LogWarning("Appointment lookup blocked for IP {IpAddress} until {RetryAt}", ipAddress, block.RetryAt);
                return StatusCode(StatusCodes.Status429TooManyRequests, new { error = $"Bạn tạm thời bị chặn tra cứu. Vui lòng thử lại sau {block.RetryAt!.Value.LocalDateTime:HH:mm}." });
            }
        }
        return Ok(new { error = result.Error, appointments = result.Items.Select(item => new { item.Reference, date = item.Date.ToString("dd/MM/yyyy"), startTime = item.StartTime.ToString(@"hh\:mm"), endTime = item.EndTime.ToString(@"hh\:mm"), item.Services, item.StylistName, item.DurationMinutes, item.Status, cancelledAt = item.CancelledAt?.ToString("dd/MM/yyyy HH:mm"), item.CancelledBy }) });
    }

    [HttpPost("cancel")]
    public async Task<IActionResult> Cancel([FromBody] CancelAppointmentRequest request)
    {
        var emailNotifications = HttpContext.RequestServices.GetRequiredService<AppointmentEmailNotificationService>();
        var notifications = HttpContext.RequestServices.GetRequiredService<StylistInAppNotificationService>();
        var result = await new AppointmentCancellationService(db, timeProvider, emailNotifications, notifications).CancelAsync(request.Reference);
        return result.Succeeded ? Ok(new { status = "Cancelled" }) : BadRequest(new { error = result.Error, code = result.ErrorCode });
    }
}

public sealed record CancelAppointmentRequest(string? Reference);
