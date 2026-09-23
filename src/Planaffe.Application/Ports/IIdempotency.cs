namespace Planaffe.Application.Ports;

/// <summary>What was answered to a write, kept for a replay (<c>docs/api.md</c>, Idempotency).</summary>
/// <param name="Status">
/// The status of the answer, or nothing while the first request is still
/// being answered: the row is written before the write runs, so that a twin
/// sent in the meantime finds it and does not run the write a second time.
/// </param>
/// <param name="Location">The <c>Location</c> header the answer carried, if any.</param>
/// <param name="ETag">The <c>ETag</c> header the answer carried, if any.</param>
/// <param name="Withheld">
/// The answer carried a secret that is shown once, so only its status was
/// kept and <paramref name="Body"/> is empty: a replay is refused rather than
/// handed a copy of the secret.
/// </param>
public sealed record StoredReply(
    byte[] RequestHash,
    short? Status,
    string? Body,
    DateTimeOffset CreatedAt,
    bool Withheld = false,
    string? Location = null,
    string? ETag = null)
{
    /// <summary>Whether the first request is still being answered.</summary>
    public bool Pending => Status is null;
}

/// <summary>
/// The idempotency store: one reply per identity and key, for 24 hours. Keys
/// of different identities never meet, because the identity is half of the key.
/// </summary>
public interface IIdempotency
{
    Task<StoredReply?> FindAsync(Guid identityId, string key, CancellationToken cancellationToken);

    /// <summary>
    /// Writes the pending row a request holds while it runs, and says whether
    /// this request got it. A row that has outlived the replay window, or a
    /// pending one whose request can no longer be running, is replaced; any
    /// other row means a twin was first.
    /// </summary>
    Task<bool> TryBeginAsync(Guid identityId, string key, byte[] requestHash, DateTimeOffset now,
        TimeSpan lifetime, TimeSpan pendingLifetime, CancellationToken cancellationToken);

    /// <summary>Turns the pending row into the reply a replay is answered with.</summary>
    Task CompleteAsync(Guid identityId, string key, StoredReply reply, CancellationToken cancellationToken);

    /// <summary>Takes the pending row away again, for an answer that is not kept — a 500 is retried.</summary>
    Task AbandonAsync(Guid identityId, string key, CancellationToken cancellationToken);
}
