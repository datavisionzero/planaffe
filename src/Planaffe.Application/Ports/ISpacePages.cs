using Planaffe.Domain.Spaces;

namespace Planaffe.Application.Ports;

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

    /// <summary>The row, tracked and locked for the rest of the transaction.</summary>
    Task<SpacePage?> LoadForWriteAsync(Guid id, CancellationToken cancellationToken);

    void Add(SpacePage page);

    Task SaveAsync(CancellationToken cancellationToken);
}
