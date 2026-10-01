using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using SalonManagement.Models;

namespace SalonManagement.Services;

public sealed class SessionPrincipalValidator(UserManager<ApplicationUser> users)
{
    public async Task<bool> ValidateAsync(ClaimsPrincipal principal)
    {
        var id = principal.FindFirstValue("sub");
        var user = id == null ? null : await users.FindByIdAsync(id);
        if (user == null || !user.IsActive || string.IsNullOrEmpty(user.SecurityStamp) ||
            principal.FindFirstValue("security_stamp") != user.SecurityStamp) return false;
        var roles = await users.GetRolesAsync(user);
        if (roles.Count != 1) return false;
        if (principal.Identity is not ClaimsIdentity identity) return false;
        foreach (var claim in identity.FindAll(identity.RoleClaimType).ToList()) identity.RemoveClaim(claim);
        identity.AddClaim(new Claim(identity.RoleClaimType, roles[0]));
        foreach (var claim in identity.FindAll(ClaimTypes.NameIdentifier).ToList()) identity.RemoveClaim(claim);
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, user.Id));
        foreach (var claim in identity.FindAll("must_change_password").ToList()) identity.RemoveClaim(claim);
        if (user.MustChangePassword) identity.AddClaim(new Claim("must_change_password", "true"));
        return true;
    }
}
