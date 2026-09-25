namespace SalonManagement.Models;

/// <summary>Refresh Token lưu dưới dạng hash SHA-256 trong database (AC3).</summary>
public class RefreshToken
{
    public int RefreshTokenId { get; set; }

    /// <summary>SHA-256 hash của token gốc — không lưu plaintext.</summary>
    public required string TokenHash { get; set; }

    public DateTime ExpiresAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }

    public required string UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;

    /// <summary>Token còn hiệu lực khi chưa bị thu hồi và chưa hết hạn.</summary>
    public bool IsValid(DateTime utcNow) => RevokedAtUtc is null && ExpiresAtUtc > utcNow;
}
