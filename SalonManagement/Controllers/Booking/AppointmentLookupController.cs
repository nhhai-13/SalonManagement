using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalonManagement.Data;
using SalonManagement.Services;

namespace SalonManagement.Controllers;

[AllowAnonymous]
[Route("appointments/lookup")]
public sealed class AppointmentLookupController(
    ApplicationDbContext db,
    TimeProvider timeProvider) : Controller
{
    [HttpGet("")]
    public IActionResult Index() => View();

    [HttpGet("search")]
    public async Task<IActionResult> Search(string? query)
    {
        var result = await new AppointmentLookupService(db, timeProvider).SearchAsync(query);
        return Ok(new
        {
            error = result.Error,
            appointments = result.Items.Select(item => new
            {
                item.Reference,
                date = item.Date.ToString("dd/MM/yyyy"),
                startTime = item.StartTime.ToString(@"hh\:mm"),
                endTime = item.EndTime.ToString(@"hh\:mm"),
                item.Services,
                item.StylistName,
                item.DurationMinutes,
                item.Status
            })
        });
    }

    [HttpPost("cancel")]
    public async Task<IActionResult> Cancel([FromBody] CancelRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Reference))
            return BadRequest(new { error = "Vui lòng nhập mã lịch hẹn." });

        var reference = request.Reference.Trim().ToUpperInvariant();
        if (reference.Length != 8)
            return BadRequest(new { error = "Mã lịch hẹn phải gồm đúng 8 ký tự." });

        var actorId = "customer:guest";
        var result = await new AppointmentLookupService(db, timeProvider)
            .CancelAsync(reference, actorId, request.Reason);

        if (!result.Success)
            return BadRequest(new { error = result.Error });

        return Ok(new { message = "Đã huỷ lịch hẹn thành công." });
    }
}

public sealed record CancelRequest(string Reference, string? Reason);