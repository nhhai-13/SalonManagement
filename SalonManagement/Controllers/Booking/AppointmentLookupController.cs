using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalonManagement.Data;
using SalonManagement.Models;
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
        var references = result.Items
            .Where(item => item.Status is "Đã đóng" or "Đã hoàn thành" or "Closed" or "Completed")
            .Select(item => item.Reference.ToUpperInvariant())
            .ToList();

        var existingReviews = references.Count == 0
            ? new List<StylistReview>()
            : await db.StylistReviews.AsNoTracking()
                .Include(review => review.Appointment)
                .Where(review => references.Contains(review.Appointment.BookingReference.ToUpper()))
                .ToListAsync();

        var reviewedReferences = existingReviews
            .Where(r => r.Rating.HasValue)
            .Select(r => r.Appointment.BookingReference.ToUpperInvariant())
            .ToHashSet();

        return Ok(new
        {
            error = result.Error,
            appointments = result.Items.Select(item =>
            {
                var upperRef = item.Reference.ToUpperInvariant();
                var isCompletedOrClosed = item.Status is "Đã đóng" or "Đã hoàn thành" or "Closed" or "Completed";
                var isReviewed = reviewedReferences.Contains(upperRef);
                var canReview = isCompletedOrClosed && !isReviewed;

                return new
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
                    isReviewed,
                    canReview,
                    reviewUrl = canReview
                        ? Url.Action("CreateByReference", "Reviews", new { reference = item.Reference }, Request.Scheme)
                        : null
                };
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
