namespace Planaffe.Api.Http;

public sealed record BrowserCookie(string Name, bool Secure)
{
    public const string ProductionName = "__Host-planaffe_session";
    public const string DevelopmentName = "planaffe_session";
    public static BrowserCookie For(bool development) => development ? new(DevelopmentName, false) : new(ProductionName, true);
    public CookieOptions Options(DateTimeOffset expires) => new() { HttpOnly = true, Secure = Secure, SameSite = SameSiteMode.Lax, Path = "/", Expires = expires };
}

/// <summary>
/// A browser write proves itself twice: the custom header, which no cross-site
/// form can set, and an <c>Origin</c> that is this instance.
/// </summary>
/// <remarks>
/// With <c>PLANAFFE_PUBLIC_URL</c> set the origin is compared whole. Without it
/// the scheme is left out, because it is the one part the instance cannot know:
/// a reverse proxy that terminates TLS forwards the request as <c>http</c>
/// unless it is trusted to say otherwise (<see cref="TrustedProxies"/>), and
/// comparing that against the browser's <c>https</c> refused every write an
/// operator who had not set the variable made. The host carries the check on
/// its own — a foreign origin cannot match it, and one that could would already
/// be answering for this instance.
/// </remarks>
public static class CsrfProtection
{
    public const string Header = "X-Planaffe-CSRF";

    public static bool IsSafe(HttpRequest request, Uri? publicUrl)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Headers[Header].ToString() != "1"
            || !Uri.TryCreate(request.Headers.Origin.ToString(), UriKind.Absolute, out var origin))
        {
            return false;
        }

        return publicUrl is null
            ? request.Host.HasValue
                && string.Equals(origin.Authority, request.Host.Value, StringComparison.OrdinalIgnoreCase)
            : Uri.Compare(publicUrl, origin, UriComponents.SchemeAndServer, UriFormat.SafeUnescaped, StringComparison.OrdinalIgnoreCase) == 0;
    }
}
