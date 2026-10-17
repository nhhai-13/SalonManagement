using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;
using SalonManagement.Models;

namespace SalonManagement.Services;

/// <summary>
/// Phát hành và kiểm tra mã OTP cho quên mật khẩu. Mã gốc không được lưu;
/// chỉ bản băm cùng thời hạn ngắn được giữ trong cache của máy chủ.
/// </summary>
public sealed class PasswordResetCodeService(
    IMemoryCache cache,
    UserManager<ApplicationUser> userManager,
    IEmailService emailService,
    TimeProvider timeProvider,
    ILogger<PasswordResetCodeService> logger)
{
    private const int CodeLifetimeMinutes = 10;
    private const int MaximumAttempts = 5;
    private readonly PasswordHasher<ApplicationUser> codeHasher = new();

    public async Task SendAsync(ApplicationUser user)
    {
        if (string.IsNullOrWhiteSpace(user.Email))
        {
            throw new InvalidOperationException("Tài khoản này chưa có địa chỉ email để nhận mã xác thực.");
        }

        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        var resetToken = await userManager.GeneratePasswordResetTokenAsync(user);
        var expiresAt = timeProvider.GetUtcNow().AddMinutes(CodeLifetimeMinutes);
        var challenge = new PasswordResetChallenge(
            codeHasher.HashPassword(user, code), resetToken, expiresAt, 0);

        cache.Set(CacheKey(user.Email), challenge, expiresAt);
        try
        {
            await emailService.SendPasswordResetCodeAsync(user.Email, code);
            logger.LogInformation("Đã gửi mã đặt lại mật khẩu tới {Email}.", user.Email);
        }
        catch
        {
            cache.Remove(CacheKey(user.Email));
            throw;
        }
    }

    public PasswordResetCodeVerificationResult Verify(string email, string code)
    {
        var key = CacheKey(email);
        if (!cache.TryGetValue<PasswordResetChallenge>(key, out var challenge) || challenge is null)
        {
            return PasswordResetCodeVerificationResult.Failed("Mã xác nhận không tồn tại hoặc đã hết hạn. Vui lòng yêu cầu mã mới.");
        }

        if (timeProvider.GetUtcNow() > challenge.ExpiresAt)
        {
            cache.Remove(key);
            return PasswordResetCodeVerificationResult.Failed("Mã xác nhận đã hết hạn. Vui lòng yêu cầu mã mới.");
        }

        if (challenge.FailedAttempts >= MaximumAttempts)
        {
            cache.Remove(key);
            return PasswordResetCodeVerificationResult.Failed("Bạn đã nhập sai mã quá nhiều lần. Vui lòng yêu cầu mã mới.");
        }

        // Dùng một đối tượng tối thiểu vì PasswordHasher chỉ cần định danh kiểu user.
        var hashCheck = codeHasher.VerifyHashedPassword(new ApplicationUser(), challenge.CodeHash, code);
        if (hashCheck is PasswordVerificationResult.Failed)
        {
            cache.Set(key, challenge with { FailedAttempts = challenge.FailedAttempts + 1 }, challenge.ExpiresAt);
            return PasswordResetCodeVerificationResult.Failed("Mã xác nhận chưa đúng. Vui lòng kiểm tra lại.");
        }

        cache.Remove(key);
        return PasswordResetCodeVerificationResult.FromSuccess(challenge.ResetToken);
    }

    private static string CacheKey(string email) => $"password-reset-otp:{email.Trim().ToUpperInvariant()}";

    private sealed record PasswordResetChallenge(
        string CodeHash,
        string ResetToken,
        DateTimeOffset ExpiresAt,
        int FailedAttempts);
}

public sealed record PasswordResetCodeVerificationResult(bool Succeeded, string? ResetToken, string? ErrorMessage)
{
    public static PasswordResetCodeVerificationResult Failed(string error) => new(false, null, error);
    public static PasswordResetCodeVerificationResult FromSuccess(string token) => new(true, token, null);
}
