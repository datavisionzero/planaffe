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
public sealed class Pages(PlanaffeDbContext context) : IPages
{
    public Task<Page?> FindLiveAsync(Guid spaceId, Guid? parentId, string slug, CancellationToken cancellationToken) =>
        context.Pages.SingleOrDefaultAsync(
            p => p.SpaceId == spaceId && p.ParentId == parentId && p.Slug == slug && p.DeletedAt == null,
            cancellationToken);

    public Task<Page?> FindAnyAsync(Guid spaceId, Guid? parentId, string slug, CancellationToken cancellationToken) =>
        context.Pages.SingleOrDefaultAsync(
            p => p.SpaceId == spaceId && p.ParentId == parentId && p.Slug == slug,
            cancellationToken);

    public Task<Page?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
        context.Pages.SingleOrDefaultAsync(p => p.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Page>> TreeAsync(Guid spaceId, CancellationToken cancellationToken) =>
        await context.Pages
            .Where(p => p.SpaceId == spaceId && p.DeletedAt == null)
            .OrderBy(p => p.Depth)
            .ThenBy(p => p.Title)
            .ThenBy(p => p.Slug)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Page>> DescendantsAsync(Guid pageId, CancellationToken cancellationToken) =>
        await context.Pages.FromSql(
            $"""
             with recursive subtree as (
                 select child.* from page child
                  where child.parent_id = {pageId} and child.deleted_at is null
                 union all
                 select child.* from page child
                   join subtree on child.parent_id = subtree.id
                  where child.deleted_at is null)
             select * from subtree
             """)
            .OrderBy(p => p.Depth)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Page>> CompanionsAsync(Guid pageId, CancellationToken cancellationToken) =>
        await context.Pages
            .Where(p => p.DeletedWith == pageId)
            .OrderBy(p => p.Depth)
            .ToListAsync(cancellationToken);

    public Task DeleteDescendantsAsync(Guid pageId, Guid by, DateTimeOffset at, CancellationToken cancellationToken) =>
        context.Database.ExecuteSqlAsync(
            $"""
             with recursive subtree as (
                 select child.id, child.parent_id from page child
                  where child.parent_id = {pageId} and child.deleted_at is null
                 union all
                 select child.id, child.parent_id from page child
                   join subtree on child.parent_id = subtree.id
                  where child.deleted_at is null)
             update page
                set deleted_at = {at}, deleted_by = {by}, deleted_with = {pageId}
              where id in (select id from subtree)
             """,
            cancellationToken);

    public Task RestoreCompanionsAsync(Guid pageId, CancellationToken cancellationToken) =>
        context.Database.ExecuteSqlAsync(
            $"""
             update page
                set deleted_at = null, deleted_by = null, deleted_with = null
              where deleted_with = {pageId}
             """,
            cancellationToken);

    public Task ShiftDescendantsAsync(Guid pageId, Guid spaceId, int levels, CancellationToken cancellationToken) =>
        context.Database.ExecuteSqlAsync(
            $"""
             with recursive subtree as (
                 select child.id, child.parent_id from page child
                  where child.parent_id = {pageId}
                 union all
                 select child.id, child.parent_id from page child
                   join subtree on child.parent_id = subtree.id)
             update page
                set space_id = {spaceId}, depth = depth + {levels}
              where id in (select id from subtree)
             """,
            cancellationToken);

    /// <summary>The columns the search selects, named as the shaper reads them.</summary>
    private sealed class Hit
    {
        public string Space { get; init; } = string.Empty;

        public string SpaceTitle { get; init; } = string.Empty;

        public string Path { get; init; } = string.Empty;

        public string Title { get; init; } = string.Empty;

        public string[] TrailPaths { get; init; } = [];

        public string[] TrailTitles { get; init; } = [];

        public string Headline { get; init; } = string.Empty;
    }

    /// <summary>
    /// The rank, the excerpt and the way down to every hit, in one statement.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The recursion builds the address and the titles above every live page
    /// of the spaces asked for, which is the same walk the tree is read with
    /// and for the same reason: three levels would go as two joins today, and
    /// the recursion stays true if the limit ever moves.
    /// </para>
    /// <para>
    /// <c>ts_rank_cd</c> orders and <c>ts_headline</c> cuts the excerpt — the
    /// one place in the product where a full-text read ranks (VISION 18). The
    /// space's name, the title and the address decide a tie, so that the same
    /// question twice is the same list twice.
    /// </para>
    /// <para>
    /// A deleted space is no place to look, whatever was asked for: a human's
    /// access row outlives the space until the purge, so the set passed in can
    /// still name it, and deleting a space marks its row alone (ADR 0013).
    /// </para>
    /// <para>
    /// The marks around the words that matched are <see cref="Excerpt"/>'s two
    /// control characters rather than the <c>&lt;b&gt;</c> Postgres would use,
    /// and the body has both taken out of it before the excerpt is cut: a page
    /// cannot write a mark of its own, and nothing that leaves here is markup.
    /// </para>
    /// </remarks>
    public async Task<IReadOnlyList<PageHitRow>> SearchAsync(
        IReadOnlyCollection<Guid> spaceIds, string query, int limit, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(spaceIds);

        if (spaceIds.Count == 0)
        {
            return [];
        }

        var ids = spaceIds.Distinct().ToArray();
        var start = Excerpt.Start.ToString();
        var stop = Excerpt.Stop.ToString();

        var hits = await context.Database.SqlQuery<Hit>(
            $"""
             with recursive tree as (
                 select page.id, page.slug as path, array[]::text[] as trail_paths, array[]::text[] as trail_titles
                   from page page
                  where page.parent_id is null and page.deleted_at is null and page.space_id = any({ids})
                 union all
                 select child.id,
                        parent.path || '/' || child.slug,
                        parent.trail_paths || parent.path,
                        parent.trail_titles || above.title
                   from page child
                   join tree parent on parent.id = child.parent_id
                   join page above on above.id = parent.id
                  where child.deleted_at is null)
             select space.name as "Space",
                    space.title as "SpaceTitle",
                    tree.path as "Path",
                    page.title as "Title",
                    tree.trail_paths as "TrailPaths",
                    tree.trail_titles as "TrailTitles",
                    ts_headline('simple',
                                replace(replace(page.body, {start}, ''), {stop}, ''),
                                asked.query,
                                'StartSel=' || {start} || ',StopSel=' || {stop} || ',MaxWords=28,MinWords=12,ShortWord=2') as "Headline"
               from websearch_to_tsquery('simple', {query}) as asked(query)
               join page page
                 on page.search @@ asked.query and page.deleted_at is null and page.space_id = any({ids})
               join tree on tree.id = page.id
               join space on space.id = page.space_id and space.deleted_at is null
              order by ts_rank_cd(page.search, asked.query) desc, space.name, page.title, tree.path
              limit {limit}
             """)
            .ToListAsync(cancellationToken);

        return [.. hits.Select(hit => new PageHitRow(
            hit.Space, hit.SpaceTitle, hit.Path, hit.Title, hit.TrailPaths, hit.TrailTitles, hit.Headline))];
    }

    public async Task<Page?> LoadForWriteAsync(Guid id, CancellationToken cancellationToken)
    {
        if (context.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("A row is loaded for writing inside a transaction, or the lock is worth nothing.");
        }

        await context.Database.ExecuteSqlRawAsync("select id from page where id = {0} for update", [id], cancellationToken);
        return await context.Pages.SingleOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public void Add(Page page) => context.Pages.Add(page);

    public Task SaveAsync(CancellationToken cancellationToken) => context.SaveChangesAsync(cancellationToken);
}
