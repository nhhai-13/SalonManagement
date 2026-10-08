using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalonManagement.Data;
using SalonManagement.Models;

namespace SalonManagement.Controllers;
[Authorize(Roles = UserRoles.Stylist, AuthenticationSchemes = "Identity.Application")]
public class StylistNotificationsController(ApplicationDbContext db) : Controller
{
    public async Task<IActionResult> Index()
    {
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var stylistId = await db.Users.Where(u => u.Id == id).Select(u => u.StylistId).SingleOrDefaultAsync();
        if (stylistId == null) return View(new List<StylistNotification>());
        return View(await db.StylistNotifications.AsNoTracking().Where(n => n.StylistId == stylistId)
            .OrderByDescending(n => n.Id).Take(100).ToListAsync());
    }
}
