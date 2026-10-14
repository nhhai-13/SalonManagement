using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;

namespace SalonManagement.Services;

public sealed class BookingEmailVerificationService(
    IMemoryCache cache,
    IEmailService emailService,
    TimeProvider timeProvider)
{
    private const int LifetimeMinutes = 10;
    private const int MaxAttempts = 5;

    private sealed record Challenge(string CodeHash, DateTimeOffset ExpiresAt, int FailedAttempts, bool Verified);

    private static string CacheKey(string email) => $"booking-email-verification:{email.Trim().ToUpperInvariant()}";
    private static string Normalize(string email) => email.Trim().ToLowerInvariant();

    public async Task<(bool Accepted, string Message)> SendAsync(string? email)
    {
        var normalized = Normalize(email ?? string.Empty);
        if (!new EmailAddressAttribute().IsValid(normalized))
            return (false, "Vui lòng nhập email hợp lệ để nhận mã xác thực.");

        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        var hash = new PasswordHasher<BookingEmailVerificationService>().HashPassword(this, code);
        var expiresAt = timeProvider.GetUtcNow().AddMinutes(LifetimeMinutes);
        cache.Set(CacheKey(normalized), new Challenge(hash, expiresAt, 0, false), expiresAt);
        await emailService.SendEmailVerificationCodeAsync(normalized, code);
        return (true, "Mã xác thực gồm 6 chữ số đã được gửi về email của bạn.");
    }

    public (bool Verified, string Message) Verify(string? email, string? code)
    {
        var normalized = Normalize(email ?? string.Empty);
        if (!cache.TryGetValue<Challenge>(CacheKey(normalized), out var challenge) || challenge is null || challenge.ExpiresAt <= timeProvider.GetUtcNow())
            return (false, "Mã xác thực đã hết hạn hoặc chưa được gửi. Vui lòng gửi lại mã.");
        if (challenge.FailedAttempts >= MaxAttempts)
            return (false, "Bạn đã nhập sai mã quá nhiều lần. Vui lòng gửi lại mã mới.");
        if (string.IsNullOrWhiteSpace(code) || code.Length != 6 || !code.All(char.IsDigit) ||
            new PasswordHasher<BookingEmailVerificationService>().VerifyHashedPassword(this, challenge.CodeHash, code) == PasswordVerificationResult.Failed)
        {
            cache.Set(CacheKey(normalized), challenge with { FailedAttempts = challenge.FailedAttempts + 1 }, challenge.ExpiresAt);
            return (false, "Mã xác thực không đúng.");
        }
        cache.Set(CacheKey(normalized), challenge with { Verified = true }, challenge.ExpiresAt);
        return (true, "Email đã được xác thực.");
    }

    public bool IsVerified(string? email) =>
        cache.TryGetValue<Challenge>(CacheKey(Normalize(email ?? string.Empty)), out var challenge) && challenge is not null &&
        challenge.Verified && challenge.ExpiresAt > timeProvider.GetUtcNow();

    public void Consume(string? email) => cache.Remove(CacheKey(Normalize(email ?? string.Empty)));
}
