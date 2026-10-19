using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalonManagement.Data;
using SalonManagement.Models;
using SalonManagement.Services;

namespace SalonManagement.Controllers;

[Authorize(Roles = RoleGroups.Management)]
[Route("management/access-requests")]
public sealed class AccessRequestsController(UserManager<ApplicationUser> userManager, ApplicationDbContext db) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var requests = await userManager.Users
            .Where(user => user.EmailConfirmed && !user.IsActive &&
                (user.RequestedRole == UserRoles.Receptionist || user.RequestedRole == UserRoles.Stylist))
            .OrderBy(user => user.AccessRequestedAtUtc)
            .Select(user => new PendingAccessRequest(
                user.Id,
                user.FullName,
                user.Email ?? string.Empty,
                user.PhoneNumber,
                user.RequestedRole!,
                user.AccessRequestedAtUtc))
            .ToListAsync();

        return View(requests);
    }

    [HttpPost("{id}/approve")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(string id)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user is null || !user.EmailConfirmed || user.IsActive || !AuthController.IsSelfRegistrationRole(user.RequestedRole))
        {
            TempData["AccessRequestError"] = "Yêu cầu này không còn hợp lệ hoặc đã được xử lý.";
            return RedirectToAction(nameof(Index));
        }

        var existingRoles = await userManager.GetRolesAsync(user);
        if (existingRoles.Count > 0)
        {
            TempData["AccessRequestError"] = "Tài khoản đã có quyền khác nên không thể tự động cấp lại.";
            return RedirectToAction(nameof(Index));
        }

        var role = user.RequestedRole!;
        var roleResult = await userManager.AddToRoleAsync(user, role);
        if (!roleResult.Succeeded)
        {
            TempData["AccessRequestError"] = "Không thể cấp quyền. Vui lòng thử lại.";
            return RedirectToAction(nameof(Index));
        }

        user.IsActive = true;
        user.RequestedRole = null;
        user.AccessRequestedAtUtc = null;
        var updateResult = await userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            await userManager.RemoveFromRoleAsync(user, role);
            TempData["AccessRequestError"] = "Không thể hoàn tất việc cấp quyền. Vui lòng thử lại.";
            return RedirectToAction(nameof(Index));
        }

        if (role == UserRoles.Stylist)
        {
            await StylistProfileSynchronizer.EnsureForUserAsync(db, user);
            await db.SaveChangesAsync();
        }

        TempData["AccessRequestSuccess"] = $"Đã cấp quyền {RoleLabel(role)} cho {user.Email}.";
        return RedirectToAction(nameof(Index));
    }

    private static string RoleLabel(string role) => role == UserRoles.Stylist ? "Thợ" : "Lễ tân";

    public sealed record PendingAccessRequest(
        string Id,
        string FullName,
        string Email,
        string? PhoneNumber,
        string RequestedRole,
        DateTime? RequestedAtUtc);
}
