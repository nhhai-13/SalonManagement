using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using SalonManagement.Data;
using SalonManagement.Models;
using SalonManagement.Models.ViewModels;

namespace SalonManagement.Controllers;

[AllowAnonymous]
public class ReviewsController(ApplicationDbContext db) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Create(string? token = null, string? reference = null)
    {
        if (!string.IsNullOrWhiteSpace(reference))
        {
            return await CreateByReference(reference);
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            ViewBag.Message = "Liên kết đánh giá không hợp lệ.";
            return View("Unavailable");
        }

        var (review, statusMessage) = await FindReviewByToken(token);
        if (review is null)
        {
            ViewBag.Message = statusMessage ?? "Liên kết đánh giá không còn hiệu lực.";
            return View("Unavailable");
        }

        if (review.Rating.HasValue)
        {
            return View("AlreadyReviewed", new StylistReviewViewModel
            {
                Token = token,
                Reference = review.Appointment?.BookingReference,
                StylistName = review.Stylist?.FullName ?? "Thợ làm tóc",
                CustomerName = review.Customer?.FullName ?? "Quý khách",
                Rating = review.Rating.Value,
                Comment = review.Comment
            });
        }

        var services = review.Appointment?.AppointmentServices?
            .Select(s => s.Service?.ServiceName ?? string.Empty)
            .Where(s => !string.IsNullOrEmpty(s))
            .ToList() ?? new List<string>();

        return View(new StylistReviewViewModel
        {
            Token = token,
            Reference = review.Appointment?.BookingReference,
            StylistName = review.Stylist?.FullName,
            CustomerName = review.Customer?.FullName,
            AppointmentDate = review.Appointment?.AppointmentDate,
            FormattedDate = review.Appointment?.AppointmentDate.ToString("dd/MM/yyyy"),
            Services = services
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(StylistReviewViewModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Token))
        {
            ViewBag.Message = "Liên kết đánh giá không hợp lệ.";
            return View("Unavailable");
        }

        var (review, statusMessage) = await FindReviewByToken(model.Token);
        if (review is null)
        {
            ViewBag.Message = statusMessage ?? "Liên kết đánh giá không còn hiệu lực.";
            return View("Unavailable");
        }

        if (review.Rating.HasValue)
        {
            return View("AlreadyReviewed", new StylistReviewViewModel
            {
                Token = model.Token,
                Reference = review.Appointment?.BookingReference,
                StylistName = review.Stylist?.FullName ?? "Thợ làm tóc",
                CustomerName = review.Customer?.FullName ?? "Quý khách",
                Rating = review.Rating.Value,
                Comment = review.Comment
            });
        }

        model.StylistName = review.Stylist?.FullName;
        model.CustomerName = review.Customer?.FullName;
        model.AppointmentDate = review.Appointment?.AppointmentDate;
        model.FormattedDate = review.Appointment?.AppointmentDate.ToString("dd/MM/yyyy");
        model.Services = review.Appointment?.AppointmentServices?
            .Select(s => s.Service?.ServiceName ?? string.Empty)
            .Where(s => !string.IsNullOrEmpty(s))
            .ToList() ?? new List<string>();

        ModelState.Remove(nameof(model.Reference));

        if (model.Rating < 1 || model.Rating > 5)
        {
            ModelState.AddModelError(nameof(model.Rating), "Vui lòng chọn số sao từ 1 đến 5.");
        }

        if (model.Comment != null && model.Comment.Length > 500)
        {
            ModelState.AddModelError(nameof(model.Comment), "Góp ý không được vượt quá 500 ký tự.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        review.Rating = model.Rating;
        review.Comment = model.Comment?.Trim();
        review.SubmittedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return View("Thanks");
    }

    [HttpGet]
    public async Task<IActionResult> Evaluate(string? token)
    {
        return await Create(token, null);
    }

    [HttpGet]
    public async Task<IActionResult> CreateByReference(string? reference)
    {
        var code = reference?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(code) || code.Length != 8)
        {
            ViewBag.Message = "Mã lịch hẹn không hợp lệ (phải gồm đúng 8 ký tự).";
            return View("Unavailable");
        }

        var appointment = await db.Appointments
            .Include(a => a.Stylist)
            .Include(a => a.Customer)
            .Include(a => a.AppointmentServices)
                .ThenInclude(s => s.Service)
            .FirstOrDefaultAsync(a => a.BookingReference == code);

        if (appointment is null)
        {
            ViewBag.Message = "Không tìm thấy lịch hẹn tương ứng với mã đã nhập.";
            return View("Unavailable");
        }

        if (appointment.Status != "Closed" && appointment.Status != "Completed")
        {
            ViewBag.Message = "Lịch hẹn chưa hoàn thành nên chưa thể đánh giá.";
            return View("Unavailable");
        }

        var review = await db.StylistReviews
            .FirstOrDefaultAsync(r => r.AppointmentId == appointment.AppointmentId);

        if (review is not null && review.Rating.HasValue)
        {
            return View("AlreadyReviewed", new StylistReviewViewModel
            {
                Reference = code,
                StylistName = appointment.Stylist?.FullName ?? "Thợ làm tóc",
                CustomerName = appointment.Customer?.FullName ?? "Quý khách",
                Rating = review.Rating.Value,
                Comment = review.Comment
            });
        }

        if (review is not null && review.ExpiresAt < DateTime.UtcNow)
        {
            ViewBag.Message = "Thời hạn đánh giá cho cuộc hẹn này đã hết hạn (quá 7 ngày).";
            return View("Unavailable");
        }

        var services = appointment.AppointmentServices?
            .Select(s => s.Service?.ServiceName ?? string.Empty)
            .Where(s => !string.IsNullOrEmpty(s))
            .ToList() ?? new List<string>();

        return View("CreateFromLookup", new StylistReviewViewModel
        {
            Reference = code,
            StylistName = appointment.Stylist?.FullName,
            CustomerName = appointment.Customer?.FullName,
            AppointmentDate = appointment.AppointmentDate,
            FormattedDate = appointment.AppointmentDate.ToString("dd/MM/yyyy"),
            Services = services
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateByReference(StylistReviewViewModel model)
    {
        var code = model.Reference?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(code) || code.Length != 8)
        {
            ViewBag.Message = "Mã lịch hẹn không hợp lệ (phải gồm đúng 8 ký tự).";
            return View("Unavailable");
        }

        var appointment = await db.Appointments
            .Include(a => a.Stylist)
            .Include(a => a.Customer)
            .Include(a => a.AppointmentServices)
                .ThenInclude(s => s.Service)
            .FirstOrDefaultAsync(a => a.BookingReference == code);

        if (appointment is null)
        {
            ViewBag.Message = "Không tìm thấy lịch hẹn.";
            return View("Unavailable");
        }

        if (appointment.Status != "Closed" && appointment.Status != "Completed")
        {
            ViewBag.Message = "Lịch hẹn chưa hoàn thành nên chưa thể đánh giá.";
            return View("Unavailable");
        }

        var review = await db.StylistReviews
            .FirstOrDefaultAsync(r => r.AppointmentId == appointment.AppointmentId);

        if (review is not null && review.Rating.HasValue)
        {
            return View("AlreadyReviewed", new StylistReviewViewModel
            {
                Reference = code,
                StylistName = appointment.Stylist?.FullName ?? "Thợ làm tóc",
                CustomerName = appointment.Customer?.FullName ?? "Quý khách",
                Rating = review.Rating.Value,
                Comment = review.Comment
            });
        }

        if (review is not null && review.ExpiresAt < DateTime.UtcNow)
        {
            ViewBag.Message = "Thời hạn đánh giá cho cuộc hẹn này đã hết hạn (quá 7 ngày).";
            return View("Unavailable");
        }

        model.StylistName = appointment.Stylist?.FullName;
        model.CustomerName = appointment.Customer?.FullName;
        model.AppointmentDate = appointment.AppointmentDate;
        model.FormattedDate = appointment.AppointmentDate.ToString("dd/MM/yyyy");
        model.Services = appointment.AppointmentServices?
            .Select(s => s.Service?.ServiceName ?? string.Empty)
            .Where(s => !string.IsNullOrEmpty(s))
            .ToList() ?? new List<string>();

        ModelState.Remove(nameof(model.Token));

        if (model.Rating < 1 || model.Rating > 5)
        {
            ModelState.AddModelError(nameof(model.Rating), "Vui lòng chọn số sao từ 1 đến 5.");
        }

        if (model.Comment != null && model.Comment.Length > 500)
        {
            ModelState.AddModelError(nameof(model.Comment), "Góp ý không được vượt quá 500 ký tự.");
        }

        if (!ModelState.IsValid)
        {
            return View("CreateFromLookup", model);
        }

        if (review is null)
        {
            var rawToken = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
            review = new StylistReview
            {
                AppointmentId = appointment.AppointmentId,
                CustomerId = appointment.CustomerId,
                StylistId = appointment.StylistId,
                ReviewTokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken))),
                RequestedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(7)
            };
            db.StylistReviews.Add(review);
        }

        review.Rating = model.Rating;
        review.Comment = model.Comment?.Trim();
        review.SubmittedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return View("Thanks");
    }

    private async Task<(StylistReview? Review, string? StatusMessage)> FindReviewByToken(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return (null, "Liên kết đánh giá không hợp lệ.");

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
        var review = await db.StylistReviews
            .Include(item => item.Stylist)
            .Include(item => item.Customer)
            .Include(item => item.Appointment)
                .ThenInclude(a => a.AppointmentServices)
                    .ThenInclude(s => s.Service)
            .FirstOrDefaultAsync(item => item.ReviewTokenHash == hash);

        if (review is null)
            return (null, "Liên kết không hợp lệ.");

        if (review.ExpiresAt < DateTime.UtcNow)
            return (null, "Liên kết đã hết hạn (quá 7 ngày).");

        return (review, null);
    }
}
