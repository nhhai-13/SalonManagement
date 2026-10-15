using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalonManagement.Data;
using SalonManagement.Models;
using SalonManagement.Models.ViewModels;

namespace SalonManagement.Controllers;

[AllowAnonymous]
public class ReviewsController(ApplicationDbContext db) : Controller
{
    [HttpGet]
    public async Task<IActionResult> CreateByReference(string reference)
    {
        var review = await FindValidReviewByReference(reference);
        if (review is null) return View("Unavailable");
        return View("CreateFromLookup", new StylistReviewViewModel
        {
            Reference = reference.Trim().ToUpperInvariant(),
            StylistName = review.Stylist.FullName,
            CustomerName = review.Customer.FullName
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateByReference(StylistReviewViewModel model)
    {
        var review = await FindValidReviewByReference(model.Reference);
        if (review is null) return View("Unavailable");
        model.StylistName = review.Stylist.FullName;
        model.CustomerName = review.Customer.FullName;
        ModelState.Remove(nameof(model.Token));
        ModelState.Remove(nameof(model.Reference));
        if (!ModelState.IsValid) return View("CreateFromLookup", model);
        review.Rating = model.Rating;
        review.Comment = model.Comment?.Trim();
        review.SubmittedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return View("Thanks");
    }

    [HttpGet]
    public async Task<IActionResult> Create(string token)
    {
        var review = await FindValidReview(token);
        if (review is null) return View("Unavailable");
        return View(new StylistReviewViewModel { Token = token, StylistName = review.Stylist.FullName, CustomerName = review.Customer.FullName });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(StylistReviewViewModel model)
    {
        var review = await FindValidReview(model.Token);
        if (review is null) return View("Unavailable");
        model.StylistName = review.Stylist.FullName;
        model.CustomerName = review.Customer.FullName;
        if (!ModelState.IsValid) return View(model);
        review.Rating = model.Rating;
        review.Comment = model.Comment?.Trim();
        review.SubmittedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return View("Thanks");
    }

    private async Task<StylistReview?> FindValidReview(string? token)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
        var now = DateTime.UtcNow;
        return await db.StylistReviews.Include(item => item.Stylist).Include(item => item.Customer)
            .Include(item => item.Appointment)
            .FirstOrDefaultAsync(item => item.ReviewTokenHash == hash && item.Rating == null &&
                item.ExpiresAt > now && item.Appointment.Status == "Closed");
    }

    private async Task<StylistReview?> FindValidReviewByReference(string? reference)
    {
        var code = reference?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(code) || code.Length != 8) return null;
        var now = DateTime.UtcNow;
        return await db.StylistReviews.Include(item => item.Stylist).Include(item => item.Customer)
            .Include(item => item.Appointment)
            .FirstOrDefaultAsync(item => item.Appointment.BookingReference == code && item.Rating == null &&
                item.ExpiresAt > now && item.Appointment.Status == "Closed");
    }
}
