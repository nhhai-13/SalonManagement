using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalonManagement.Data;
using SalonManagement.Models;
using SalonManagement.Services;
using System.Text.Json;

namespace SalonManagement.Controllers;
[Authorize(Roles = UserRoles.Stylist, AuthenticationSchemes = "Identity.Application")]
public class StylistNotificationsController(ApplicationDbContext db, AppointmentNotificationBus notifications) : Controller
{
    [HttpGet]
    public async Task Stream()
    {
        var token = HttpContext.RequestAborted;
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var stylistId = await db.Users.Where(u => u.Id == userId).Select(u => u.StylistId).SingleOrDefaultAsync(token);
        if (!stylistId.HasValue) { Response.StatusCode = 409; return; }
        var subscription = notifications.Subscribe(stylistId.Value);
        try
        {
            Response.ContentType = "text/event-stream";
            Response.Headers.CacheControl = "no-cache";
            Response.Headers["X-Accel-Buffering"] = "no";
            await Response.WriteAsync(": connected\n\n", token);
            await Response.Body.FlushAsync(token);
            while (!token.IsCancellationRequested)
            {
                using var heartbeat = CancellationTokenSource.CreateLinkedTokenSource(token);
                heartbeat.CancelAfter(TimeSpan.FromSeconds(20));
                try
                {
                    var notification = await subscription.Reader.ReadAsync(heartbeat.Token);
                    var currentUser = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == userId, token);
                    var ticket = await HttpContext.AuthenticateAsync("Identity.Application");
                    if (currentUser == null || !currentUser.IsActive || currentUser.StylistId != stylistId ||
                        currentUser.SecurityStamp != User.FindFirstValue(HttpContext.RequestServices.GetRequiredService<Microsoft.Extensions.Options.IOptions<IdentityOptions>>().Value.ClaimsIdentity.SecurityStampClaimType) ||
                        ticket.Properties?.ExpiresUtc <= DateTimeOffset.UtcNow) break;
                    await Response.WriteAsync("data: " + JsonSerializer.Serialize(new { notification.Id, notification.Message, notification.CreatedAt }) + "\n\n", token);
                }
                catch (OperationCanceledException) when (!token.IsCancellationRequested) { await Response.WriteAsync(": heartbeat\n\n", token); }
                await Response.Body.FlushAsync(token);
                // Terminate streams if the account is deactivated, reassigned or the cookie expires.
                var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == userId, token);
                var auth = await HttpContext.AuthenticateAsync("Identity.Application");
                if (user == null || !user.IsActive || user.StylistId != stylistId ||
                    user.SecurityStamp != User.FindFirstValue(HttpContext.RequestServices.GetRequiredService<Microsoft.Extensions.Options.IOptions<IdentityOptions>>().Value.ClaimsIdentity.SecurityStampClaimType) ||
                    auth.Properties?.ExpiresUtc <= DateTimeOffset.UtcNow) break;
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        finally { notifications.Unsubscribe(subscription.Id); }
    }

    [HttpGet]
    public async Task<IActionResult> Latest()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var stylistId = await db.Users.Where(u => u.Id == userId).Select(u => u.StylistId).SingleOrDefaultAsync();
        if (!stylistId.HasValue) return Conflict(new { message = "Tài khoản chưa liên kết hồ sơ thợ." });
        var items = await db.StylistNotifications.AsNoTracking().Where(n => n.StylistId == stylistId)
            .OrderByDescending(n => n.Id).Take(100).Select(n => new { n.Id, n.Message, n.CreatedAt }).ToListAsync();
        return Json(items);
    }

    public async Task<IActionResult> Index()
    {
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var stylistId = await db.Users.Where(u => u.Id == id).Select(u => u.StylistId).SingleOrDefaultAsync();
        if (stylistId == null) return View(new List<StylistNotification>());
        return View(await db.StylistNotifications.AsNoTracking().Where(n => n.StylistId == stylistId)
            .OrderByDescending(n => n.Id).Take(100).ToListAsync());
    }
}
