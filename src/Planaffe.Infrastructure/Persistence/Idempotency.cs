using Microsoft.EntityFrameworkCore;
using Planaffe.Application.Ports;

namespace Planaffe.Infrastructure.Persistence;

/// <summary>The idempotency rows; the purge takes those older than a day.</summary>
/// <remarks>
/// Every statement here is one statement, outside the change tracker: the
/// context is the one the request's act uses, and a row written before the
/// act must be committed before the act begins, or a twin could not see it.
/// </remarks>
public sealed class Idempotency(PlanaffeDbContext context) : IIdempotency
{
    public async Task<StoredReply?> FindAsync(Guid identityId, string key, CancellationToken cancellationToken)
    {
        var row = await context.Idempotency
            .AsNoTracking()
            .SingleOrDefaultAsync(r => r.IdentityId == identityId && r.Key == key, cancellationToken);

        return row is null
            ? null
            : new StoredReply(row.RequestHash, row.Status, row.Body, row.CreatedAt, row.Withheld, row.Location, row.ETag);
    }

    public async Task<bool> TryBeginAsync(Guid identityId, string key, byte[] requestHash, DateTimeOffset now,
        TimeSpan lifetime, TimeSpan pendingLifetime, CancellationToken cancellationToken)
    {
        // The row of a request that has outlived the window, or a pending one
        // left by a process that went away mid-request, gives way; then the
        // insert is conditional on the key, and the primary key decides which
        // of two twins holds it.
        await context.Idempotency
            .Where(r => r.IdentityId == identityId && r.Key == key
                && (r.CreatedAt <= now - lifetime || (r.Status == null && r.CreatedAt <= now - pendingLifetime)))
            .ExecuteDeleteAsync(cancellationToken);

        var inserted = await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            insert into idempotency (identity_id, key, request_hash, status, body, withheld, created_at)
            values ({identityId}, {key}, {requestHash}, null, null, false, {now})
            on conflict (identity_id, key) do nothing
            """,
            cancellationToken);

        return inserted == 1;
    }

    public Task CompleteAsync(Guid identityId, string key, StoredReply reply, CancellationToken cancellationToken) =>
        context.Idempotency
            .Where(r => r.IdentityId == identityId && r.Key == key)
            .ExecuteUpdateAsync(set => set
                .SetProperty(r => r.Status, reply.Status)
                .SetProperty(r => r.Body, reply.Body)
                .SetProperty(r => r.Withheld, reply.Withheld)
                .SetProperty(r => r.Location, reply.Location)
                .SetProperty(r => r.ETag, reply.ETag), cancellationToken);

    public Task AbandonAsync(Guid identityId, string key, CancellationToken cancellationToken) =>
        context.Idempotency
            .Where(r => r.IdentityId == identityId && r.Key == key && r.Status == null)
            .ExecuteDeleteAsync(cancellationToken);
}
