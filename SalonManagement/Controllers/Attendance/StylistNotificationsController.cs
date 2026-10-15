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
[Authorize(Roles = UserRoles.Stylist)]
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
                    if (!await CanReceive(userId, stylistId.Value, token)) break;
                    var reference = await db.Appointments.AsNoTracking().Where(appointment => appointment.AppointmentId == notification.AppointmentId)
                        .Select(appointment => appointment.BookingReference).SingleOrDefaultAsync(token);
                    var url = string.IsNullOrWhiteSpace(reference)
                        ? Url.Action("Index", "StylistNotifications")
                        : Url.Action("Index", "AppointmentLookup", new { reference });
                    await Response.WriteAsync("data: " + JsonSerializer.Serialize(new { notification.Id, notification.Message, notification.CreatedAt, notification.IsRead, notification.AppointmentId, Url = url }) + "\n\n", token);
                }
                catch (OperationCanceledException) when (!token.IsCancellationRequested) { await Response.WriteAsync(": heartbeat\n\n", token); }
                await Response.Body.FlushAsync(token);
                if (!await CanReceive(userId, stylistId.Value, token)) break;
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        finally { notifications.Unsubscribe(subscription.Id); }
    }

    private async Task<bool> CanReceive(string? userId, int stylistId, CancellationToken token)
    {
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == userId, token);
        var validator = HttpContext.RequestServices.GetRequiredService<SessionPrincipalValidator>();
        return user != null && user.StylistId == stylistId && await validator.ValidateAsync(User) &&
            User.IsInRole(UserRoles.Stylist) && long.TryParse(User.FindFirstValue("exp"), out var expires) &&
            DateTimeOffset.UtcNow.ToUnixTimeSeconds() < expires;
    }

    [HttpGet]
    public async Task<IActionResult> Latest()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var stylistId = await db.Users.Where(u => u.Id == userId).Select(u => u.StylistId).SingleOrDefaultAsync();
        if (!stylistId.HasValue) return Conflict(new { message = "Tài khoản chưa liên kết hồ sơ thợ." });
        var cutoff = DateTimeOffset.UtcNow.AddDays(-30);
        var items = await db.StylistNotifications.AsNoTracking().Where(n => n.StylistId == stylistId && n.CreatedAt >= cutoff)
            .OrderByDescending(n => n.Id).Take(100).Select(n => new { n.Id, n.Message, n.CreatedAt, n.IsRead, n.AppointmentId }).ToListAsync();
        var references = await db.Appointments.AsNoTracking().Where(appointment => items.Select(item => item.AppointmentId).Contains(appointment.AppointmentId))
            .Select(appointment => new { appointment.AppointmentId, appointment.BookingReference }).ToDictionaryAsync(item => item.AppointmentId, item => item.BookingReference);
        return Json(new { unreadCount = items.Count(item => !item.IsRead), items = items.Select(item => new {
            item.Id, item.Message, item.CreatedAt, item.IsRead, item.AppointmentId,
            Url = references.TryGetValue(item.AppointmentId, out var reference) && !string.IsNullOrWhiteSpace(reference)
                ? Url.Action("Index", "AppointmentLookup", new { reference }) : Url.Action("Index", "StylistNotifications")
        }) });
    }

    public async Task<IActionResult> Index()
    {
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var stylistId = await db.Users.Where(u => u.Id == id).Select(u => u.StylistId).SingleOrDefaultAsync();
        if (stylistId == null) return View(new List<StylistNotification>());
        var cutoff = DateTimeOffset.UtcNow.AddDays(-30);
        var items = await db.StylistNotifications.AsNoTracking().Where(n => n.StylistId == stylistId && n.CreatedAt >= cutoff)
            .OrderByDescending(n => n.Id).Take(100).ToListAsync();
        ViewBag.AppointmentReferences = await db.Appointments.AsNoTracking()
            .Where(appointment => items.Select(item => item.AppointmentId).Contains(appointment.AppointmentId))
            .Select(appointment => new { appointment.AppointmentId, appointment.BookingReference })
            .ToDictionaryAsync(item => item.AppointmentId, item => item.BookingReference);
        ViewBag.UnreadCount = items.Count(item => !item.IsRead);
        return View(items);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkRead(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var stylistId = await db.Users.Where(user => user.Id == userId).Select(user => user.StylistId).SingleOrDefaultAsync();
        if (!stylistId.HasValue) return Conflict();
        await db.StylistNotifications.Where(item => item.Id == id && item.StylistId == stylistId.Value && !item.IsRead)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.IsRead, true));
        return NoContent();
    }
}
