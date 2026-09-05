using Planaffe.Domain.Pages;
using Planaffe.Domain.Projects;

namespace Planaffe.Application.Ports;

public sealed record PageLabelRow(Guid PageId, Label Label);

/// <summary>
/// The page rows (<c>docs/storage.md</c>, Pages). A page is found by its slug
/// within a project, because that is its address (ADR 0021).
/// </summary>
/// <remarks>
/// There is no paged list here and no cursor. The wiki is flat, one project's
/// pages are few, and the list is slim — slug, title, labels, who touched it
/// last — for the same reason ADR 0012 makes an issue list slim: the body is
/// what would make it expensive, and the body is not in it.
/// </remarks>
public interface IPages
{
    /// <summary>By slug, live only — what a reader and a writer may reach.</summary>
    Task<Page?> FindLiveAsync(Guid projectId, string slug, CancellationToken cancellationToken);

    /// <summary>By slug, deleted or not — for the <c>deleted</c> answer and for restore.</summary>
    Task<Page?> FindAnyAsync(Guid projectId, string slug, CancellationToken cancellationToken);

    /// <summary>Every live page of the project, by slug; every label named has to be on it.</summary>
    Task<IReadOnlyList<Page>> ListAsync(Guid projectId, IReadOnlyList<string> labelNames, CancellationToken cancellationToken);

    /// <summary>The row, tracked and locked for the rest of the transaction.</summary>
    Task<Page?> LoadForWriteAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<PageLabelRow>> LabelsOfAsync(IReadOnlyCollection<Guid> pageIds, CancellationToken cancellationToken);

    void Add(Page page);

    void Attach(PageLabel attachment);

    Task DetachAsync(Guid pageId, Guid labelId, CancellationToken cancellationToken);

    Task SaveAsync(CancellationToken cancellationToken);
}
