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
        var email = User.FindFirstValue(ClaimTypes.Email) ?? User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(email)) return Unauthorized();
        var since = SalonClock.GetLocalNow(timeProvider).ToUniversalTime().AddDays(-30);
        var items = await db.StaffNotifications.AsNoTracking().Where(item => item.RecipientEmail == email && item.CreatedAt >= since)
            .OrderByDescending(item => item.CreatedAt).Take(20).ToListAsync();
        return Ok(new { unread = items.Count(item => item.ReadAt is null), items });
    }

    [HttpPost("{notificationId:int}/read")]
    public async Task<IActionResult> Read(int notificationId)
    {
        var email = User.FindFirstValue(ClaimTypes.Email) ?? User.Identity?.Name;
        var item = await db.StaffNotifications.SingleOrDefaultAsync(notification => notification.StaffNotificationId == notificationId && notification.RecipientEmail == email);
        if (item is null) return NotFound();
        item.ReadAt ??= DateTime.UtcNow;
        await db.SaveChangesAsync();
        return NoContent();
    }
}
