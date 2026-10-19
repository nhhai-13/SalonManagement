using Microsoft.EntityFrameworkCore;
using SalonManagement.Data;
using SalonManagement.Models;

namespace SalonManagement.Services;

/// <summary>Keeps approved staff accounts in sync with the stylist profiles used
/// by booking and work-schedule screens.</summary>
public static class StylistProfileSynchronizer
{
    public static async Task<int> EnsureForActiveStylistAccountsAsync(ApplicationDbContext db)
    {
        var users = await (
            from user in db.Users
            join userRole in db.UserRoles on user.Id equals userRole.UserId
            join role in db.Roles on userRole.RoleId equals role.Id
            where user.IsActive && role.Name == UserRoles.Stylist
            select user).ToListAsync();

        var created = 0;
        foreach (var user in users)
        {
            if (await EnsureForUserAsync(db, user)) created++;
        }
        if (created > 0) await db.SaveChangesAsync();
        return created;
    }

    public static async Task<bool> EnsureForUserAsync(ApplicationDbContext db, ApplicationUser user)
    {
        var email = user.Email?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email)) return false;
        var profile = await db.Stylists.FirstOrDefaultAsync(stylist =>
            stylist.Email != null && stylist.Email.ToLower() == email);
        if (profile is not null)
        {
            if (!profile.IsActive)
            {
                profile.IsActive = true;
                profile.UpdatedAt = DateTime.Now;
                return true;
            }
            return false;
        }

        db.Stylists.Add(new Stylist
        {
            FullName = string.IsNullOrWhiteSpace(user.FullName) ? email : user.FullName.Trim(),
            Email = email,
            Phone = user.PhoneNumber?.Trim() ?? string.Empty,
            IsActive = true
        });
        return true;
    }
}
