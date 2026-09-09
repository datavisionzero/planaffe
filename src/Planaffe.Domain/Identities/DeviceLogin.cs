namespace Planaffe.Domain.Identities;

/// <summary>
/// Where one device login has got to. <c>pa login</c> polls and is answered
/// exactly one of these; four of the five end the polling (ADR 0025).
/// </summary>
public enum DeviceLoginState
{
    /// <summary>Nobody has confirmed it yet. Keep polling.</summary>
    Pending,

    /// <summary>A human confirmed it. The next poll receives the token.</summary>
    Approved,

    /// <summary>A human said they did not start this login.</summary>
    Denied,

    /// <summary>Nobody confirmed it in time.</summary>
    Expired,

    /// <summary>The token was already handed over. A device code works once.</summary>
    Redeemed,
}

/// <summary>
/// One <c>pa login</c> in progress (ADR 0025): the CLI prints a short code, a
/// human confirms it in a browser on this machine or on another one, and the
/// CLI polls with a long one until something has happened.
/// </summary>
/// <remarks>
/// <para>
/// This is the only login that works everywhere <c>pa</c> runs — an SSH
/// session, a CI job, a container, an agent's sandbox — because it is the only
/// one that needs no browser on the machine doing the asking.
/// </para>
/// <para>
/// The row carries the hash of the device code and never the code. What the
/// instance can read back is the short user code, which is not a credential: it
/// identifies a pending request to the human confirming it, and confirming it
/// still takes a browser session of theirs.
/// </para>
/// <para>
/// It is the one row in this product that expires by the clock rather than by
/// an act. Everything else here is revoked deliberately (VISION 12), and this
/// is not an exception to that rule so much as the reason for it: what expires
/// is the request, not the credential it produces.
/// </para>
/// </remarks>
public sealed class DeviceLogin
{
    /// <summary>
    /// How long a human has. Long enough to walk to another machine, short
    /// enough that an abandoned code is not lying around for an afternoon.
    /// </summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

    /// <summary>How often <c>pa</c> should poll. Seconds, and the server says so.</summary>
    public static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(5);

    private DeviceLogin()
    {
        // EF Core materializes through this; every other route goes through Begin.
    }

    private DeviceLogin(byte[] deviceCodeHash, string userCode, DateTimeOffset now)
    {
        Id = Guid.CreateVersion7();
        DeviceCodeHash = deviceCodeHash;
        UserCode = userCode;
        CreatedAt = now;
        ExpiresAt = now + Lifetime;
    }

    public Guid Id { get; private init; }

    /// <summary>The hash of the long code <c>pa</c> polls with, and never the code.</summary>
    public byte[] DeviceCodeHash { get; private init; } = null!;

    /// <summary>The short code a human reads out of a terminal and types into a browser.</summary>
    public string UserCode { get; private init; } = null!;

    public DateTimeOffset CreatedAt { get; private init; }

    public DateTimeOffset ExpiresAt { get; private init; }

    /// <summary>When a human confirmed it, and who.</summary>
    public DateTimeOffset? ApprovedAt { get; private set; }

    public Guid? ApprovedByUserId { get; private set; }

    /// <summary>When a human said they did not start this login.</summary>
    public DateTimeOffset? DeniedAt { get; private set; }

    /// <summary>When the token was handed over. A device code works once.</summary>
    public DateTimeOffset? RedeemedAt { get; private set; }

    /// <summary>Which user token this login produced, once it has.</summary>
    public Guid? IssuedTokenId { get; private set; }

    /// <summary>
    /// Begin one: the record, and the two codes, which exist exactly once. The
    /// device code is returned rather than stored, for the reason a token
    /// secret is.
    /// </summary>
    public static (DeviceLogin Login, string DeviceCode, string UserCode) Begin(DateTimeOffset now)
    {
        var deviceCode = Identities.DeviceCode.Issue();
        var userCode = Identities.UserCode.Issue();

        return (new DeviceLogin(Identities.DeviceCode.Hash(deviceCode), userCode, now), deviceCode, userCode);
    }

    /// <summary>Where this login has got to at <paramref name="moment"/>.</summary>
    /// <remarks>
    /// The order is the order of what already happened: a redeemed login stays
    /// redeemed after it expires, and an approval nobody collected in time is
    /// expired rather than approved — otherwise a device code left in a CI log
    /// would still be worth something an hour later.
    /// </remarks>
    public DeviceLoginState StateAt(DateTimeOffset moment) =>
        RedeemedAt is not null ? DeviceLoginState.Redeemed
        : DeniedAt is not null ? DeviceLoginState.Denied
        : moment >= ExpiresAt ? DeviceLoginState.Expired
        : ApprovedAt is not null ? DeviceLoginState.Approved
        : DeviceLoginState.Pending;

    /// <summary>
    /// A human confirms it. Only a pending one can be confirmed — repeating it,
    /// or confirming one somebody already refused, changes nothing and says so.
    /// </summary>
    public bool ApproveBy(Guid userId, DateTimeOffset moment)
    {
        if (StateAt(moment) is not DeviceLoginState.Pending)
        {
            return false;
        }

        ApprovedAt = moment;
        ApprovedByUserId = userId;

        return true;
    }

    /// <summary>A human says they did not start this login.</summary>
    public bool Deny(DateTimeOffset moment)
    {
        if (StateAt(moment) is not DeviceLoginState.Pending)
        {
            return false;
        }

        DeniedAt = moment;

        return true;
    }

    /// <summary>
    /// Hand the token over, once. Only an approved login can be redeemed, and
    /// only the first poll after the approval gets anything.
    /// </summary>
    public bool RedeemTo(Guid tokenId, DateTimeOffset moment)
    {
        if (StateAt(moment) is not DeviceLoginState.Approved)
        {
            return false;
        }

        RedeemedAt = moment;
        IssuedTokenId = tokenId;

        return true;
    }
}
