using Microsoft.VisualStudio.TestTools.UnitTesting;
using SalonManagement.Models;

namespace SalonManagement.Tests;

/// <summary>
/// Unit tests kiểm tra logic RefreshToken entity — phục vụ AC3.
/// </summary>
[TestClass]
public class RefreshTokenTests
{
    // ── AC3-01: Token còn hạn và chưa bị thu hồi là hợp lệ ──────────────────

    [TestMethod]
    [Description("AC3: Refresh token còn hạn và chưa bị thu hồi phải là hợp lệ")]
    public void IsValid_NotExpiredAndNotRevoked_ReturnsTrue()
    {
        var now = DateTime.UtcNow;
        var token = new RefreshToken
        {
            TokenHash = "hash",
            UserId = "user1",
            ExpiresAtUtc = now.AddDays(7),
            CreatedAtUtc = now,
            RevokedAtUtc = null   // chưa thu hồi
        };

        Assert.IsTrue(token.IsValid(now), "Token còn hạn, chưa bị thu hồi phải là valid");
    }

    // ── AC3-02: Token đã hết hạn phải là không hợp lệ ────────────────────────

    [TestMethod]
    [Description("AC3: Refresh token đã hết hạn phải là không hợp lệ")]
    public void IsValid_Expired_ReturnsFalse()
    {
        var now = DateTime.UtcNow;
        var token = new RefreshToken
        {
            TokenHash = "hash",
            UserId = "user1",
            ExpiresAtUtc = now.AddDays(-1),   // hết hạn 1 ngày trước
            CreatedAtUtc = now.AddDays(-8),
            RevokedAtUtc = null
        };

        Assert.IsFalse(token.IsValid(now), "Token đã hết hạn phải là invalid");
    }

    // ── AC3-03: Token bị thu hồi phải là không hợp lệ ────────────────────────

    [TestMethod]
    [Description("AC3: Refresh token đã bị thu hồi (RevokedAtUtc != null) phải là không hợp lệ")]
    public void IsValid_Revoked_ReturnsFalse()
    {
        var now = DateTime.UtcNow;
        var token = new RefreshToken
        {
            TokenHash = "hash",
            UserId = "user1",
            ExpiresAtUtc = now.AddDays(7),
            CreatedAtUtc = now,
            RevokedAtUtc = now  // đã bị thu hồi
        };

        Assert.IsFalse(token.IsValid(now), "Token bị thu hồi phải là invalid");
    }

    // ── AC3-04: Token vừa hết hạn vừa bị thu hồi phải là không hợp lệ ────────

    [TestMethod]
    [Description("AC3: Token vừa hết hạn vừa bị thu hồi cũng phải là không hợp lệ")]
    public void IsValid_ExpiredAndRevoked_ReturnsFalse()
    {
        var now = DateTime.UtcNow;
        var token = new RefreshToken
        {
            TokenHash = "hash",
            UserId = "user1",
            ExpiresAtUtc = now.AddDays(-2),
            CreatedAtUtc = now.AddDays(-9),
            RevokedAtUtc = now.AddDays(-1)
        };

        Assert.IsFalse(token.IsValid(now), "Token hết hạn và bị thu hồi phải là invalid");
    }

    // ── AC3-05: Token thu hồi phải được phân biệt theo RevokedAtUtc ──────────

    [TestMethod]
    [Description("AC3: RevokedAtUtc null vs có giá trị phân biệt rõ trạng thái thu hồi")]
    public void IsValid_RevokedAtUtcBoundary_CorrectBehavior()
    {
        var now = DateTime.UtcNow;

        var notRevoked = new RefreshToken
        {
            TokenHash = "hash1", UserId = "u",
            ExpiresAtUtc = now.AddHours(1),
            CreatedAtUtc = now.AddMinutes(-5),
            RevokedAtUtc = null
        };

        var revoked = new RefreshToken
        {
            TokenHash = "hash2", UserId = "u",
            ExpiresAtUtc = now.AddHours(1),
            CreatedAtUtc = now.AddMinutes(-5),
            RevokedAtUtc = now.AddSeconds(-1)
        };

        Assert.IsTrue(notRevoked.IsValid(now), "Chưa thu hồi phải valid");
        Assert.IsFalse(revoked.IsValid(now), "Đã thu hồi phải invalid dù chưa hết hạn");
    }
}
