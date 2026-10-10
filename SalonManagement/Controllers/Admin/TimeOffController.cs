using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalonManagement.Data;
using SalonManagement.Models;

namespace SalonManagement.Controllers;

[Authorize(Roles = UserRoles.Owner)]
public sealed class TimeOffController(ApplicationDbContext db) : Controller
{
    [HttpGet("owner/time-off")]
    public async Task<IActionResult> Index()
    {
        ViewBag.Stylists = await db.Stylists.AsNoTracking()
            .Where(stylist => stylist.IsActive)
            .OrderBy(stylist => stylist.FullName)
            .Select(stylist => new { stylist.StylistId, stylist.FullName })
            .ToListAsync();
        return View();
    }
}
