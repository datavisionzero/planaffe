using Planaffe.Application.Ports;
using Planaffe.Domain.Projects;

namespace Planaffe.Application.Acts;

/// <summary>
/// The four groups of "needs you", counted, and when the oldest of them began
/// to wait. <c>oldest</c> is absent where nothing does.
/// </summary>
public sealed record StandingAttentionShape(int Question, int Review, int Unready, int Stuck, DateTimeOffset? Oldest);

/// <summary>What the work looks like — three numbers, and none of them a grade (ADR 0024).</summary>
public sealed record StandingWorkShape(int InProgress, int Ready, int Open);

/// <summary>One project on the overview.</summary>
public sealed record ProjectStandingShape(
    string Key,
    string Name,
    Standing Standing,
    StandingBecause Because,
    StandingAttentionShape NeedsYou,
    StandingWorkShape Work);

/// <summary>
/// The overview (<c>docs/human-interface.md</c>): every project the caller
/// sees, each with its standing. The document is the overview and the value on
/// each project is the standing, which is also why the two carry different
/// names in the contract.
/// </summary>
/// <param name="Agents">
/// Live agent tokens on the instance, said once for the whole answer: at
/// <c>0</c> nothing anywhere gets picked up, and the thing to do about it has
/// nothing to do with any single project.
/// </param>
public sealed record OverviewShape(IReadOnlyList<ProjectStandingShape> Projects, int Agents);

/// <summary>
/// How every project the caller sees is standing, in one read (ADR 0024). The
/// step is decided here and never by a client: two clients applying the
/// thresholds themselves are two clients that will eventually disagree about
/// the same project.
/// </summary>
public sealed class ReadStanding(
    ICallerIdentity callerIdentity,
    IProjects projects,
    IProjectAccess access,
    IIssues issues,
    TimeProvider clock)
{
    public async Task<OverviewShape> ExecuteAsync(CancellationToken cancellationToken)
    {
        var caller = callerIdentity.Caller;
        var ids = await access.ProjectIdsAsync(caller.OwnerId ?? caller.Id, cancellationToken);
        var mine = (await projects.ListAsync(cancellationToken)).Where(project => ids.Contains(project.Id)).ToArray();

        var rows = await issues.StandingAsync(
            [.. mine.Select(project => new StandingScope(project.Id, project.TriageRequired))], caller.Id, cancellationToken);

        var counted = rows.Projects.ToDictionary(row => row.ProjectId);
        var now = clock.GetUtcNow();

        var standings = mine
            .Select(project =>
            {
                // A project with no issues at all has no row of its own in the
                // aggregate; it is `clear`, and saying so here beats a left join
                // that has to invent every column.
                var row = counted.GetValueOrDefault(project.Id);
                var facts = new StandingFacts(
                    row?.Questions ?? 0,
                    row?.InReview ?? 0,
                    row?.Unready ?? 0,
                    row?.Stuck ?? 0,
                    row?.Oldest,
                    row?.InProgress ?? 0,
                    row?.Ready ?? 0,
                    row?.Blocked ?? 0,
                    row?.Open ?? 0,
                    rows.Agents);

                var standing = StandingScale.Of(facts, now);

                return new ProjectStandingShape(
                    project.Key,
                    project.Name,
                    standing,
                    StandingScale.Why(facts, standing),
                    new StandingAttentionShape(facts.Questions, facts.InReview, facts.Unready, facts.Stuck, facts.Oldest),
                    new StandingWorkShape(facts.InProgress, facts.Ready, facts.Open));
            })
            // Worst first, then whoever has been waiting longest, then the key —
            // so that what needs a human is the first thing under the reader's
            // eye and the order does not move on its own between two reads.
            .OrderByDescending(shape => shape.Standing)
            .ThenBy(shape => shape.NeedsYou.Oldest ?? DateTimeOffset.MaxValue)
            .ThenBy(shape => shape.Key, StringComparer.Ordinal)
            .ToArray();

        return new OverviewShape(standings, rows.Agents);
    }
}
