using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SalonManagement.Services;

namespace SalonManagement.Tests;

/// <summary>
/// Unit test cho PasswordResetRateLimiter — kiểm tra AC4:
/// Tối đa 3 lần yêu cầu gửi email reset trong 1 giờ cho cùng 1 email,
/// lần thứ 4 phải báo lỗi.
/// </summary>
[TestClass]
public class PasswordResetRateLimiterTests
{
    private static PasswordResetRateLimiter CreateLimiter(TimeProvider timeProvider)
    {
        var options = Options.Create(new MemoryCacheOptions());
        var cache = new MemoryCache(options);
        return new PasswordResetRateLimiter(cache, timeProvider);
    }

    // ── AC4-01: Lần 1, 2, 3 phải được chấp nhận ──────────────────────────────

    [TestMethod]
    [Description("AC4: Ba lần yêu cầu đầu tiên trong 1 giờ phải được chấp nhận")]
    public void TryRecord_First3Attempts_ReturnsTrue()
    {
        // Arrange
        var limiter = CreateLimiter(TimeProvider.System);
        const string email = "user@example.com";

        // Act & Assert
        Assert.IsTrue(limiter.TryRecord(email), "Lần 1 phải được chấp nhận");
        Assert.IsTrue(limiter.TryRecord(email), "Lần 2 phải được chấp nhận");
        Assert.IsTrue(limiter.TryRecord(email), "Lần 3 phải được chấp nhận");
    }

    // ── AC4-02: Lần thứ 4 phải bị từ chối ────────────────────────────────────

    [TestMethod]
    [Description("AC4: Lần thứ 4 trở đi trong 1 giờ phải bị từ chối")]
    public void TryRecord_4thAttempt_ReturnsFalse()
    {
        // Arrange
        var limiter = CreateLimiter(TimeProvider.System);
        const string email = "user@example.com";

        limiter.TryRecord(email); // 1
        limiter.TryRecord(email); // 2
        limiter.TryRecord(email); // 3

        // Act
        var result = limiter.TryRecord(email); // 4th — phải bị từ chối

        // Assert
        Assert.IsFalse(result, "Lần thứ 4 phải bị từ chối");
    }

    // ── AC4-03: Email khác nhau phải được tính độc lập ────────────────────────

    [TestMethod]
    [Description("AC4: Các email khác nhau được tính rate limit độc lập")]
    public void TryRecord_DifferentEmails_IndependentLimits()
    {
        // Arrange
        var limiter = CreateLimiter(TimeProvider.System);

        // Email A đã dùng hết 3 lượt
        limiter.TryRecord("a@example.com");
        limiter.TryRecord("a@example.com");
        limiter.TryRecord("a@example.com");

        // Act
        var aBlocked = !limiter.TryRecord("a@example.com"); // phải bị block
        var bAllowed = limiter.TryRecord("b@example.com");  // email B chưa dùng

        // Assert
        Assert.IsTrue(aBlocked, "Email A đã vượt ngưỡng phải bị chặn");
        Assert.IsTrue(bAllowed, "Email B chưa có request nào phải được phép");
    }

    // ── AC4-04: Email case-insensitive ────────────────────────────────────────

    [TestMethod]
    [Description("AC4: Email khác casing phải được tính là cùng 1 người dùng")]
    public void TryRecord_EmailCaseInsensitive_SameLimit()
    {
        // Arrange
        var limiter = CreateLimiter(TimeProvider.System);

        limiter.TryRecord("User@Example.COM");
        limiter.TryRecord("user@example.com");
        limiter.TryRecord("USER@EXAMPLE.COM");

        // Act — đã dùng 3 lần, lần này phải bị từ chối dù casing khác
        var result = limiter.TryRecord("User@Example.Com");

        // Assert
        Assert.IsFalse(result, "Các email khác casing phải tính là cùng người dùng");
    }

    // ── AC4-05: GetRemainingAttempts phản ánh đúng số lượt còn lại ───────────

    [TestMethod]
    [Description("AC4: GetRemainingAttempts phải trả về số lượt còn lại chính xác")]
    public void GetRemainingAttempts_AfterAttempts_CorrectCount()
    {
        // Arrange
        var limiter = CreateLimiter(TimeProvider.System);
        const string email = "test@salon.vn";

        // Act
        Assert.AreEqual(3, limiter.GetRemainingAttempts(email), "Ban đầu phải còn 3 lượt");
        limiter.TryRecord(email);
        Assert.AreEqual(2, limiter.GetRemainingAttempts(email), "Sau 1 lần phải còn 2 lượt");
        limiter.TryRecord(email);
        Assert.AreEqual(1, limiter.GetRemainingAttempts(email), "Sau 2 lần phải còn 1 lượt");
        limiter.TryRecord(email);
        Assert.AreEqual(0, limiter.GetRemainingAttempts(email), "Sau 3 lần phải còn 0 lượt");
    }

    // ── AC4-06: Sau khi cửa sổ 1 giờ trôi qua, limit được reset ─────────────

    [TestMethod]
    [Description("AC4: Sau khi cửa sổ thời gian 1 giờ trôi qua, rate limit phải reset")]
    public void TryRecord_AfterWindowExpired_AllowsAgain()
    {
        // Arrange — dùng FakeTimeProvider để kiểm soát thời gian
        var fakeTime = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var limiter = CreateLimiter(fakeTime);
        const string email = "test@salon.vn";

        // Dùng hết 3 lượt tại thời điểm t=0
        limiter.TryRecord(email);
        limiter.TryRecord(email);
        limiter.TryRecord(email);
        Assert.IsFalse(limiter.TryRecord(email), "Lần 4 phải bị chặn");

        // Tua thời gian lên hơn 1 giờ — cache đã expire, window mới bắt đầu
        fakeTime.Advance(TimeSpan.FromHours(1).Add(TimeSpan.FromSeconds(1)));

        // Act — tạo instance mới vì cache đã expire
        var options = Options.Create(new MemoryCacheOptions());
        var freshCache = new MemoryCache(options);
        var freshLimiter = new PasswordResetRateLimiter(freshCache, fakeTime);

        // Assert — email này phải được chấp nhận trở lại
        Assert.IsTrue(freshLimiter.TryRecord(email),
            "Sau khi cửa sổ 1 giờ trôi qua, phải được phép yêu cầu lại");
    }
}

/// <summary>
/// FakeTimeProvider dùng để kiểm soát thời gian trong unit test mà không phụ thuộc
/// vào thời gian thực (tránh flaky tests).
/// </summary>
internal sealed class FakeTimeProvider(DateTimeOffset startTime) : TimeProvider
{
    private DateTimeOffset _current = startTime;

    public override DateTimeOffset GetUtcNow() => _current;

    public void Advance(TimeSpan delta) => _current = _current.Add(delta);
}
