using Planaffe.Application.Ports;
using Planaffe.Domain;
using Planaffe.Domain.Identities;

namespace Planaffe.Application.Acts;

/// <summary>What <c>pa login</c> is told when a login begins.</summary>
/// <param name="DeviceCode">The long code the CLI keeps and polls with. Never shown to a person.</param>
/// <param name="UserCode">The short code the CLI prints, as a person reads it.</param>
/// <param name="VerificationUri">Where a human goes to confirm.</param>
/// <param name="VerificationUriComplete">The same, with the code already in it.</param>
/// <param name="ExpiresInSeconds">How long the human has.</param>
/// <param name="IntervalSeconds">How often the CLI should poll.</param>
public sealed record DeviceLoginBegun(
    string DeviceCode,
    string UserCode,
    string VerificationUri,
    string VerificationUriComplete,
    int ExpiresInSeconds,
    int IntervalSeconds);

/// <summary>
/// A login waiting to be confirmed, as the browser draws it before anybody
/// presses anything: the code as it is read, and how long is left.
/// </summary>
public sealed record PendingDeviceLogin(string UserCode, DateTimeOffset ExpiresAt);

/// <summary>What a redeemed login hands over: the token, once, and who it makes the caller.</summary>
public sealed record DeviceLoginRedeemed(IssuedToken Token, IdentityRef User, string Email, bool Administrator);

/// <summary>
/// Begin a device login (ADR 0025). Unauthenticated, because the whole point is
/// that the machine asking has nothing yet.
/// </summary>
public sealed class BeginDeviceLogin(IDeviceLogins logins, TimeProvider clock)
{
    /// <summary>The page a human is sent to. Relative; the CLI has the host already.</summary>
    public const string VerificationPath = "/device";

    public async Task<DeviceLoginBegun> ExecuteAsync(CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var (login, deviceCode, userCode) = DeviceLogin.Begin(now);

        await logins.AddAsync(login, now, cancellationToken);

        // The address is relative and the CLI joins the halves, because what it
        // prints is read by a person who has to open it — and `/device` is not
        // something anybody can open.
        var readable = UserCode.ForReading(userCode);

        return new DeviceLoginBegun(
            deviceCode,
            readable,
            VerificationPath,
            $"{VerificationPath}?code={readable}",
            (int)DeviceLogin.Lifetime.TotalSeconds,
            (int)DeviceLogin.PollingInterval.TotalSeconds);
    }
}

/// <summary>
/// What the confirmation page draws: the login a typed code names, if one is
/// still waiting. A signed-in user only — an agent that could read a pending
/// login is an agent one step from confirming it.
/// </summary>
public sealed class ShowDeviceLogin(ICallerIdentity caller, IDeviceLogins logins, TimeProvider clock)
{
    public async Task<PendingDeviceLogin> ExecuteAsync(string? typedCode, CancellationToken cancellationToken)
    {
        caller.Caller.RequireUser("confirm a device login");

        var login = await DeviceLoginLookup.Waiting(typedCode, logins, clock.GetUtcNow(), cancellationToken);

        return new PendingDeviceLogin(UserCode.ForReading(login.UserCode), login.ExpiresAt);
    }
}

/// <summary>
/// A human confirms or refuses a login, by the browser session they already
/// have. No second password on this one page: planaffe has a sign-in screen,
/// and somebody who is not signed in is sent to it and returned here.
/// </summary>
public sealed class DecideDeviceLogin(ICallerIdentity caller, IDeviceLogins logins, TimeProvider clock)
{
    public async Task<PendingDeviceLogin> ExecuteAsync(string? typedCode, bool approve, CancellationToken cancellationToken)
    {
        var who = caller.Caller.RequireUser("confirm a device login");
        var now = clock.GetUtcNow();

        var login = await DeviceLoginLookup.Waiting(typedCode, logins, now, cancellationToken);

        var decided = approve ? login.ApproveBy(who.Id, now) : login.Deny(now);
        if (!decided || !await logins.TryRecordDecisionAsync(login, cancellationToken))
        {
            // Between the read and here somebody decided it, or it ran out.
            throw DeviceLoginLookup.NoLongerWaiting(login, now);
        }

        return new PendingDeviceLogin(UserCode.ForReading(login.UserCode), login.ExpiresAt);
    }
}

/// <summary>
/// The CLI's poll: the user token once a human has confirmed, and a code that
/// says why not until then (ADR 0025).
/// </summary>
/// <remarks>
/// What it mints is an ordinary user token — the same thing <c>POST /tokens</c>
/// makes. The device flow is how one is delivered without going through a
/// person's clipboard, not a second kind of credential.
/// </remarks>
public sealed class RedeemDeviceLogin(IDeviceLogins logins, TimeProvider clock)
{
    public async Task<DeviceLoginRedeemed> ExecuteAsync(string? deviceCode, CancellationToken cancellationToken)
    {
        var found = string.IsNullOrWhiteSpace(deviceCode)
            ? null
            : await logins.FindByDeviceCodeHashAsync(DeviceCode.Hash(deviceCode), cancellationToken);

        if (found is null)
        {
            throw new Refusal(RefusalCode.NotFound, "No login is waiting for that device code.");
        }

        var now = clock.GetUtcNow();
        var login = found.Login;

        // Every state but one ends the polling, and each is its own code so
        // that a CLI keeps waiting on exactly one of them (docs/api.md).
        switch (login.StateAt(now))
        {
            case DeviceLoginState.Pending:
                throw new Refusal(RefusalCode.DevicePending, "Nobody has confirmed this login yet.");
            case DeviceLoginState.Denied:
                throw new Refusal(RefusalCode.DeviceDenied, "A human refused this login.");
            case DeviceLoginState.Expired:
                throw new Refusal(RefusalCode.DeviceExpired, "This login expired before it was confirmed.");
            case DeviceLoginState.Redeemed:
                throw new Refusal(RefusalCode.DeviceExpired,
                    "This login's token has already been collected. A device code works once.");
            default:
                break;
        }

        var user = found.Approver;
        if (user is null || user.State != UserState.Active)
        {
            throw new Refusal(RefusalCode.DeviceDenied,
                "The user who confirmed this login can no longer sign in.");
        }

        var secret = TokenSecret.Generate();
        var token = Token.Issue(user, secret, now);

        if (!login.RedeemTo(token.Id, now) || !await logins.TryRedeemAsync(login, token, cancellationToken))
        {
            // Two polls of the same device code raced; the loser gets what a
            // second collection always gets.
            throw new Refusal(RefusalCode.DeviceExpired,
                "This login's token has already been collected. A device code works once.");
        }

        return new DeviceLoginRedeemed(
            new IssuedToken(token.Id, token.Prefix, secret, token.CreatedAt),
            IdentityRef.Of(user),
            user.Email,
            user.Administrator);
    }
}

/// <summary>
/// Revoke the token this request presented — what <c>pa logout</c> calls,
/// because <c>GET /me</c> shows a token's prefix and not its id.
/// </summary>
public sealed class RevokeMyToken(ICallerIdentity caller, ITokens tokens, TimeProvider clock)
{
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var who = caller.Caller;
        if (who.SessionId is not null)
        {
            throw new Refusal(RefusalCode.Forbidden,
                "This request came in on a browser session, which is ended by `DELETE /session`.");
        }

        var token = await tokens.FindAsync(who.TokenId, cancellationToken);
        if (token is null || token.Revoked)
        {
            return;
        }

        token.Revoke(clock.GetUtcNow());
        await tokens.RecordRevocationAsync(token, cancellationToken);
    }
}

/// <summary>
/// The one lookup both browser acts make, and the one refusal they share.
/// </summary>
/// <remarks>
/// A code that never existed and one that has run out are the same answer.
/// Telling them apart tells somebody guessing codes which of their guesses was
/// half right, and a person who mistyped is helped by neither.
/// </remarks>
internal static class DeviceLoginLookup
{
    internal static async Task<DeviceLogin> Waiting(
        string? typedCode, IDeviceLogins logins, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var code = UserCode.Normalize(typedCode);

        var login = code.Length == 0
            ? null
            : await logins.FindByUserCodeAsync(code, cancellationToken);

        if (login is null)
        {
            throw new Refusal(RefusalCode.NotFound, "No login is waiting for that code.");
        }

        return login.StateAt(now) is DeviceLoginState.Pending ? login : throw NoLongerWaiting(login, now);
    }

    internal static Refusal NoLongerWaiting(DeviceLogin login, DateTimeOffset now) =>
        login.StateAt(now) switch
        {
            DeviceLoginState.Denied => new Refusal(RefusalCode.DeviceDenied, "That login was already refused."),
            DeviceLoginState.Expired => new Refusal(RefusalCode.DeviceExpired, "That login expired before it was confirmed."),
            _ => new Refusal(RefusalCode.DeviceDenied, "That login has already been confirmed."),
        };
}
