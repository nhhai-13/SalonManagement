using Microsoft.AspNetCore.Identity;

namespace SalonManagement.Models;

public class ApplicationUser : IdentityUser
{
    public bool IsActive { get; set; } = true;

    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}
