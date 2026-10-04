using System.Collections.Concurrent;

namespace SalonManagement.Services;

public sealed record LookupBlockResult(bool IsBlocked, DateTimeOffset? RetryAt);

public sealed class AppointmentLookupRateLimiter(TimeProvider timeProvider)
{
    private static readonly ConcurrentDictionary<string, List<DateTimeOffset>> Failures = new();
    private static readonly ConcurrentDictionary<string, DateTimeOffset> Blocks = new();
    private static readonly object Gate = new();

    public LookupBlockResult Check(string ipAddress)
    {
        lock (Gate)
        {
            if (Blocks.TryGetValue(ipAddress, out var retryAt) && retryAt > timeProvider.GetUtcNow()) return new(true, retryAt);
            if (Blocks.TryRemove(ipAddress, out _)) Failures.TryRemove(ipAddress, out _);
            return new(false, null);
        }
    }

    public LookupBlockResult RegisterFailure(string ipAddress)
    {
        lock (Gate)
        {
            var now = timeProvider.GetUtcNow();
            var failures = Failures.GetOrAdd(ipAddress, _ => []);
            failures.RemoveAll(item => item <= now.AddHours(-1));
            failures.Add(now);
            if (failures.Count <= 10) return new(false, null);
            var retryAt = now.AddMinutes(30);
            Blocks[ipAddress] = retryAt;
            return new(true, retryAt);
        }
    }
}
