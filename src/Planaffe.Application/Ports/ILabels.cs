using Planaffe.Domain.Projects;

namespace Planaffe.Application.Ports;

/// <summary>The label rows of a project.</summary>
public interface ILabels
{
    /// <summary>The live labels of a project, by name.</summary>
    Task<IReadOnlyList<Label>> ListAsync(Guid projectId, CancellationToken cancellationToken);

    /// <summary>By name within the project, deleted or not.</summary>
    Task<Label?> FindAsync(Guid projectId, string name, CancellationToken cancellationToken);

    /// <exception cref="Domain.Refusal"><c>validation</c> on <c>name</c> when the unique index refuses it.</exception>
    Task AddAsync(Label label, CancellationToken cancellationToken);

    /// <inheritdoc cref="AddAsync"/>
    Task SaveAsync(Label label, CancellationToken cancellationToken);

    /// <summary>
    /// The keys of the live issues that carry <paramref name="label"/> and
    /// another live label of <paramref name="group"/> — the issues that would
    /// end up with two of one group if the label moved there.
    /// </summary>
    Task<IReadOnlyList<string>> IssuesWithAnotherOfGroupAsync(Label label, string group, CancellationToken cancellationToken);
}
