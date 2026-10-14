using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalonManagement.Data;
using SalonManagement.Models;

namespace SalonManagement.Controllers.Owner;

[Authorize(Roles = UserRoles.Owner)]
[Route("owner/stylist-reviews")]
public sealed class StylistReviewsController(ApplicationDbContext db) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var stylists = await db.Stylists.AsNoTracking()
            .OrderBy(item => item.FullName)
            .Select(item => new { item.StylistId, item.FullName })
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
            return new StylistReviewSummary(stylist.StylistId, stylist.FullName, count,
                count >= 5 ? Math.Round(rating!.Average, 1, MidpointRounding.AwayFromZero) : null);
        }).ToList();
        return View(rows);
    }
}

public sealed record StylistReviewSummary(int StylistId, string FullName, int ReviewCount, double? AverageRating);
