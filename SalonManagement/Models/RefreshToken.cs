namespace SalonManagement.Models;

public class RefreshToken
{
    public int RefreshTokenId { get; set; }
    public required string TokenHash { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public required string UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;

    public bool IsValid(DateTime utcNow) => RevokedAtUtc is null && ExpiresAtUtc > utcNow;
}
