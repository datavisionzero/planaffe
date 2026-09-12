using Microsoft.EntityFrameworkCore;
using Planaffe.Application.Ports;
using Planaffe.Domain.Spaces;

namespace Planaffe.Infrastructure.Persistence;

/// <summary>
/// The knowledge base's page rows. A tree comes out in one query and is put in
/// order here, by depth and then by title: a reader of three levels wants the
/// whole thing, and asking once per level would be three round trips for a
/// table this small.
/// </summary>
public sealed class SpacePages(PlanaffeDbContext context) : ISpacePages
{
    public Task<SpacePage?> FindLiveAsync(Guid spaceId, Guid? parentId, string slug, CancellationToken cancellationToken) =>
        context.SpacePages.SingleOrDefaultAsync(
            p => p.SpaceId == spaceId && p.ParentId == parentId && p.Slug == slug && p.DeletedAt == null,
            cancellationToken);

    public Task<SpacePage?> FindAnyAsync(Guid spaceId, Guid? parentId, string slug, CancellationToken cancellationToken) =>
        context.SpacePages.SingleOrDefaultAsync(
            p => p.SpaceId == spaceId && p.ParentId == parentId && p.Slug == slug,
            cancellationToken);

    public Task<SpacePage?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
        context.SpacePages.SingleOrDefaultAsync(p => p.Id == id, cancellationToken);

    public async Task<IReadOnlyList<SpacePage>> TreeAsync(Guid spaceId, CancellationToken cancellationToken) =>
        await context.SpacePages
            .Where(p => p.SpaceId == spaceId && p.DeletedAt == null)
            .OrderBy(p => p.Depth)
            .ThenBy(p => p.Title)
            .ThenBy(p => p.Slug)
            .ToListAsync(cancellationToken);

    public async Task<SpacePage?> LoadForWriteAsync(Guid id, CancellationToken cancellationToken)
    {
        if (context.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("A row is loaded for writing inside a transaction, or the lock is worth nothing.");
        }

        await context.Database.ExecuteSqlRawAsync("select id from space_page where id = {0} for update", [id], cancellationToken);
        return await context.SpacePages.SingleOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public void Add(SpacePage page) => context.SpacePages.Add(page);

    public Task SaveAsync(CancellationToken cancellationToken) => context.SaveChangesAsync(cancellationToken);
}
