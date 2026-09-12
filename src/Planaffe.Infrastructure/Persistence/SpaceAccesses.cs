using Microsoft.EntityFrameworkCore;
using Planaffe.Application.Ports;
using Planaffe.Domain.Identities;
using Planaffe.Domain.Spaces;

namespace Planaffe.Infrastructure.Persistence;

/// <summary>The space assignments — the mirror of <see cref="ProjectAccesses"/>.</summary>
public sealed class SpaceAccesses(PlanaffeDbContext context) : ISpaceAccess
{
    public Task<bool> HasAsync(Guid userId, Guid spaceId, CancellationToken cancellationToken) =>
        context.SpaceAccesses.AnyAsync(a => a.UserId == userId && a.SpaceId == spaceId, cancellationToken);

    public async Task<IReadOnlySet<Guid>> SpaceIdsAsync(Guid userId, CancellationToken cancellationToken) =>
        (await context.SpaceAccesses.Where(a => a.UserId == userId).Select(a => a.SpaceId)
            .ToListAsync(cancellationToken)).ToHashSet();

    public async Task<IReadOnlyList<User>> UsersAsync(Guid spaceId, CancellationToken cancellationToken) =>
        await (from access in context.SpaceAccesses
               join user in context.Users on access.UserId equals user.Id
               where access.SpaceId == spaceId
               orderby user.Name
               select user).ToListAsync(cancellationToken);

    public async Task GrantAsync(SpaceAccess access, CancellationToken cancellationToken)
    {
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            insert into space_access (space_id, user_id, granted_by, granted_at)
            values ({access.SpaceId}, {access.UserId}, {access.GrantedBy}, {access.GrantedAt})
            on conflict (space_id, user_id) do nothing
            """, cancellationToken);
    }

    public async Task RevokeAsync(Guid spaceId, Guid userId, CancellationToken cancellationToken)
    {
        await context.SpaceAccesses.Where(a => a.SpaceId == spaceId && a.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
