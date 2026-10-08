using Microsoft.AspNetCore.Identity;

namespace SalonManagement.Models;

public class ApplicationUser : IdentityUser
{
    public int? StylistId { get; set; }
    public Stylist? Stylist { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}
