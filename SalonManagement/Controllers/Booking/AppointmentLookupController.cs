using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
        var references = result.Items.Where(item => item.Status == "Đã đóng").Select(item => item.Reference).ToList();
        var reviewableReferences = references.Count == 0 ? new List<string>() : await db.StylistReviews.AsNoTracking()
            .Where(review => references.Contains(review.Appointment.BookingReference) && review.Rating == null && review.ExpiresAt > DateTime.UtcNow)
            .Select(review => review.Appointment.BookingReference).ToListAsync();
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
                item.CancelledAt,
                item.CancelledBy,
                item.Status,
                reviewUrl = reviewableReferences.Contains(item.Reference)
                    ? Url.Action("CreateByReference", "Reviews", new { reference = item.Reference }, Request.Scheme)
                    : null
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
