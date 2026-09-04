using Planaffe.Application.Ports;
using Planaffe.Domain;
using Planaffe.Domain.Projects;
using Planaffe.Domain.Releases;

namespace Planaffe.Application.Acts;

/// <summary>A project as <c>docs/api.md</c> shapes it. The suffix is dropped in the contract.</summary>
public sealed record ProjectShape(
    string Key,
    string Name,
    bool TriageRequired,
    bool ReviewRequired,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static ProjectShape Of(Project project) =>
        new(project.Key, project.Name, project.TriageRequired, project.ReviewRequired, project.CreatedAt, project.UpdatedAt);
}

/// <summary>
/// The lookups every project act starts with: the live project by key, or the
/// refusal that says what is there instead.
/// </summary>
public static class ProjectLookup
{
    /// <exception cref="Refusal"><c>not-found</c>, or <c>deleted</c> with <c>restorable_until</c>.</exception>
    public static async Task<Project> LiveAsync(
        this IProjects projects, string key, InstanceSettings settings, CancellationToken cancellationToken)
    {
        var project = await projects.FindByKeyAsync(key.Trim().ToUpperInvariant(), cancellationToken)
            ?? throw new Refusal(RefusalCode.NotFound, $"No project {key}.");

        return project.Deleted
            ? throw new Refusal(
                RefusalCode.Deleted,
                $"Project {project.Key} is deleted and can be restored until at least {project.DeletedAt!.Value + settings.DeletionGrace:u}.",
                new Dictionary<string, object?> { ["restorable_until"] = project.DeletedAt.Value + settings.DeletionGrace })
            : project;
    }

    /// <summary>The same live lookup as <see cref="LiveAsync"/>, as a fresh read for a long poll.</summary>
    public static async Task<Project> LiveForReadAsync(
        this IProjects projects, string key, InstanceSettings settings, CancellationToken cancellationToken)
    {
        var project = await projects.FindByKeyForReadAsync(key.Trim().ToUpperInvariant(), cancellationToken)
            ?? throw new Refusal(RefusalCode.NotFound, $"No project {key}.");

        return project.Deleted
            ? throw new Refusal(
                RefusalCode.Deleted,
                $"Project {project.Key} is deleted and can be restored until at least {project.DeletedAt!.Value + settings.DeletionGrace:u}.",
                new Dictionary<string, object?> { ["restorable_until"] = project.DeletedAt.Value + settings.DeletionGrace })
            : project;
    }
}

/// <summary>
/// A user creates a project: the key that will prefix everything in it, typed
/// by a person and never changed (ADR 0015), the two switches, and the
/// <c>kind</c> group with its three labels (VISION 8).
/// </summary>
public sealed class CreateProject(ICallerIdentity callerIdentity, IProjects projects, TimeProvider clock)
{
    public async Task<ProjectShape> ExecuteAsync(
        string? key, string? name, bool triageRequired, bool reviewRequired, CancellationToken cancellationToken)
    {
        var caller = callerIdentity.Caller.RequireUser("create a project");

        var normalizedKey = Validated.Field("key", () => ProjectKey.Normalize(key!));
        var normalizedName = Validated.Field("name", () => Project.NormalizeName(name!));

        if (await projects.KeyTakenAsync(normalizedKey, cancellationToken))
        {
            throw Refusal.Validation("key", $"The key {normalizedKey} is taken — by a project, or by a deleted one waiting out its grace period.");
        }

        var now = clock.GetUtcNow();
        var project = Project.Create(normalizedKey, normalizedName, caller.Id, now);
        project.RequireTriage(triageRequired, now);
        project.RequireReview(reviewRequired, now);

        await projects.AddAsync(project, Label.Kind(project.Id, now), Release.Open(project.Id, now),
            ProjectAccess.Grant(project.Id, caller.Id, caller.Id, now), cancellationToken);

        return ProjectShape.Of(project);
    }
}

/// <summary>Every project the caller sees — in cut one, every live project.</summary>
public sealed class ListProjects(ICallerIdentity callerIdentity, IProjects projects, IProjectAccess access)
{
    public async Task<IReadOnlyList<ProjectShape>> ExecuteAsync(CancellationToken cancellationToken)
    {
        var ids = await access.ProjectIdsAsync(callerIdentity.Caller.OwnerId ?? callerIdentity.Caller.Id, cancellationToken);
        return [.. (await projects.ListAsync(cancellationToken)).Where(project => ids.Contains(project.Id)).Select(ProjectShape.Of)];
    }
}

public sealed class ReadProject(IProjects projects, ProjectScope scope, InstanceSettings settings)
{
    public async Task<ProjectShape> ExecuteAsync(string key, CancellationToken cancellationToken)
    {
        var project = await projects.LiveAsync(key, settings, cancellationToken);
        await scope.RequireAsync(project.Id, cancellationToken);
        return ProjectShape.Of(project);
    }
}

/// <summary>What a <c>PATCH</c> carries: only what is present changes.</summary>
public sealed record ProjectChanges(string? Name, bool? TriageRequired, bool? ReviewRequired);

/// <summary>A user changes the name or the switches; the key is immutable.</summary>
public sealed class ChangeProject(ICallerIdentity callerIdentity, IProjects projects, ProjectScope scope, InstanceSettings settings, TimeProvider clock)
{
    public async Task<ProjectShape> ExecuteAsync(string key, ProjectChanges changes, CancellationToken cancellationToken)
    {
        callerIdentity.Caller.RequireUser("change a project");
        ArgumentNullException.ThrowIfNull(changes);

        var project = await projects.LiveAsync(key, settings, cancellationToken);
        await scope.RequireAsync(project.Id, cancellationToken);
        var now = clock.GetUtcNow();

        if (changes.Name is not null)
        {
            Validated.Field("name", () => { project.Rename(changes.Name, now); return true; });
        }

        if (changes.TriageRequired is { } triage)
        {
            project.RequireTriage(triage, now);
        }

        if (changes.ReviewRequired is { } review)
        {
            project.RequireReview(review, now);
        }

        await projects.SaveAsync(project, cancellationToken);

        return ProjectShape.Of(project);
    }
}

/// <summary>
/// An administrator deletes a project with everything in it (ADR 0013). The API
/// asks for nothing to be typed; the CLI does.
/// </summary>
public sealed class DeleteProject(ICallerIdentity callerIdentity, IProjects projects, InstanceSettings settings, TimeProvider clock)
{
    public async Task ExecuteAsync(string key, CancellationToken cancellationToken)
    {
        var caller = callerIdentity.Caller.RequireAdministrator("delete a project");

        var project = await projects.LiveAsync(key, settings, cancellationToken);
        project.Delete(caller.Id, clock.GetUtcNow());

        await projects.SaveAsync(project, cancellationToken);
    }
}

/// <summary>Back, with everything in it. A project that is not deleted is <c>transition</c>.</summary>
public sealed class RestoreProject(ICallerIdentity callerIdentity, IProjects projects)
{
    public async Task<ProjectShape> ExecuteAsync(string key, CancellationToken cancellationToken)
    {
        callerIdentity.Caller.RequireAdministrator("restore a project");

        var project = await projects.FindByKeyAsync(key.Trim().ToUpperInvariant(), cancellationToken)
            ?? throw new Refusal(RefusalCode.NotFound, $"No project {key}.");

        if (!project.Deleted)
        {
            throw new Refusal(RefusalCode.Transition, $"Project {project.Key} is not deleted.");
        }

        project.Restore();
        await projects.SaveAsync(project, cancellationToken);

        return ProjectShape.Of(project);
    }
}
