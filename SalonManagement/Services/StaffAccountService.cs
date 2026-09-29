using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SalonManagement.Data;
using SalonManagement.Models;

namespace SalonManagement.Services;

public sealed class StaffAccountService(
    UserManager<ApplicationUser> userManager,
    ApplicationDbContext dbContext,
    TimeProvider timeProvider) : IStaffAccountService
{
    private static readonly string[] StaffRoles =
        [UserRoles.Admin, UserRoles.Owner, UserRoles.Receptionist, UserRoles.Stylist];

    public async Task<StaffAccountListResponse> GetAccountsAsync(string? search, string? role, bool? isActive)
    {
        var query = userManager.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(user => user.Email != null && user.Email.Contains(term));
        }
        if (isActive.HasValue) query = query.Where(user => user.IsActive == isActive.Value);

        var users = await query.OrderBy(user => user.Email).ToListAsync();
        var items = new List<StaffAccountResponse>();
        foreach (var user in users)
        {
            var roles = await userManager.GetRolesAsync(user);
            var currentRole = roles.FirstOrDefault(StaffRoles.Contains);
            if (currentRole is null || (!string.IsNullOrWhiteSpace(role) && !currentRole.Equals(role, StringComparison.OrdinalIgnoreCase))) continue;
            items.Add(new StaffAccountResponse(
                user.Id,
                user.Email ?? string.Empty,
                user.PhoneNumber,
                currentRole,
                user.IsActive,
                await userManager.IsLockedOutAsync(user)));
        }
        return new StaffAccountListResponse(items, items.Count);
    }

    public async Task<(bool Success, string Message)> UpdateAsync(string id, UpdateStaffAccountRequest request)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user is null) return (false, "Không tìm thấy tài khoản.");
        var role = NormalizeRole(request.Role);
        if (role is null) return (false, "Vai trò không hợp lệ.");
        var duplicate = await userManager.FindByEmailAsync(request.Email.Trim());
        if (duplicate is not null && duplicate.Id != id) return (false, "Email này đã được sử dụng.");

        var currentRoles = await userManager.GetRolesAsync(user);
        if (!currentRoles.Contains(role) && currentRoles.Contains(UserRoles.Admin) && !await CanRemoveAdminAsync(user.Id))
            return (false, "Không thể đổi vai trò của Admin cuối cùng đang hoạt động.");

        user.Email = request.Email.Trim();
        user.UserName = request.Email.Trim();
        user.PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim();
        var updateResult = await userManager.UpdateAsync(user);
        if (!updateResult.Succeeded) return (false, string.Join(" ", updateResult.Errors.Select(error => error.Description)));

        if (!currentRoles.Contains(role))
        {
            if (currentRoles.Count > 0) await userManager.RemoveFromRolesAsync(user, currentRoles);
            var roleResult = await userManager.AddToRoleAsync(user, role);
            if (!roleResult.Succeeded) return (false, "Không thể cập nhật vai trò.");
            await RevokeSessionsAsync(id);
        }
        return (true, "Đã cập nhật tài khoản.");
    }

    public async Task<(bool Success, string Message)> ChangeStatusAsync(string id, bool isActive)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user is null) return (false, "Không tìm thấy tài khoản.");
        if (!isActive && await userManager.IsInRoleAsync(user, UserRoles.Admin) && !await CanRemoveAdminAsync(id))
            return (false, "Không thể ngừng hoạt động Admin cuối cùng.");

        user.IsActive = isActive;
        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded) return (false, "Không thể cập nhật trạng thái tài khoản.");
        if (!isActive) await RevokeSessionsAsync(id);
        return (true, isActive ? "Đã kích hoạt tài khoản." : "Đã ngừng hoạt động và thu hồi phiên đăng nhập.");
    }

    public async Task<(bool Success, string Message)> RevokeSessionsAsync(string id)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user is null) return (false, "Không tìm thấy tài khoản.");
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var tokens = await dbContext.RefreshTokens.Where(token => token.UserId == id && token.RevokedAtUtc == null).ToListAsync();
        foreach (var token in tokens) token.RevokedAtUtc = now;
        await dbContext.SaveChangesAsync();
        await userManager.UpdateSecurityStampAsync(user);
        return (true, "Đã thu hồi toàn bộ phiên đăng nhập.");
    }

    private async Task<bool> CanRemoveAdminAsync(string excludedId)
    {
        var admins = await userManager.GetUsersInRoleAsync(UserRoles.Admin);
        return admins.Any(user => user.Id != excludedId && user.IsActive);
    }

    private static string? NormalizeRole(string? role) => role?.Trim().ToLowerInvariant() switch
    {
        "admin" => UserRoles.Admin,
        "owner" => UserRoles.Owner,
        "receptionist" or "reception" => UserRoles.Receptionist,
        "stylist" => UserRoles.Stylist,
        _ => null
    };
}
