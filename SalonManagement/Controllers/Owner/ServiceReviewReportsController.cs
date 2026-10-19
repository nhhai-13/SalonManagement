using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalonManagement.Data;
using SalonManagement.Models;

namespace SalonManagement.Controllers.Owner;

[Authorize(Roles = UserRoles.Owner)]
public sealed class ServiceReviewReportsPageController : Controller
{
    [HttpGet("owner/reviews")]
    public IActionResult Index() => View("~/Views/ServiceReviewReports/Index.cshtml");
}

[Authorize(Roles = UserRoles.Owner)]
[ApiController]
[Route("api/owner/reviews")]
public sealed class ServiceReviewReportsController(ApplicationDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var stylists = await db.Stylists.AsNoTracking()
            .OrderBy(item => item.FullName)
            .Select(item => new { item.StylistId, item.FullName, item.Specialty })
            .ToListAsync();
        var submitted = await db.ServiceReviews.AsNoTracking()
            .Where(item => item.SubmittedAt != null && item.Rating != null)
            .Include(item => item.Appointment)
            .ToListAsync();
        var reviewGroups = submitted.GroupBy(item => item.Appointment.StylistId)
            .ToDictionary(group => group.Key, group => new { Count = group.Count(), Average = group.Average(item => item.Rating!.Value) });

        return Ok(stylists.Select(stylist =>
        {
            reviewGroups.TryGetValue(stylist.StylistId, out var statistics);
            var count = statistics?.Count ?? 0;
            var hasEnoughData = count >= 5;
            return new ServiceReviewReportRow(
                stylist.StylistId,
                stylist.FullName,
                stylist.Specialty ?? "Chưa cập nhật chuyên môn",
                count,
                hasEnoughData ? (decimal?)Math.Round(statistics!.Average, 1, MidpointRounding.AwayFromZero) : null,
                hasEnoughData);
        }));
    }
}

public sealed record ServiceReviewReportRow(int StylistId, string StylistName, string Specialty, int ReviewCount, decimal? AverageRating, bool HasEnoughData);
