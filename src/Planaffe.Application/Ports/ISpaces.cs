using Planaffe.Domain.Spaces;

namespace Planaffe.Application.Ports;

/// <summary>
/// The space rows (<c>docs/storage.md</c>, Spaces). A space is found by its
/// name, because that is its address (ADR 0021, ADR 0027).
/// </summary>
/// <remarks>
/// There is no cursor here. An instance has spaces the way it has projects —
/// few, and created one at a time by a human — so the list is the whole list,
/// ordered by the name it is addressed by.
/// </remarks>
public interface ISpaces
{
    /// <summary>By name, live only — what a reader and a writer may reach.</summary>
    Task<Space?> FindLiveAsync(string name, CancellationToken cancellationToken);

    /// <summary>By name, deleted or not — for the <c>deleted</c> answer and for restore.</summary>
    Task<Space?> FindAnyAsync(string name, CancellationToken cancellationToken);

    Task<Space?> FindByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Whether the name is taken, by a live space or by one waiting out its grace period.</summary>
    Task<bool> NameTakenAsync(string name, CancellationToken cancellationToken);

    /// <summary>The live spaces behind the ids the caller may see, by name.</summary>
    Task<IReadOnlyList<Space>> ListAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);

    /// <summary>Every space, deleted ones included — the instance administration's list.</summary>
    Task<IReadOnlyList<Space>> ListAllAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Of the ids given, those an agent may reach: live, and not closed to
    /// agents. One query rather than a filter every caller writes again, so
    /// that a route forgetting the switch is a route that does not compile.
    /// </summary>
    Task<IReadOnlySet<Guid>> OpenToAgentsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);

    /// <summary>The space and its creator's access in one transaction.</summary>
    /// <exception cref="Domain.Refusal"><c>validation</c> on <c>name</c> when the unique index refuses it.</exception>
    Task AddAsync(Space space, SpaceAccess creatorAccess, CancellationToken cancellationToken);

    /// <exception cref="Domain.Refusal"><c>validation</c> on <c>name</c> when a rename collides.</exception>
    Task SaveAsync(CancellationToken cancellationToken);
}
