using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalonManagement.Data;
using SalonManagement.Models;
using SalonManagement.Services;

namespace SalonManagement.Controllers.StylistPortal;

[Authorize(Roles = UserRoles.Stylist)]
[ApiController]
[Route("api/stylist/notifications")]
public sealed class StylistNotificationsController(ApplicationDbContext db, TimeProvider timeProvider) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var email = CurrentUserEmail();
        if (string.IsNullOrWhiteSpace(email)) return Unauthorized();
        var since = SalonClock.GetLocalNow(timeProvider).ToUniversalTime().AddDays(-30);
        var items = await db.StaffNotifications.AsNoTracking().Where(item => item.RecipientEmail == email && item.CreatedAt >= since)
            .OrderByDescending(item => item.CreatedAt).Take(20).ToListAsync();
        return Ok(new { unread = items.Count(item => item.ReadAt is null), items });
    }

    [HttpPost("{notificationId:int}/read")]
    public async Task<IActionResult> Read(int notificationId)
    {
        var email = CurrentUserEmail();
        var item = await db.StaffNotifications.SingleOrDefaultAsync(notification => notification.StaffNotificationId == notificationId && notification.RecipientEmail == email);
        if (item is null) return NotFound();
        item.ReadAt ??= DateTime.UtcNow;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> ReadAll()
    {
        var email = CurrentUserEmail();
        if (string.IsNullOrWhiteSpace(email)) return Unauthorized();
        var now = SalonClock.GetLocalNow(timeProvider).ToUniversalTime();
        var unread = await db.StaffNotifications
            .Where(notification => notification.RecipientEmail == email && notification.ReadAt == null)
            .ToListAsync();
        foreach (var item in unread) item.ReadAt = now;
        await db.SaveChangesAsync();
        return Ok(new { markedRead = unread.Count });
    }

    private string? CurrentUserEmail() =>
        User.FindFirstValue(JwtRegisteredClaimNames.Email) ?? User.FindFirstValue(ClaimTypes.Email);
}
