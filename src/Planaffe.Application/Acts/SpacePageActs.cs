using Planaffe.Application.Ports;
using Planaffe.Domain;
using Planaffe.Domain.History;
using Planaffe.Domain.Spaces;

namespace Planaffe.Application.Acts;

/// <summary>
/// The slim page every tree returns: everything but the document itself
/// (ADR 0012). <c>Path</c> is the address, <c>Parent</c> the address of the
/// page above it, and both carry the tree (ADR 0028).
/// </summary>
public sealed record SpacePageSummaryShape(
    string Path,
    string Space,
    string Slug,
    string? Parent,
    int Depth,
    string Title,
    IdentityRef UpdatedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>The complete page: the summary plus the Markdown and the author.</summary>
public sealed record SpacePageShape(
    string Path,
    string Space,
    string Slug,
    string? Parent,
    int Depth,
    string Title,
    string Body,
    IdentityRef Author,
    IdentityRef UpdatedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <param name="Parent">The address of the page it hangs under, or nothing for one directly under the space.</param>
public sealed record CreateSpacePageRequest(string? Parent, string? Slug, string? Title, string? Body);

/// <param name="BodyGiven">Present, even as <c>null</c>, which empties the document.</param>
public sealed record SpacePageChanges(string? Slug, string? Title, bool BodyGiven, string? Body);

/// <summary>
/// The address of a page inside its space: the slugs from the root down,
/// separated by slashes (ADR 0028).
/// </summary>
/// <remarks>
/// One place parses it and one place builds it, because an address that is
/// read in four acts and written in two is exactly the thing that drifts. A
/// path that cannot name a page — empty, too many segments, a segment that is
/// no slug — is <c>not-found</c> rather than <c>validation</c>: it arrived in
/// the address, not in a body.
/// </remarks>
public static class SpacePagePath
{
    public const char Separator = '/';

    /// <summary>The segments, or nothing where the path could not name a page.</summary>
    public static IReadOnlyList<string>? Segments(string? path)
    {
        var segments = (path ?? string.Empty)
            .Trim()
            .Trim(Separator)
            .Split(Separator, StringSplitOptions.TrimEntries);

        return segments.Length is 0 or > SpacePage.MaxDepth + 1 || !segments.All(Slug.IsValid)
            ? null
            : segments;
    }

    public static string Of(string? parent, string slug) =>
        string.IsNullOrEmpty(parent) ? slug : $"{parent}{Separator}{slug}";
}

/// <summary>The lookups every act on a page of the knowledge base starts with.</summary>
public static class SpacePageLookup
{
    /// <summary>
    /// Down the tree, segment by segment, deleted or not. It answers with one
    /// sentence wherever it stops, and deliberately does not say which segment
    /// was missing: the path is an address, and an address that names nothing
    /// says only that.
    /// </summary>
    /// <exception cref="Refusal"><c>not-found</c>.</exception>
    public static async Task<SpacePage> AnyAsync(
        this ISpacePages pages, Space space, string? path, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pages);
        ArgumentNullException.ThrowIfNull(space);

        var segments = SpacePagePath.Segments(path);
        var missing = new Refusal(RefusalCode.NotFound, $"No page {space.Name}/{(path ?? string.Empty).Trim()}.");
        if (segments is null)
        {
            throw missing;
        }

        SpacePage? page = null;
        foreach (var slug in segments)
        {
            page = await pages.FindAnyAsync(space.Id, page?.Id, slug, cancellationToken) ?? throw missing;
        }

        return page!;
    }

    /// <exception cref="Refusal"><c>not-found</c>, or <c>deleted</c> with <c>restorable_until</c>.</exception>
    public static async Task<SpacePage> LiveAsync(
        this ISpacePages pages, Space space, string? path, InstanceSettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(space);
        var page = await pages.AnyAsync(space, path, cancellationToken);

        return page.Deleted
            ? throw new Refusal(
                RefusalCode.Deleted,
                $"Page {space.Name}/{(path ?? string.Empty).Trim()} is deleted and can be restored until at least {page.DeletedAt!.Value + settings.DeletionGrace:u}.",
                new Dictionary<string, object?> { ["restorable_until"] = page.DeletedAt.Value + settings.DeletionGrace })
            : page;
    }

    /// <summary>
    /// The page a new or moved one is to hang under: nothing where the address
    /// is empty, and a refusal where it has no room left below it.
    /// </summary>
    /// <exception cref="Refusal"><c>not-found</c>, <c>deleted</c>, or <c>too-deep</c>.</exception>
    public static async Task<SpacePage?> ParentAsync(
        this ISpacePages pages, Space space, string? path, InstanceSettings settings, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var parent = await pages.LiveAsync(space, path, settings, cancellationToken);

        return parent.Depth < SpacePage.MaxDepth
            ? parent
            : throw new Refusal(
                RefusalCode.TooDeep,
                $"A page sits at most {SpacePage.MaxDepth + 1} levels under its space, and {path.Trim()} is already at the bottom. Whoever needs a fourth level has found a second space.",
                new Dictionary<string, object?> { ["depth"] = parent.Depth });
    }

    /// <summary>
    /// A slug already taken under the same parent is refused as
    /// <c>validation</c>, and a deleted page's slug says so rather than
    /// pretending the name is in use — the same answer the project's page
    /// gives, for the same reason.
    /// </summary>
    public static async Task TakenAsync(
        this ISpacePages pages,
        Space space,
        SpacePage? parent,
        string slug,
        InstanceSettings settings,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pages);
        ArgumentNullException.ThrowIfNull(space);
        ArgumentNullException.ThrowIfNull(settings);

        if (await pages.FindAnyAsync(space.Id, parent?.Id, slug, cancellationToken) is not { } existing)
        {
            return;
        }

        throw Refusal.Validation("slug", existing.Deleted
            ? $"The page {slug} is deleted and can be restored until at least {existing.DeletedAt!.Value + settings.DeletionGrace:u}; a new one cannot take its slug until it is purged."
            : $"The page {slug} exists here.");
    }
}

/// <summary>
/// Turns page rows into the two shapes: the addresses from the tree, the
/// identities resolved once for the whole list.
/// </summary>
public sealed class SpacePageAssembler(ISpacePages pages, IIdentities identities)
{
    /// <summary>
    /// The tree in the order it is drawn: a page, then everything under it,
    /// siblings by title. The rows arrive sorted by depth, so every parent is
    /// seen before its children and the addresses can be built in one pass.
    /// </summary>
    public async Task<IReadOnlyList<SpacePageSummaryShape>> TreeAsync(
        Space space, IReadOnlyList<SpacePage> rows, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(space);
        ArgumentNullException.ThrowIfNull(rows);

        if (rows.Count == 0)
        {
            return [];
        }

        var people = await PeopleAsync(rows.Select(p => p.UpdatedBy), cancellationToken);
        var children = rows.GroupBy(p => p.ParentId).ToDictionary(g => g.Key ?? Guid.Empty, g => g.ToList());
        var shapes = new List<SpacePageSummaryShape>(rows.Count);

        void Walk(Guid parentId, string? parentPath)
        {
            if (!children.TryGetValue(parentId, out var level))
            {
                return;
            }

            foreach (var page in level)
            {
                var path = SpacePagePath.Of(parentPath, page.Slug);
                shapes.Add(new SpacePageSummaryShape(
                    path,
                    space.Name,
                    page.Slug,
                    parentPath,
                    page.Depth,
                    page.Title,
                    people[page.UpdatedBy],
                    page.CreatedAt,
                    page.UpdatedAt));

                Walk(page.Id, path);
            }
        }

        Walk(Guid.Empty, null);
        return shapes;
    }

    public async Task<SpacePageShape> CompleteAsync(Space space, SpacePage page, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(space);
        ArgumentNullException.ThrowIfNull(page);

        var parent = await ParentPathAsync(page, cancellationToken);
        var people = await PeopleAsync([page.CreatedBy, page.UpdatedBy], cancellationToken);

        return new SpacePageShape(
            SpacePagePath.Of(parent, page.Slug),
            space.Name,
            page.Slug,
            parent,
            page.Depth,
            page.Title,
            page.Body,
            people[page.CreatedBy],
            people[page.UpdatedBy],
            page.CreatedAt,
            page.UpdatedAt);
    }

    /// <summary>
    /// Upwards from the page, at most two steps, because that is how deep the
    /// tree goes. A caller that already holds the whole tree does not need
    /// this; one holding a single row does, and two reads are cheaper than
    /// carrying the address through every act that writes one.
    /// </summary>
    private async Task<string?> ParentPathAsync(SpacePage page, CancellationToken cancellationToken)
    {
        var slugs = new List<string>();
        var parentId = page.ParentId;

        while (parentId is { } id)
        {
            var parent = await pages.FindByIdAsync(id, cancellationToken)
                ?? throw new InvalidOperationException($"Page {id} has no row, and a child of it does.");

            slugs.Insert(0, parent.Slug);
            parentId = parent.ParentId;
        }

        return slugs.Count == 0 ? null : string.Join(SpacePagePath.Separator, slugs);
    }

    private async Task<Dictionary<Guid, IdentityRef>> PeopleAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken)
    {
        var people = new Dictionary<Guid, IdentityRef>();
        foreach (var id in ids.Distinct())
        {
            people[id] = IdentityRef.Of(
                await identities.FindAsync(id, cancellationToken)
                ?? throw new InvalidOperationException($"Identity {id} has no row."));
        }

        return people;
    }
}

/// <summary>
/// The tree of a space, without the bodies. Not paginated: three levels are
/// small, and this list is the navigation rather than a page of results
/// (VISION 18).
/// </summary>
public sealed class ListSpacePages(ISpaces spaces, SpaceScope scope, ISpacePages pages, SpacePageAssembler assembler, InstanceSettings settings)
{
    public async Task<IReadOnlyList<SpacePageSummaryShape>> ExecuteAsync(string name, CancellationToken cancellationToken)
    {
        var space = await spaces.LiveAsync(scope, name, settings, cancellationToken);

        return await assembler.TreeAsync(space, await pages.TreeAsync(space.Id, cancellationToken), cancellationToken);
    }
}

public sealed class ReadSpacePage(ISpaces spaces, SpaceScope scope, ISpacePages pages, SpacePageAssembler assembler, InstanceSettings settings)
{
    public async Task<SpacePageShape> ExecuteAsync(string name, string path, CancellationToken cancellationToken)
    {
        var space = await spaces.LiveAsync(scope, name, settings, cancellationToken);

        return await assembler.CompleteAsync(
            space, await pages.LiveAsync(space, path, settings, cancellationToken), cancellationToken);
    }
}

/// <summary>
/// A page under another, or directly under the space. Whoever may see the
/// space may write in it, agents included: the bracket is a human's to draw
/// and the work inside it is not (VISION 18, ADR 0027).
/// </summary>
public sealed class CreateSpacePage(
    ICallerIdentity callerIdentity,
    ISpaces spaces,
    SpaceScope scope,
    ISpacePages pages,
    IHistory history,
    ITransactions transactions,
    SpacePageAssembler assembler,
    InstanceSettings settings,
    TimeProvider clock)
{
    public async Task<SpacePageShape> ExecuteAsync(string name, CreateSpacePageRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var caller = callerIdentity.Caller;

        var space = await spaces.LiveAsync(scope, name, settings, cancellationToken);
        var parent = await pages.ParentAsync(space, request.Parent, settings, cancellationToken);
        var slug = Validated.Field("slug", () => Slug.Normalize(request.Slug ?? string.Empty));
        var title = Validated.Field("title", () => SpacePage.NormalizeTitle(request.Title!));

        await pages.TakenAsync(space, parent, slug, settings, cancellationToken);

        var page = await transactions.RunAsync(async () =>
        {
            var now = clock.GetUtcNow();
            var created = SpacePage.Create(space.Id, parent, slug, title, request.Body, caller.Id, now);

            pages.Add(created);
            history.Add(HistoryEntry.OnSpacePage(created.Id, caller.Id, now, HistoryField.Created));

            await pages.SaveAsync(cancellationToken);
            return created;
        }, cancellationToken);

        return await assembler.CompleteAsync(space, page, cancellationToken);
    }
}

/// <summary>
/// Title, the document and the address, guarded by <c>If-Match</c> against
/// <c>updated_at</c> — the guard a text a human and an agent both edit needs.
/// The history records that the body changed, not how; a rename carries both
/// slugs, because nothing else keeps the old address.
/// </summary>
/// <remarks>
/// The parent is not here. Moving a page rewrites the depth of everything
/// under it, which is an act with an outcome of its own rather than a field
/// somebody sets in passing — the line ADR 0016 draws for the status.
/// </remarks>
public sealed class ChangeSpacePage(
    ICallerIdentity callerIdentity,
    ISpaces spaces,
    SpaceScope scope,
    ISpacePages pages,
    IHistory history,
    ITransactions transactions,
    SpacePageAssembler assembler,
    InstanceSettings settings,
    TimeProvider clock)
{
    public async Task<SpacePageShape> ExecuteAsync(
        string name, string path, SpacePageChanges changes, string? ifMatch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(changes);
        var caller = callerIdentity.Caller;

        var space = await spaces.LiveAsync(scope, name, settings, cancellationToken);
        var before = await pages.LiveAsync(space, path, settings, cancellationToken);
        var expected = ChangeIssue.Expected(ifMatch);

        var renamed = changes.Slug is null ? null : Validated.Field("slug", () => Slug.Normalize(changes.Slug));
        if (renamed is not null && renamed != before.Slug)
        {
            var parent = before.ParentId is { } id ? await pages.FindByIdAsync(id, cancellationToken) : null;
            await pages.TakenAsync(space, parent, renamed, settings, cancellationToken);
        }

        var page = await transactions.RunAsync(async () =>
        {
            var row = await pages.LoadForWriteAsync(before.Id, cancellationToken)
                ?? throw new Refusal(RefusalCode.NotFound, $"No page {space.Name}/{path}.");

            if (expected is { } version && row.UpdatedAt != version)
            {
                throw new Refusal(
                    RefusalCode.Stale,
                    $"{space.Name}/{path} changed at {row.UpdatedAt:yyyy-MM-dd'T'HH:mm:ss.ffffff'Z'}; you last read it at {version:yyyy-MM-dd'T'HH:mm:ss.ffffff'Z'}.",
                    new Dictionary<string, object?> { ["current"] = await assembler.CompleteAsync(space, row, cancellationToken) });
            }

            var now = clock.GetUtcNow();

            if (renamed is not null && renamed != row.Slug)
            {
                var old = row.Slug;
                row.Rename(renamed, caller.Id, now);
                history.Add(HistoryEntry.OnSpacePage(row.Id, caller.Id, now, HistoryField.Slug, old, row.Slug));
            }

            if (changes.Title is not null && changes.Title != row.Title)
            {
                var old = row.Title;
                Validated.Field("title", () => { row.Retitle(changes.Title, caller.Id, now); return true; });
                history.Add(HistoryEntry.OnSpacePage(row.Id, caller.Id, now, HistoryField.Title, old, row.Title));
            }

            if (changes.BodyGiven && (changes.Body ?? string.Empty) != row.Body)
            {
                row.Rewrite(changes.Body, caller.Id, now);
                history.Add(HistoryEntry.OnSpacePage(row.Id, caller.Id, now, HistoryField.Body));
            }

            await pages.SaveAsync(cancellationToken);
            return row;
        }, cancellationToken);

        return await assembler.CompleteAsync(space, page, cancellationToken);
    }
}

/// <param name="Space">The space it lands in, or nothing to stay in this one.</param>
/// <param name="Parent">The page it lands under, or nothing for the root of that space.</param>
public sealed record SpacePageMove(string? Space, string? Parent);

/// <summary>
/// A page under another parent, in this space or another one, with everything
/// below it. Moving is an act and not a field of the change, because it
/// rewrites a subtree and has outcomes of its own — the line ADR 0016 draws
/// for the status.
/// </summary>
/// <remarks>
/// Four things are asked before anything is written: that the target space is
/// one the caller may see, that the target is not the page itself or a page
/// below it, that the slug is free under the new parent, and that the subtree
/// still fits under the third level. The refusals say which of the four it
/// was.
/// </remarks>
public sealed class MoveSpacePage(
    ICallerIdentity callerIdentity,
    ISpaces spaces,
    SpaceScope scope,
    ISpacePages pages,
    IHistory history,
    ITransactions transactions,
    SpacePageAssembler assembler,
    InstanceSettings settings,
    TimeProvider clock)
{
    public async Task<SpacePageShape> ExecuteAsync(
        string name, string path, SpacePageMove move, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(move);
        var caller = callerIdentity.Caller;

        var space = await spaces.LiveAsync(scope, name, settings, cancellationToken);
        var page = await pages.LiveAsync(space, path, settings, cancellationToken);

        // A space the caller may not see refuses here, as any other space
        // would: the move is out of one bracket and into another, and both
        // ends are asked.
        var target = string.IsNullOrWhiteSpace(move.Space)
            ? space
            : await spaces.LiveAsync(scope, move.Space, settings, cancellationToken);
        var parent = await pages.ParentAsync(target, move.Parent, settings, cancellationToken);

        if (target.Id == page.SpaceId && parent?.Id == page.ParentId)
        {
            return await assembler.CompleteAsync(space, page, cancellationToken);
        }

        var subtree = await pages.DescendantsAsync(page.Id, cancellationToken);

        if (parent is not null && (parent.Id == page.Id || subtree.Any(p => p.Id == parent.Id)))
        {
            throw new Refusal(
                RefusalCode.Cycle,
                $"{space.Name}/{path} cannot hang under itself or under a page below it.");
        }

        var depth = parent is null ? 0 : parent.Depth + 1;
        var height = subtree.Count == 0 ? 0 : subtree.Max(p => p.Depth) - page.Depth;
        if (height > SpacePage.Room(depth))
        {
            throw new Refusal(
                RefusalCode.TooDeep,
                $"{space.Name}/{path} is {height + 1} levels tall and there is room for {SpacePage.Room(depth) + 1} where it would land.",
                new Dictionary<string, object?> { ["depth"] = height });
        }

        await pages.TakenAsync(target, parent, page.Slug, settings, cancellationToken);

        var parentPath = parent is null ? null : string.Join(SpacePagePath.Separator, SpacePagePath.Segments(move.Parent)!);
        var from = $"{space.Name}/{string.Join(SpacePagePath.Separator, SpacePagePath.Segments(path)!)}";
        var to = $"{target.Name}/{SpacePagePath.Of(parentPath, page.Slug)}";

        var moved = await transactions.RunAsync(async () =>
        {
            var row = await pages.LoadForWriteAsync(page.Id, cancellationToken)
                ?? throw new Refusal(RefusalCode.NotFound, $"No page {space.Name}/{path}.");

            var now = clock.GetUtcNow();
            var levels = depth - row.Depth;
            row.MoveUnder(target.Id, parent, caller.Id, now);

            // The descendants are carried in one statement. They are not
            // moved — the page was — so the history stands at the page and
            // names the two addresses; nothing else keeps the old one.
            await pages.ShiftDescendantsAsync(row.Id, target.Id, levels, cancellationToken);
            history.Add(HistoryEntry.OnSpacePage(row.Id, caller.Id, now, HistoryField.Parent, from, to));

            await pages.SaveAsync(cancellationToken);
            return row;
        }, cancellationToken);

        return await assembler.CompleteAsync(target, moved, cancellationToken);
    }
}

/// <summary>
/// Soft, with the grace period of everything else (ADR 0013), and with the
/// subtree: the page and every live page under it go in one act, so that a
/// tree never has a live page hanging under a deleted one.
/// </summary>
/// <remarks>
/// Whoever may write in the space may delete, agents included — the grace
/// period is the net, not a permission. That is the difference from the space
/// itself, which an administrator deletes: a bracket is a decision about more
/// than one person's text.
/// </remarks>
public sealed class DeleteSpacePage(
    ICallerIdentity callerIdentity,
    ISpaces spaces,
    SpaceScope scope,
    ISpacePages pages,
    IHistory history,
    ITransactions transactions,
    InstanceSettings settings,
    TimeProvider clock)
{
    /// <returns>How many pages went, the page itself included.</returns>
    public async Task<int> ExecuteAsync(string name, string path, CancellationToken cancellationToken)
    {
        var caller = callerIdentity.Caller;
        var space = await spaces.LiveAsync(scope, name, settings, cancellationToken);
        var page = await pages.LiveAsync(space, path, settings, cancellationToken);
        var subtree = await pages.DescendantsAsync(page.Id, cancellationToken);

        await transactions.RunAsync(async () =>
        {
            var row = await pages.LoadForWriteAsync(page.Id, cancellationToken)
                ?? throw new Refusal(RefusalCode.NotFound, $"No page {space.Name}/{path}.");

            var now = clock.GetUtcNow();
            row.Delete(caller.Id, now);
            await pages.DeleteDescendantsAsync(row.Id, caller.Id, now, cancellationToken);

            // One entry per page. The grace period is read at each row, so the
            // reason it is away is written at each row too.
            history.Add(HistoryEntry.OnSpacePage(row.Id, caller.Id, now, HistoryField.Deleted, null, "true"));
            foreach (var gone in subtree)
            {
                history.Add(HistoryEntry.OnSpacePage(gone.Id, caller.Id, now, HistoryField.Deleted, null, "true"));
            }

            await pages.SaveAsync(cancellationToken);
            return true;
        }, cancellationToken);

        return subtree.Count + 1;
    }
}

/// <summary>
/// Back with everything that went with it, and nothing else: the page and the
/// rows carrying its id: a page somebody deleted on its own beforehand did not
/// go along, so it does not come back.
/// </summary>
public sealed class RestoreSpacePage(
    ICallerIdentity callerIdentity,
    ISpaces spaces,
    SpaceScope scope,
    ISpacePages pages,
    IHistory history,
    ITransactions transactions,
    SpacePageAssembler assembler,
    InstanceSettings settings,
    TimeProvider clock)
{
    public async Task<SpacePageShape> ExecuteAsync(string name, string path, CancellationToken cancellationToken)
    {
        var caller = callerIdentity.Caller;
        var space = await spaces.LiveAsync(scope, name, settings, cancellationToken);
        var page = await pages.AnyAsync(space, path, cancellationToken);

        if (!page.Deleted)
        {
            throw new Refusal(RefusalCode.Transition, $"Page {space.Name}/{path} is not deleted.");
        }

        // A live page under a deleted one is the state this whole act exists
        // to keep out, so the parent comes back first or nothing does.
        if (page.ParentId is { } parentId)
        {
            var parent = await pages.FindByIdAsync(parentId, cancellationToken);
            if (parent is null || parent.Deleted)
            {
                var segments = SpacePagePath.Segments(path)!;
                throw new Refusal(
                    RefusalCode.Transition,
                    $"The page above {space.Name}/{path} is deleted; restore {space.Name}/{string.Join(SpacePagePath.Separator, segments.Take(segments.Count - 1))} first.");
            }
        }

        var companions = await pages.CompanionsAsync(page.Id, cancellationToken);

        var restored = await transactions.RunAsync(async () =>
        {
            var row = await pages.LoadForWriteAsync(page.Id, cancellationToken)
                ?? throw new Refusal(RefusalCode.NotFound, $"No page {space.Name}/{path}.");

            var now = clock.GetUtcNow();
            row.Restore();
            await pages.RestoreCompanionsAsync(row.Id, cancellationToken);

            history.Add(HistoryEntry.OnSpacePage(row.Id, caller.Id, now, HistoryField.Deleted, "true", null));
            foreach (var back in companions)
            {
                history.Add(HistoryEntry.OnSpacePage(back.Id, caller.Id, now, HistoryField.Deleted, "true", null));
            }

            await pages.SaveAsync(cancellationToken);
            return row;
        }, cancellationToken);

        return await assembler.CompleteAsync(space, restored, cancellationToken);
    }
}
