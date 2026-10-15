using System.Collections.Concurrent;

namespace SalonManagement.Services;

public sealed record BookingRateLimitResult(bool Allowed, DateTimeOffset? RetryAt);

public sealed class BookingRateLimiter(TimeProvider timeProvider)
{
    private static readonly ConcurrentDictionary<string, List<DateTimeOffset>> Attempts = new();
    private static readonly object Gate = new();

    public BookingRateLimitResult TryReserve(string ipAddress)
    {
        lock (Gate)
        {
            var now = timeProvider.GetUtcNow();
            var attempts = Attempts.GetOrAdd(ipAddress, _ => []);
            attempts.RemoveAll(item => item <= now.AddHours(-1));
            if (attempts.Count >= 5) return new(false, attempts[0].AddHours(1));
            attempts.Add(now);
            return new(true, null);
        }
    }

    public void Release(string ipAddress)
    {
        lock (Gate) if (Attempts.TryGetValue(ipAddress, out var attempts) && attempts.Count > 0) attempts.RemoveAt(attempts.Count - 1);
    }
}
