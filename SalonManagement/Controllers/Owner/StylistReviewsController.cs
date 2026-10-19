using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalonManagement.Data;
using SalonManagement.Models;

namespace SalonManagement.Controllers.Owner;

[Authorize(Roles = RoleGroups.Management)]
[Route("owner/stylist-reviews")]
public sealed class StylistReviewsController(ApplicationDbContext db) : Controller
{
    public const int MinimumReviewThreshold = 5;

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var stylists = await db.Stylists.AsNoTracking()
            .OrderBy(item => item.FullName)
            .Select(item => new { item.StylistId, item.FullName, item.IsActive })
            .ToListAsync();

        var ratings = await db.StylistReviews.AsNoTracking()
            .Where(item => item.Rating.HasValue)
            .GroupBy(item => item.StylistId)
            .Select(group => new
            {
                StylistId = group.Key,
                Count = group.Count(),
                Average = group.Average(item => item.Rating!.Value)
            })
            .ToDictionaryAsync(item => item.StylistId);

        var rows = stylists.Select(stylist =>
        {
            ratings.TryGetValue(stylist.StylistId, out var rating);
            var count = rating?.Count ?? 0;
            return new StylistReviewSummary(
                stylist.StylistId,
                stylist.FullName,
                count,
                count >= MinimumReviewThreshold ? Math.Round(rating!.Average, 1, MidpointRounding.AwayFromZero) : null,
                stylist.IsActive);
        }).ToList();

        return View(rows);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Details(int id)
    {
        var stylist = await db.Stylists.AsNoTracking()
            .FirstOrDefaultAsync(s => s.StylistId == id);

        if (stylist is null)
        {
            return NotFound();
        }

        var reviews = await db.StylistReviews.AsNoTracking()
            .Include(r => r.Customer)
            .Include(r => r.Appointment)
            .Where(r => r.StylistId == id && r.Rating.HasValue)
            .OrderByDescending(r => r.SubmittedAt ?? r.RequestedAt)
            .ToListAsync();

        var count = reviews.Count;
        double? average = count >= MinimumReviewThreshold
            ? Math.Round(reviews.Average(r => r.Rating!.Value), 1, MidpointRounding.AwayFromZero)
            : null;

        var items = reviews.Select(r => new StylistReviewItem(
            r.StylistReviewId,
            r.Rating!.Value,
            r.Comment,
            r.Customer?.FullName ?? "Khách hàng",
            r.Appointment?.BookingReference ?? "—",
            r.SubmittedAt ?? r.RequestedAt
        )).ToList();

        var viewModel = new StylistReviewDetailsViewModel(
            stylist.StylistId,
            stylist.FullName,
            stylist.IsActive,
            count,
            average,
            items
        );

        return View(viewModel);
    }
}

public sealed record StylistReviewSummary(
    int StylistId,
    string FullName,
    int ReviewCount,
    double? AverageRating,
    bool IsActive = true)
{
    public string RatingDisplay => ReviewCount < StylistReviewsController.MinimumReviewThreshold
        ? $"Chưa đủ dữ liệu ({ReviewCount}/{StylistReviewsController.MinimumReviewThreshold} lượt)"
        : $"{AverageRating?.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)} / 5";

    public bool HasSufficientReviews => ReviewCount >= StylistReviewsController.MinimumReviewThreshold;
}

public sealed record StylistReviewItem(
    int ReviewId,
    int Rating,
    string? Comment,
    string CustomerName,
    string BookingReference,
    DateTime SubmittedAt);

public sealed record StylistReviewDetailsViewModel(
    int StylistId,
    string FullName,
    bool IsActive,
    int ReviewCount,
    double? AverageRating,
    IReadOnlyList<StylistReviewItem> RecentReviews)
{
    public string RatingDisplay => ReviewCount < StylistReviewsController.MinimumReviewThreshold
        ? $"Chưa đủ dữ liệu ({ReviewCount}/{StylistReviewsController.MinimumReviewThreshold} lượt)"
        : $"{AverageRating?.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)} / 5";

    public bool HasSufficientReviews => ReviewCount >= StylistReviewsController.MinimumReviewThreshold;
}
