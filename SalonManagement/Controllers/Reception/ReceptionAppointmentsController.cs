using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalonManagement.Models;
using SalonManagement.Models.ViewModels.Appointments;
using SalonManagement.Services.Appointments;

namespace SalonManagement.Controllers.Reception;

[ApiController]
[Route("api/reception")]
[Authorize(Roles = $"{UserRoles.Receptionist},{UserRoles.Admin},{UserRoles.Owner}")]
public class ReceptionAppointmentsController : ControllerBase
{
    private readonly IAppointmentRescheduleService _rescheduleService;

    public ReceptionAppointmentsController(IAppointmentRescheduleService rescheduleService)
    {
        _rescheduleService = rescheduleService;
    }

    /// <summary>
    /// Lấy toàn bộ lịch hẹn và ca làm việc theo ngày cho màn hình lịch ngày của lễ tân.
    /// </summary>
    [HttpGet("daily-schedule")]
    public async Task<IActionResult> GetDailySchedule([FromQuery] string? date, CancellationToken cancellationToken)
    {
        var targetDate = DateTime.Today;
        if (!string.IsNullOrWhiteSpace(date) && DateTime.TryParse(date, out var parsedDate))
        {
            targetDate = parsedDate.Date;
        }

        var result = await _rescheduleService.GetDailyScheduleAsync(targetDate, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Lấy thông tin chi tiết của lịch hẹn để mở Modal dời lịch.
    /// </summary>
    [HttpGet("appointments/{id:int}/reschedule-info")]
    public async Task<IActionResult> GetRescheduleInfo([FromRoute] int id, [FromQuery] string? targetDate, CancellationToken cancellationToken)
    {
        DateTime? parsedTarget = null;
        if (!string.IsNullOrWhiteSpace(targetDate) && DateTime.TryParse(targetDate, out var dt))
        {
            parsedTarget = dt.Date;
        }

        var info = await _rescheduleService.GetRescheduleInfoAsync(id, parsedTarget, cancellationToken);
        if (info == null)
        {
            return NotFound(new { message = "Không tìm thấy lịch hẹn." });
        }

        return Ok(info);
    }

    /// <summary>
    /// Kiểm tra tính hợp lệ trước khi lưu (Kiểm tra kỹ năng F33, ca làm việc, trùng lịch).
    /// </summary>
    [HttpPost("appointments/{id:int}/validate-reschedule")]
    public async Task<IActionResult> ValidateReschedule([FromRoute] int id, [FromBody] RescheduleAppointmentRequest request, CancellationToken cancellationToken)
    {
        if (request == null || request.NewStylistId <= 0)
        {
            return BadRequest(new { message = "Dữ liệu yêu cầu không hợp lệ." });
        }

        var validation = await _rescheduleService.ValidateRescheduleAsync(
            id,
            request.NewStylistId,
            request.NewDate,
            request.NewStartTime,
            cancellationToken);

        return Ok(validation);
    }

    /// <summary>
    /// Thực hiện dời giờ hoặc đổi thợ cho lịch hẹn.
    /// </summary>
    [HttpPost("appointments/{id:int}/reschedule")]
    public async Task<IActionResult> Reschedule([FromRoute] int id, [FromBody] RescheduleAppointmentRequest request, CancellationToken cancellationToken)
    {
        if (request == null || request.NewStylistId <= 0)
        {
            return BadRequest(new { message = "Dữ liệu yêu cầu không hợp lệ." });
        }

        var currentUserId = User?.FindFirstValue(ClaimTypes.NameIdentifier) ?? User?.FindFirstValue("sub");
        var currentUserName = User?.FindFirstValue("email") ?? User?.Identity?.Name;

        var result = await _rescheduleService.RescheduleAppointmentAsync(
            id,
            request,
            currentUserId,
            currentUserName,
            cancellationToken);

        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }
}
