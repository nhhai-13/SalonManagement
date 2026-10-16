using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalonManagement.Data;
using SalonManagement.Models;
using SalonManagement.Services;

namespace SalonManagement.Controllers.Reviews;

[AllowAnonymous]
[Route("reviews")]
public sealed class ServiceReviewsController(ApplicationDbContext db, TimeProvider timeProvider) : Controller
{
    [HttpGet("")]
    public IActionResult Index([FromQuery] string? reference)
    {
        ViewData["Reference"] = reference?.Trim().ToUpperInvariant();
        return View("~/Views/ServiceReviews/Index.cshtml");
    }

    [HttpGet("link/{token}")]
    public IActionResult Link(string token)
    {
        ViewData["ReviewToken"] = token;
        return View("~/Views/ServiceReviews/Index.cshtml");
    }

    [HttpGet("api/token/{token}")]
    public Task<IActionResult> GetByToken(string token) => GetDetailsAsync(token, null);

    [HttpGet("api/reference/{reference}")]
    public Task<IActionResult> GetByReference(string reference) => GetDetailsAsync(null, reference);

    [HttpPost("api/submit")]
    public async Task<IActionResult> Submit([FromBody] SubmitServiceReviewRequest request)
    {
        if (request.Rating is < 1 or > 5) return BadRequest(new { error = "Vui lòng chọn điểm từ 1 đến 5 sao." });
        var comment = request.Comment?.Trim();
        if (comment?.Length > 500) return BadRequest(new { error = "Nhận xét tối đa 500 ký tự." });

        var review = await FindReviewAsync(request.Token, request.Reference);
        if (review is null) return NotFound(new { error = "Không tìm thấy lời mời đánh giá hợp lệ." });
        var unavailable = GetAvailabilityError(review);
        if (unavailable is not null) return Conflict(new { error = unavailable });

        review.Rating = request.Rating;
        review.Comment = string.IsNullOrWhiteSpace(comment) ? null : comment;
        review.SubmittedAt = SalonClock.GetLocalNow(timeProvider);
        await db.SaveChangesAsync();
        return Ok(new { message = "Cảm ơn bạn đã gửi đánh giá cho Luminol Salon & Spa." });
    }

    private async Task<IActionResult> GetDetailsAsync(string? token, string? reference)
    {
        var review = await FindReviewAsync(token, reference);
        if (review is null) return NotFound(new { error = "Không tìm thấy lịch hẹn hoặc lời mời đánh giá." });
        var unavailable = GetAvailabilityError(review);
        if (unavailable is not null) return Conflict(new { error = unavailable });

        return Ok(new ServiceReviewDetailsResponse(
            review.Appointment.BookingReference,
            review.Appointment.ActualCompletedAt ?? review.Appointment.UpdatedAt ?? review.Appointment.AppointmentDate,
            review.Appointment.Stylist.FullName,
            string.Join(", ", review.Appointment.AppointmentServices.Select(item => item.Service.ServiceName)),
            review.ExpiresAt));
    }

    private async Task<ServiceReview?> FindReviewAsync(string? token, string? reference)
    {
        IQueryable<ServiceReview> reviews = db.ServiceReviews
            .Include(item => item.Appointment).ThenInclude(item => item.Stylist)
            .Include(item => item.Appointment).ThenInclude(item => item.Invoice)
            .Include(item => item.Appointment).ThenInclude(item => item.AppointmentServices).ThenInclude(item => item.Service);

        if (!string.IsNullOrWhiteSpace(token))
        {
            var hash = HashToken(token);
            return await reviews.SingleOrDefaultAsync(item => item.TokenHash == hash);
        }

        if (string.IsNullOrWhiteSpace(reference)) return null;
        var normalizedReference = reference.Trim().ToUpperInvariant();
        return await reviews.SingleOrDefaultAsync(item => item.Appointment.BookingReference == normalizedReference);
    }

    private string? GetAvailabilityError(ServiceReview review)
    {
        var now = SalonClock.GetLocalNow(timeProvider);
        if (review.SubmittedAt is not null) return "Lời mời đánh giá này đã được sử dụng. Cảm ơn bạn đã phản hồi.";
        if (review.ExpiresAt < now) return "Liên kết đánh giá đã hết hạn sau 7 ngày.";
        if (review.Appointment.Invoice?.Status != "Paid") return "Lời mời đánh giá chỉ được mở sau khi thanh toán hoàn tất.";
        if (review.Appointment.Status != "Completed") return "Bạn có thể đánh giá khi lịch hẹn đã hoàn tất.";
        return null;
    }

    private static string HashToken(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}

public sealed record SubmitServiceReviewRequest(string? Token, string? Reference, int Rating, string? Comment);
public sealed record ServiceReviewDetailsResponse(string Reference, DateTime CompletedAt, string StylistName, string Services, DateTime ExpiresAt);
