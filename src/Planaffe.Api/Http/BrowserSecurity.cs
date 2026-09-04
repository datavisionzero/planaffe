using System.Collections.Concurrent;

namespace Planaffe.Api.Http;

public sealed record BrowserCookie(string Name, bool Secure)
{
    public const string ProductionName = "__Host-planaffe_session";
    public const string DevelopmentName = "planaffe_session";
    public static BrowserCookie For(bool development) => development ? new(DevelopmentName, false) : new(ProductionName, true);
    public CookieOptions Options(DateTimeOffset expires) => new() { HttpOnly = true, Secure = Secure, SameSite = SameSiteMode.Lax, Path = "/", Expires = expires };
}

/// <summary>Small bounded rolling-window limiter for failed password sign-ins.</summary>
public sealed class LoginThrottle(TimeProvider clock)
{
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(15);
    private const int AccountLimit = 5, AddressLimit = 20, MaximumKeys = 4096;
    private readonly ConcurrentDictionary<string, Queue<DateTimeOffset>> attempts = new(StringComparer.Ordinal);
    private readonly object gate = new();

    public bool IsBlocked(string normalizedEmail, string sourceAddress)
    {
        lock (gate) return Count("account:" + normalizedEmail) >= AccountLimit || Count("address:" + sourceAddress) >= AddressLimit;
    }
    public void Failed(string normalizedEmail, string sourceAddress)
    {
        lock (gate) { Add("account:" + normalizedEmail); Add("address:" + sourceAddress); TrimStore(); }
    }
    public void Succeeded(string normalizedEmail) { lock (gate) attempts.TryRemove("account:" + normalizedEmail, out _); }
    private int Count(string key) { if (!attempts.TryGetValue(key, out var queue)) return 0; Prune(queue); return queue.Count; }
    private void Add(string key) { var queue = attempts.GetOrAdd(key, _ => new()); Prune(queue); queue.Enqueue(clock.GetUtcNow()); }
    private void Prune(Queue<DateTimeOffset> queue) { var floor = clock.GetUtcNow() - Window; while (queue.TryPeek(out var time) && time <= floor) queue.Dequeue(); }
    private void TrimStore() { if (attempts.Count <= MaximumKeys) return; foreach (var pair in attempts.Where(x => { Prune(x.Value); return x.Value.Count == 0; }).Take(attempts.Count - MaximumKeys)) attempts.TryRemove(pair.Key, out _); }
}

public static class CsrfProtection
{
    public const string Header = "X-Planaffe-CSRF";
    public static bool IsSafe(HttpRequest request, string expectedOrigin) =>
        request.Headers[Header].ToString() == "1" && Uri.TryCreate(expectedOrigin, UriKind.Absolute, out var expected)
        && Uri.TryCreate(request.Headers.Origin.ToString(), UriKind.Absolute, out var actual)
        && Uri.Compare(expected, actual, UriComponents.SchemeAndServer, UriFormat.SafeUnescaped, StringComparison.OrdinalIgnoreCase) == 0
        && expected.AbsolutePath.TrimEnd('/') == actual.AbsolutePath.TrimEnd('/');
}
