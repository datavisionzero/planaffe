using Planaffe.Domain.Identities;

namespace Planaffe.Application.Ports;

public interface IPasswordHasher
{
    Task<string> HashAsync(string password, CancellationToken cancellationToken);
    Task<bool> VerifyAsync(string encodedHash, string password, CancellationToken cancellationToken);
}

public interface IOneTimeSecrets
{
    Task AddReplacingLiveAsync(OneTimeSecret secret, DateTimeOffset now, CancellationToken cancellationToken);
    Task<OneTimeSecret?> ConsumeAsync(byte[] secretHash, OneTimeSecretPurpose purpose, DateTimeOffset now, CancellationToken cancellationToken);
}

public sealed record SessionUser(BrowserSession Session, User User);

public interface IBrowserSessions
{
    Task AddAsync(BrowserSession session, CancellationToken cancellationToken);
    Task<SessionUser?> AuthenticateAsync(byte[] secretHash, DateTimeOffset now, CancellationToken cancellationToken);
    Task RevokeAsync(Guid sessionId, Guid userId, DateTimeOffset now, CancellationToken cancellationToken);
    Task RevokeAllAsync(Guid userId, Guid? exceptSessionId, DateTimeOffset now, CancellationToken cancellationToken);
    Task<IReadOnlyList<BrowserSession>> ListAsync(Guid userId, CancellationToken cancellationToken);
}

/// <summary>
/// One device login and whoever confirmed it, as the CLI's poll reads them:
/// one lookup, both rows, because the poll is the hot half of this flow.
/// </summary>
public sealed record ConfirmedDeviceLogin(DeviceLogin Login, User? Approver);

/// <summary>
/// The device-login rows (ADR 0025): found by the hash of the code the CLI
/// polls with, and by the short code the human typed.
/// </summary>
public interface IDeviceLogins
{
    /// <summary>
    /// Adds a login, and sweeps the ones that expired more than an hour ago in
    /// the same statement — the table's only growth control, and a grace long
    /// enough that no poll of a ten-minute code can outlive its own row.
    /// </summary>
    Task AddAsync(DeviceLogin login, DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>The login a typed code names, whatever state it is in.</summary>
    Task<DeviceLogin?> FindByUserCodeAsync(string userCode, CancellationToken cancellationToken);

    /// <summary>The login a poll's device code names, with whoever confirmed it.</summary>
    Task<ConfirmedDeviceLogin?> FindByDeviceCodeHashAsync(byte[] deviceCodeHash, CancellationToken cancellationToken);

    /// <summary>
    /// Writes back the approval or the refusal just made on the row, guarded on
    /// nobody having decided it in the meantime. False means somebody did.
    /// </summary>
    Task<bool> TryRecordDecisionAsync(DeviceLogin login, CancellationToken cancellationToken);

    /// <summary>
    /// Redeeming and issuing in one transaction, guarded on the row still being
    /// unredeemed: either the login is marked and the token exists, or neither
    /// happened. A device code that stayed usable after handing a token over
    /// would be a second key for whoever still has it.
    /// </summary>
    Task<bool> TryRedeemAsync(DeviceLogin login, Token token, CancellationToken cancellationToken);
}
