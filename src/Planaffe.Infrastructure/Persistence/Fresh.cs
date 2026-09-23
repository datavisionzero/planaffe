using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace Planaffe.Infrastructure.Persistence;

/// <summary>
/// Reading a row as the database holds it now, not as this context last saw it.
/// </summary>
/// <remarks>
/// A query for an entity the context already tracks hands back the tracked
/// instance and leaves its values alone: EF Core resolves identity before it
/// looks at what the row says. An act that read the row before its
/// transaction and reads it again after <c>for update</c> would then check
/// <c>If-Match</c> or a state against the copy from before the lock — which is
/// how a second writer passes a guard the first one has already moved. Every
/// read under a lock goes through here, so a tracked, unchanged instance is
/// reloaded first. One with pending changes is left as it is; those are the
/// act's own.
/// </remarks>
internal static class Fresh
{
    public static async Task<TEntity?> SingleAsync<TEntity>(
        DbSet<TEntity> set, Guid id, Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken)
        where TEntity : class
    {
        if (set.Local.FindEntry(id) is { State: EntityState.Unchanged } tracked)
        {
            // A row that has gone since detaches the entry, and the query below
            // then answers null the way it would for any absent row.
            await tracked.ReloadAsync(cancellationToken);
        }

        return await set.SingleOrDefaultAsync(predicate, cancellationToken);
    }
}
