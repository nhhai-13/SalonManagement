using System.Security.Cryptography;
using System.Text;

namespace SalonManagement.Models;

/// <summary>
/// Domain model encapsulating token lifecycle rules for appointment review invitations (S4-04).
/// </summary>
public sealed class ReviewInvitationToken
{
    public const int DefaultExpiryDays = 7;

    public string Token { get; set; } = string.Empty;
    public string TokenHash { get; set; } = string.Empty;
    public int AppointmentId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAtUtc { get; set; } = DateTime.UtcNow.AddDays(DefaultExpiryDays);
    public bool IsUsed { get; set; }
    public DateTime? UsedAtUtc { get; set; }

    public static string Generate() => Guid.NewGuid().ToString("N");

    public static string ComputeHash(string rawToken)
    {
        if (string.IsNullOrWhiteSpace(rawToken)) return string.Empty;
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
    }

    public static bool IsValid(DateTime nowUtc, DateTime expiresAtUtc, bool isUsed)
    {
        return !isUsed && nowUtc <= expiresAtUtc;
    }
}
