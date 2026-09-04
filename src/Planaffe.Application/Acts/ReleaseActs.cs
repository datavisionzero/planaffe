using Planaffe.Application.Ports;
using Planaffe.Domain;
using Planaffe.Domain.Releases;

namespace Planaffe.Application.Acts;

public sealed record ReleaseSummaryShape(string Name, ReleaseStatus Status, string Description, DateTimeOffset? PublishedAt, IdentityRef? PublishedBy, int Issues);
public sealed record ReleaseShape(string Name, ReleaseStatus Status, string Description, DateTimeOffset? PublishedAt, IdentityRef? PublishedBy, IReadOnlyList<IssueSummaryShape> Issues);
public sealed record PublishReleaseRequest(string? Name, string? Description);

public sealed class ReleaseAssembler(IReleases releases, IIdentities identities, IssueAssembler issues)
{
    public async Task<ReleaseSummaryShape> SummaryAsync(Release release, CancellationToken ct)
    {
        var rows = await releases.IssuesAsync(release.Id, ct);
        return new(DisplayName(release), release.Status, release.Description, release.PublishedAt, await PublisherAsync(release, ct), rows.Count);
    }

    public async Task<ReleaseShape> CompleteAsync(Release release, CancellationToken ct)
    {
        var rows = await releases.IssuesAsync(release.Id, ct);
        return new(DisplayName(release), release.Status, release.Description, release.PublishedAt, await PublisherAsync(release, ct), await issues.SummariesAsync(rows, ct));
    }

    private async Task<IdentityRef?> PublisherAsync(Release release, CancellationToken ct) =>
        release.PublishedBy is { } id && await identities.FindAsync(id, ct) is { } identity ? IdentityRef.Of(identity) : null;
    private static string DisplayName(Release release) => release.Name ?? "unreleased";
}

public sealed class ListReleases(IProjects projects, ProjectScope scope, IReleases releases, ReleaseAssembler assembler, InstanceSettings settings)
{
    public async Task<IReadOnlyList<ReleaseSummaryShape>> ExecuteAsync(string projectKey, CancellationToken ct)
    {
        var project = await projects.LiveAsync(projectKey, settings, ct);
        await scope.RequireAsync(project.Id, ct);
        var result = new List<ReleaseSummaryShape>();
        foreach (var release in await releases.ListAsync(project.Id, ct)) result.Add(await assembler.SummaryAsync(release, ct));
        return result;
    }
}

public sealed class ReadRelease(IProjects projects, ProjectScope scope, IReleases releases, ReleaseAssembler assembler, InstanceSettings settings)
{
    public async Task<ReleaseShape> ExecuteAsync(string projectKey, string name, CancellationToken ct)
    {
        var project = await projects.LiveAsync(projectKey, settings, ct);
        await scope.RequireAsync(project.Id, ct);
        var release = await releases.FindAsync(project.Id, name, ct) ?? throw new Refusal(RefusalCode.NotFound, $"No release {name} in {project.Key}.");
        return await assembler.CompleteAsync(release, ct);
    }
}

public sealed class ChangeRelease(IProjects projects, ProjectScope scope, IReleases releases, ITransactions transactions, ReleaseAssembler assembler, InstanceSettings settings, TimeProvider clock)
{
    public async Task<ReleaseShape> ExecuteAsync(string projectKey, string name, string? description, CancellationToken ct)
    {
        var project = await projects.LiveAsync(projectKey, settings, ct);
        await scope.RequireAsync(project.Id, ct);
        var changed = await transactions.RunAsync(async () =>
        {
            var release = await releases.LoadForWriteAsync(project.Id, name, ct) ?? throw new Refusal(RefusalCode.NotFound, $"No release {name} in {project.Key}.");
            release.Describe(description, clock.GetUtcNow());
            await releases.SaveAsync(ct);
            return release;
        }, ct);
        return await assembler.CompleteAsync(changed, ct);
    }
}

public sealed class PublishRelease(ICallerIdentity callerIdentity, IProjects projects, ProjectScope scope, IReleases releases, ITransactions transactions, ReleaseAssembler assembler, InstanceSettings settings, TimeProvider clock)
{
    public async Task<ReleaseShape> ExecuteAsync(string projectKey, PublishReleaseRequest request, CancellationToken ct)
    {
        var project = await projects.LiveAsync(projectKey, settings, ct);
        await scope.RequireAsync(project.Id, ct);
        var name = Validated.Field("name", () => Release.NormalizeName(request.Name!));
        if (await releases.NameTakenAsync(project.Id, name, ct)) throw new Refusal(RefusalCode.ReleaseExists, $"Release {name} already exists.");
        var published = await transactions.RunAsync(async () =>
        {
            var open = await releases.LoadOpenForWriteAsync(project.Id, ct) ?? throw new InvalidOperationException("Project has no open release.");
            if (await releases.NameTakenAsync(project.Id, name, ct)) throw new Refusal(RefusalCode.ReleaseExists, $"Release {name} already exists.");
            var now = clock.GetUtcNow();
            open.Publish(name, request.Description, callerIdentity.Caller.Id, now);
            releases.Add(Release.Open(project.Id, now));
            await releases.SaveAsync(ct);
            return open;
        }, ct);
        return await assembler.CompleteAsync(published, ct);
    }
}
