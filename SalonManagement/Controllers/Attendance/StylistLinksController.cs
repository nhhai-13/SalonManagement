using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalonManagement.Data;
using SalonManagement.Models;

namespace SalonManagement.Controllers;
[Authorize(Roles = RoleGroups.Management)]
public class StylistLinksController(ApplicationDbContext db, UserManager<ApplicationUser> users) : Controller
{
    public async Task<IActionResult> Index()
    {
        ViewBag.Stylists = await db.Stylists.Where(s => s.IsActive).OrderBy(s => s.FullName).ToListAsync();
        return View((await users.GetUsersInRoleAsync(UserRoles.Stylist)).OrderBy(u => u.Email).ToList());
    }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Link(string userId, int? stylistId)
    {
        var user = await users.FindByIdAsync(userId);
        if (user == null || !await users.IsInRoleAsync(user, UserRoles.Stylist)) return BadRequest("Tài khoản phải có vai trò Thợ.");
        if (stylistId.HasValue && !await db.Stylists.AnyAsync(s => s.StylistId == stylistId && s.IsActive)) return BadRequest("Hồ sơ thợ không hợp lệ.");
        user.StylistId = stylistId;
        var result = await users.UpdateAsync(user);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded ? "Đã liên kết tài khoản thợ." : "Không thể cập nhật tài khoản.";
        return RedirectToAction(nameof(Index));
    }
}
