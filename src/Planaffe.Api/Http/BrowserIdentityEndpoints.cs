using Planaffe.Application.Acts;
using Planaffe.Application.Ports;
using Planaffe.Domain;
using Microsoft.AspNetCore.Authorization;

namespace Planaffe.Api.Http;

public sealed record PasswordSignInRequest(string? Email, string? Password);
public sealed record BootstrapExchangeRequest(string? Token, string? Password);
public sealed record SecretPasswordRequest(string? Secret, string? Password);
public sealed record RecoveryRequest(string? Email);
public sealed record ChangePasswordRequest(string? CurrentPassword, string? Password);

public static class BrowserIdentityEndpoints
{
    public static IEndpointRouteBuilder MapBrowserIdentity(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/session", async (PasswordSignInRequest? request, HttpContext http,
            SignInWithPassword signIn, LoginThrottle throttle, BrowserCookie cookie, CancellationToken ct) =>
        {
            var source = LoginThrottle.SourceOf(http.Connection.RemoteIpAddress);
            if (!throttle.TryReserve(Normalized(request?.Email), source, out var attempt, out var wait))
            {
                return Problems.Throttled(http, wait, "Too many failed sign-ins for this account or from this address.");
            }

            (Domain.Identities.BrowserSession Session, string Secret)? issued;
            try { issued = await signIn.ExecuteAsync(request?.Email, request?.Password, ct); }
            catch { throttle.GiveBack(attempt); throw; }

            if (issued is null) return Problems.Result(RefusalCode.Unauthenticated, "The email or password is not correct.");
            throttle.Succeeded(attempt); SetCookie(http, cookie, issued.Value); return Results.NoContent();
        }).AllowAnonymous().WithName("SignIn").Produces(StatusCodes.Status204NoContent).ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        endpoints.MapPost("/session/bootstrap", async (BootstrapExchangeRequest? request, HttpContext http,
            ExchangeBootstrapToken exchange, BrowserCookie cookie, CancellationToken ct) =>
        {
            var issued = await exchange.ExecuteAsync(request?.Token, request?.Password, ct);
            if (issued is null) return Problems.Result(RefusalCode.Unauthenticated, "The bootstrap token cannot be exchanged.");
            SetCookie(http, cookie, issued.Value); return Results.NoContent();
        }).AllowAnonymous().WithName("ExchangeBootstrapToken").Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status401Unauthorized);

        endpoints.MapPost("/invitations/accept", async (SecretPasswordRequest? request, HttpContext http,
            AcceptInvitation accept, BrowserCookie cookie, CancellationToken ct) =>
        { var issued = await accept.ExecuteAsync(request?.Secret, request?.Password, ct); SetCookie(http, cookie, issued); return Results.NoContent(); })
            .AllowAnonymous().WithName("AcceptInvitation").Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status410Gone);

        // The answer is the same 202 for every address, and it leaves before
        // anything is looked up: writing the secret and talking to the SMTP
        // server take time only for an account that exists, and an answer that
        // waited for them would say which ones do.
        endpoints.MapPost("/password-recovery", (RecoveryRequest? request, HttpContext http, SmtpSettings smtp,
            LoginThrottle throttle, RequestPasswordRecovery recover, ILogger<RequestPasswordRecovery> logger) =>
        {
            if (!smtp.Configured) throw new Refusal(RefusalCode.SmtpNotConfigured, "Transactional email is not configured for this instance.");
            var verdict = throttle.TryRecover(Normalized(request?.Email), LoginThrottle.SourceOf(http.Connection.RemoteIpAddress), out var wait);
            if (verdict == LoginThrottle.Recovery.Throttled)
            {
                return Problems.Throttled(http, wait, "Too many recovery emails were asked for from this address.");
            }

            if (verdict == LoginThrottle.Recovery.Send)
            {
                var email = request?.Email;
                http.Response.OnCompleted(async () =>
                {
                    try { await recover.ExecuteAsync(email, CancellationToken.None); }
                    catch (Exception exception) { logger.LogError(exception, "A recovery email could not be sent."); }
                });
            }

            return Results.Accepted();
        })
            .AllowAnonymous().WithName("RequestPasswordRecovery").Produces(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity).ProducesProblem(StatusCodes.Status429TooManyRequests);

        endpoints.MapPost("/password-recovery/complete", async (SecretPasswordRequest? request, CompletePasswordRecovery recover, CancellationToken ct) =>
        { await recover.ExecuteAsync(request?.Secret, request?.Password, ct); return Results.NoContent(); })
            .AllowAnonymous().WithName("CompletePasswordRecovery").Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status410Gone);

        // A cookie-authenticated write without its CSRF proof is 403 `csrf`,
        // and an agent is told `forbidden`: both are the door's 403.
        var door = endpoints.MapGroup(string.Empty).RequireAuthorization()
            .ProducesProblem(StatusCodes.Status401Unauthorized).ProducesProblem(StatusCodes.Status403Forbidden);
        door.MapDelete("/session", async (HttpContext http, ICallerIdentity caller, IBrowserSessions sessions,
            BrowserCookie cookie, TimeProvider clock, CancellationToken ct) =>
        { var who = caller.Caller.RequireUser("sign out"); if (who.SessionId is { } id) await sessions.RevokeAsync(id, who.Id, clock.GetUtcNow(), ct); http.Response.Cookies.Delete(cookie.Name, cookie.Options(DateTimeOffset.UnixEpoch)); return Results.NoContent(); })
            .WithName("SignOut").Produces(StatusCodes.Status204NoContent);
        door.MapGet("/sessions", (ListBrowserSessions list, CancellationToken ct) => list.ExecuteAsync(ct)).WithName("ListBrowserSessions");
        door.MapDelete("/sessions/{id:guid}", async (Guid id, ICallerIdentity caller, IBrowserSessions sessions, TimeProvider clock, CancellationToken ct) =>
        { var who = caller.Caller.RequireUser("revoke a browser session"); await sessions.RevokeAsync(id, who.Id, clock.GetUtcNow(), ct); return Results.NoContent(); })
            .WithName("RevokeBrowserSession").Produces(StatusCodes.Status204NoContent);
        door.MapDelete("/sessions", async (ICallerIdentity caller, IBrowserSessions sessions, TimeProvider clock, CancellationToken ct) =>
        { var who = caller.Caller.RequireUser("revoke browser sessions"); await sessions.RevokeAllAsync(who.Id, who.SessionId, clock.GetUtcNow(), ct); return Results.NoContent(); })
            .WithName("RevokeOtherBrowserSessions").Produces(StatusCodes.Status204NoContent);
        door.MapPost("/me/password", async (ChangePasswordRequest? request, HttpContext http, ICallerIdentity caller,
            LoginThrottle throttle, ChangePassword change, CancellationToken ct) =>
        {
            var who = caller.Caller.RequireUser("change a password");
            if (!throttle.TryReservePasswordChange(who.Id, out var attempt, out var wait))
            {
                return Problems.Throttled(http, wait, "Too many wrong current passwords.");
            }

            try { await change.ExecuteAsync(request?.CurrentPassword, request?.Password, ct); }
            catch (Refusal refusal) when (ChangePassword.IsWrongCurrentPassword(refusal)) { throw; }
            catch { throttle.GiveBack(attempt); throw; }

            throttle.Succeeded(attempt);
            return Results.NoContent();
        })
            .WithName("ChangePassword").Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status429TooManyRequests);
        return endpoints;
    }

    // The address as the throttle counts it: normalized when it is one, and
    // as typed otherwise, so that garbage is counted rather than let through.
    private static string Normalized(string? email)
    {
        try { return Domain.Identities.User.NormalizeEmailForComparison(email!); }
        catch (ArgumentException) { return email?.Trim().ToLowerInvariant() ?? string.Empty; }
    }

    private static void SetCookie(HttpContext http, BrowserCookie cookie, (Domain.Identities.BrowserSession Session, string Secret) issued) =>
        http.Response.Cookies.Append(cookie.Name, issued.Secret, cookie.Options(issued.Session.ExpiresAt));
}

public sealed class BrowserCsrfMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ICallerIdentity caller, SmtpSettings smtp)
    {
        if (context.GetEndpoint()?.Metadata.GetMetadata<IAllowAnonymous>() is null
            && context.User.Identity?.IsAuthenticated == true && caller.Caller.SessionId is not null
            && context.Request.Method is not ("GET" or "HEAD" or "OPTIONS"))
        {
            if (!CsrfProtection.IsSafe(context.Request, smtp.PublicUrl)) { await Problems.WriteAsync(context, RefusalCode.Csrf, "The browser write failed its CSRF check."); return; }
        }
        await next(context);
    }
}
