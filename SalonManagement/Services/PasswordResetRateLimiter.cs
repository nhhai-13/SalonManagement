using Microsoft.Extensions.Caching.Memory;

namespace SalonManagement.Services;

/// <summary>
/// Triển khai Rate Limiter dùng IMemoryCache (in-process) — phù hợp với phạm vi Sprint 1.
/// Quy tắc: tối đa <see cref="MaxAttempts"/> lần / giờ cho cùng 1 email (AC4).
/// Lần thứ 4 trở đi sẽ trả về false.
/// </summary>
public sealed class PasswordResetRateLimiter(
    IMemoryCache cache,
    TimeProvider timeProvider) : IPasswordResetRateLimiter
{
    private const int MaxAttempts = 3;
    private static readonly TimeSpan Window = TimeSpan.FromHours(1);

    private static string CacheKey(string email) =>
        $"ratelimit:pwreset:{email.Trim().ToUpperInvariant()}";

    /// <inheritdoc />
    public bool TryRecord(string email)
    {
        var key = CacheKey(email);
        var now = timeProvider.GetUtcNow();
        var timestamps = cache.GetOrCreate(key, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = Window;
            return new List<DateTimeOffset>();
        })!;

        // Loại bỏ timestamps cũ hơn cửa sổ 1 giờ
        timestamps.RemoveAll(t => now - t > Window);

        if (timestamps.Count >= MaxAttempts)
        {
            return false; // Đã vượt ngưỡng — từ chối
        }

        timestamps.Add(now);
        // Cập nhật lại cache (list thay đổi in-place nhưng cần đảm bảo TTL)
        cache.Set(key, timestamps, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = Window
        });
        return true;
    }

    /// <inheritdoc />
    public int GetRemainingAttempts(string email)
    {
        var key = CacheKey(email);
        var now = timeProvider.GetUtcNow();
        if (!cache.TryGetValue(key, out List<DateTimeOffset>? timestamps) || timestamps is null)
        {
            return MaxAttempts;
        }
        var active = timestamps.Count(t => now - t <= Window);
        return Math.Max(0, MaxAttempts - active);
    }
}
