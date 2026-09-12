using Planaffe.Domain.Spaces;

namespace Planaffe.Application.Ports;

/// <summary>
/// One hit of the knowledge base's search: the page, where it stands, and the
/// excerpt Postgres cut out of its body.
/// </summary>
/// <param name="TrailPaths">The addresses of the pages above it, from the space down.</param>
/// <param name="TrailTitles">Their titles, in the same order: a hit says where the page stands.</param>
/// <param name="Headline">The excerpt, with the words that matched between <see cref="Excerpt"/>'s two marks.</param>
public sealed record SpacePageHitRow(
    string Space,
    string SpaceTitle,
    string Path,
    string Title,
    IReadOnlyList<string> TrailPaths,
    IReadOnlyList<string> TrailTitles,
    string Headline);

/// <summary>
/// How an excerpt marks the words that matched: two control characters, taken
/// out of the body before the excerpt is cut, so that a page cannot write them
/// itself.
/// </summary>
/// <remarks>
/// Not <c>&lt;b&gt;</c>, which is what Postgres would mark with by default. An
/// application that never renders Markdown as HTML (ADR 0007) does not start
/// by putting a server's string into the browser's tree as markup, so the
/// marks are agreed here between the store and the act that reads them, and
/// what leaves the instance is a sequence of pieces with a flag each.
/// </remarks>
public static class Excerpt
{
    public const char Start = '\u0002';

    public const char Stop = '\u0003';
}

/// <summary>
/// The knowledge base's page rows (<c>docs/storage.md</c>, Space pages). A page
/// is found by the slugs from the space down, because that is its address
/// (ADR 0028), so every lookup here takes a parent rather than a space alone.
/// </summary>
/// <remarks>
/// There is no cursor and no page of results. A space's tree is three levels
/// deep and is read whole, because it is the navigation; what would make that
/// expensive is the body, and the list the acts build does not carry one
/// (ADR 0012).
/// </remarks>
public interface ISpacePages
{
    /// <summary>
    /// By slug under its parent — <paramref name="parentId"/> is <c>null</c>
    /// for a page directly under the space. Live only: what a reader and a
    /// writer may reach.
    /// </summary>
    Task<SpacePage?> FindLiveAsync(Guid spaceId, Guid? parentId, string slug, CancellationToken cancellationToken);

    /// <summary>By slug under its parent, deleted or not — for the <c>deleted</c> answer and for restore.</summary>
    Task<SpacePage?> FindAnyAsync(Guid spaceId, Guid? parentId, string slug, CancellationToken cancellationToken);

    Task<SpacePage?> FindByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Every live page of the space, in one query: parents before their
    /// children, siblings by title with the slug deciding a tie — the order a
    /// tree is drawn in, and the only order it has (VISION 18).
    /// </summary>
    Task<IReadOnlyList<SpacePage>> TreeAsync(Guid spaceId, CancellationToken cancellationToken);

    /// <summary>
    /// The live pages under this one, at every level, shallowest first. A
    /// subtree is asked for in one statement rather than one per level,
    /// because everything that happens to a page happens to it whole.
    /// </summary>
    Task<IReadOnlyList<SpacePage>> DescendantsAsync(Guid pageId, CancellationToken cancellationToken);

    /// <summary>
    /// The pages that went with this one when it was deleted — the rows
    /// carrying its id in <c>deleted_with</c>, deleted or not being a question
    /// that does not arise: nothing else clears the column.
    /// </summary>
    Task<IReadOnlyList<SpacePage>> CompanionsAsync(Guid pageId, CancellationToken cancellationToken);

    /// <summary>
    /// Soft-delete every live page under this one in one statement, stamping
    /// each with the same moment, the same hand and this page's id.
    /// </summary>
    Task DeleteDescendantsAsync(Guid pageId, Guid by, DateTimeOffset at, CancellationToken cancellationToken);

    /// <summary>Bring back every page that went with this one, in one statement.</summary>
    Task RestoreCompanionsAsync(Guid pageId, CancellationToken cancellationToken);

    /// <summary>
    /// Carry the descendants with the page that moved: the space it landed in
    /// and the levels it travelled, in one statement rather than a row at a
    /// time out of memory.
    /// </summary>
    Task ShiftDescendantsAsync(Guid pageId, Guid spaceId, int levels, CancellationToken cancellationToken);

    /// <summary>
    /// The pages of these spaces whose title or body matches, best first, with
    /// the way down to each and an excerpt of its body.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The set of spaces is a parameter and not a filter the caller may forget:
    /// a search that did not take one could not be written at all, and the one
    /// place that knows which spaces an agent may reach is
    /// <c>SpaceScope</c> (ADR 0027). A space that is closed to it is therefore
    /// missing from a row and from a count alike, because both come out of this
    /// one query.
    /// </para>
    /// <para>
    /// This is the one read in the knowledge base that ranks. Everywhere else
    /// <c>q</c> is a filter beside <c>label</c> on a list that keeps its own
    /// order (<c>docs/api.md</c>, Search); here the search is the navigation
    /// (VISION 18), and a navigation without an order is a list somebody reads
    /// through themselves.
    /// </para>
    /// </remarks>
    Task<IReadOnlyList<SpacePageHitRow>> SearchAsync(
        IReadOnlyCollection<Guid> spaceIds, string query, int limit, CancellationToken cancellationToken);

    /// <summary>The row, tracked and locked for the rest of the transaction.</summary>
    Task<SpacePage?> LoadForWriteAsync(Guid id, CancellationToken cancellationToken);

    void Add(SpacePage page);

    Task SaveAsync(CancellationToken cancellationToken);
}
