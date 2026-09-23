namespace Planaffe.Application.Ports;

/// <summary>What was answered to a write, kept for a replay (<c>docs/api.md</c>, Idempotency).</summary>
/// <param name="Withheld">
/// The answer carried a secret that is shown once, so only its status was
/// kept and <paramref name="Body"/> is empty: a replay is refused rather than
/// handed a copy of the secret.
/// </param>
public sealed record StoredReply(byte[] RequestHash, short Status, string? Body, DateTimeOffset CreatedAt, bool Withheld = false);

/// <summary>
/// The idempotency store: one reply per identity and key, for 24 hours. Keys
/// of different identities never meet, because the identity is half of the key.
/// </summary>
public interface IIdempotency
{
    Task<StoredReply?> FindAsync(Guid identityId, string key, CancellationToken cancellationToken);

    /// <summary>Keeps the reply; a second writer of the same identity and key loses quietly.</summary>
    Task StoreAsync(Guid identityId, string key, StoredReply reply, CancellationToken cancellationToken);
}
