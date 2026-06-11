using System.Collections.Concurrent;

namespace FamilyAssistance.Api.Auth;

public class LoginRateLimiter
{
    private readonly ConcurrentDictionary<string, List<DateTime>> _attempts = new();
    private const int MaxAttempts = 5;
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(15);

    public bool IsRateLimited(string username, string? ipAddress)
    {
        var key = $"{username.ToLowerInvariant()}:{ipAddress ?? "unknown"}";
        var now = DateTime.UtcNow;
        var list = _attempts.GetOrAdd(key, _ => []);

        lock (list)
        {
            list.RemoveAll(t => t < now - Window);
            return list.Count >= MaxAttempts;
        }
    }

    public void RecordFailure(string username, string? ipAddress)
    {
        var key = $"{username.ToLowerInvariant()}:{ipAddress ?? "unknown"}";
        var list = _attempts.GetOrAdd(key, _ => []);

        lock (list)
        {
            list.Add(DateTime.UtcNow);
        }
    }

    public void ClearFailures(string username, string? ipAddress)
    {
        var key = $"{username.ToLowerInvariant()}:{ipAddress ?? "unknown"}";
        _attempts.TryRemove(key, out _);
    }
}
