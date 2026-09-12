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
/// <remarks>
/// A subtree is found and written in one statement each, recursively. The
/// depth limit would let every one of them be written as two joins today, and
/// the recursion is what keeps them true if the limit ever moves.
/// </remarks>
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

    public async Task<IReadOnlyList<SpacePage>> DescendantsAsync(Guid pageId, CancellationToken cancellationToken) =>
        await context.SpacePages.FromSql(
            $"""
             with recursive subtree as (
                 select child.* from space_page child
                  where child.parent_id = {pageId} and child.deleted_at is null
                 union all
                 select child.* from space_page child
                   join subtree on child.parent_id = subtree.id
                  where child.deleted_at is null)
             select * from subtree
             """)
            .OrderBy(p => p.Depth)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<SpacePage>> CompanionsAsync(Guid pageId, CancellationToken cancellationToken) =>
        await context.SpacePages
            .Where(p => p.DeletedWith == pageId)
            .OrderBy(p => p.Depth)
            .ToListAsync(cancellationToken);

    public Task DeleteDescendantsAsync(Guid pageId, Guid by, DateTimeOffset at, CancellationToken cancellationToken) =>
        context.Database.ExecuteSqlAsync(
            $"""
             with recursive subtree as (
                 select child.id, child.parent_id from space_page child
                  where child.parent_id = {pageId} and child.deleted_at is null
                 union all
                 select child.id, child.parent_id from space_page child
                   join subtree on child.parent_id = subtree.id
                  where child.deleted_at is null)
             update space_page
                set deleted_at = {at}, deleted_by = {by}, deleted_with = {pageId}
              where id in (select id from subtree)
             """,
            cancellationToken);

    public Task RestoreCompanionsAsync(Guid pageId, CancellationToken cancellationToken) =>
        context.Database.ExecuteSqlAsync(
            $"""
             update space_page
                set deleted_at = null, deleted_by = null, deleted_with = null
              where deleted_with = {pageId}
             """,
            cancellationToken);

    public Task ShiftDescendantsAsync(Guid pageId, Guid spaceId, int levels, CancellationToken cancellationToken) =>
        context.Database.ExecuteSqlAsync(
            $"""
             with recursive subtree as (
                 select child.id, child.parent_id from space_page child
                  where child.parent_id = {pageId}
                 union all
                 select child.id, child.parent_id from space_page child
                   join subtree on child.parent_id = subtree.id)
             update space_page
                set space_id = {spaceId}, depth = depth + {levels}
              where id in (select id from subtree)
             """,
            cancellationToken);

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
