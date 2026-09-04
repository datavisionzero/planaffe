using Planaffe.Application.Ports;
using Planaffe.Domain.Identities;
using Planaffe.Domain.Projects;

namespace Planaffe.Application.Acts;

/// <summary>
/// Turns rows into the two issue shapes, a page at a time: one query per
/// attachment kind for the whole page rather than one per issue.
/// </summary>
public sealed class IssueAssembler(
    IIssues issues,
    IIdentities identities,
    IEpics epics,
    IProjects projects,
    ILabels labels)
{
    public async Task<IReadOnlyList<IssueSummaryShape>> SummariesAsync(
        IReadOnlyList<IssueRow> rows, CancellationToken cancellationToken)
    {
        if (rows.Count == 0)
        {
            return [];
        }

        var ids = rows.Select(r => r.Id).ToArray();
        var labelRows = await issues.LabelsOfAsync(ids, cancellationToken);
        var blockers = await issues.BlockersOfAsync(ids, cancellationToken);
        var questions = await issues.OpenQuestionCountsAsync(ids, cancellationToken);
        var children = await issues.OpenSubIssueCountsAsync(ids, cancellationToken);
        var parents = (await issues.FindLiveManyAsync(rows.Select(r => r.ParentId).OfType<Guid>(), cancellationToken)).ToDictionary(r => r.Id);
        var epicKeys = await EpicKeysAsync(rows.Select(r => r.EpicId), cancellationToken);
        var people = await identities.FindManyAsync(
            rows.SelectMany(r => new[] { r.AssigneeId, r.ClaimedBy, r.DeletedBy }).OfType<Guid>().Distinct(),
            cancellationToken);

        return
        [
            .. rows.Select(row =>
            {
                var blockedBy = blockers.Where(e => e.NearId == row.Id).Select(e => e.Far).ToArray();
                return new IssueSummaryShape(
                    row.Key,
                    row.ProjectKey,
                    row.Title,
                    row.Status,
                    row.Ready,
                    row.Priority,
                    [.. labelRows.Where(l => l.IssueId == row.Id).Select(l => l.Label.Name).Order(StringComparer.Ordinal)],
                    row.EpicId is { } epicId ? epicKeys.GetValueOrDefault(epicId) : null,
                    row.ParentId is { } parentId && parents.TryGetValue(parentId, out var parent) ? parent.Key : null,
                    Ref(people, row.AssigneeId),
                    Claim(people, row),
                    [.. blockedBy.Select(b => new BlockerRefShape(b.Key, !b.Closed))],
                    questions.GetValueOrDefault(row.Id),
                    blockedBy.Count(b => !b.Closed),
                    children.GetValueOrDefault(row.Id),
                    row.CreatedAt,
                    row.UpdatedAt,
                    row.ClosedAt,
                    row.DeletedAt,
                    Ref(people, row.DeletedBy));
            }),
        ];
    }

    public async Task<IssueShape> CompleteAsync(IssueRow row, CancellationToken cancellationToken)
    {
        Guid[] ids = [row.Id];
        var labelRows = await issues.LabelsOfAsync(ids, cancellationToken);
        var blockedBy = (await issues.BlockersOfAsync(ids, cancellationToken)).Select(e => e.Far).ToArray();
        var blocks = (await issues.BlockedByEachAsync(ids, cancellationToken)).Select(e => e.Far).ToArray();
        var comments = await issues.CommentsOfAsync(row.Id, cancellationToken);
        var questions = await issues.QuestionsOfAsync(row.Id, cancellationToken);
        var epic = row.EpicId is { } epicId ? await epics.FindAsync(epicId, cancellationToken) : null;
        var parent = row.ParentId is { } parentId ? (await issues.FindLiveManyAsync([parentId], cancellationToken)).SingleOrDefault() : null;
        var subIssues = await issues.SubIssuesOfAsync(row.Id, cancellationToken);
        var project = await projects.FindByKeyAsync(row.ProjectKey, cancellationToken)
            ?? throw new InvalidOperationException($"Issue {row.Key} has no project row.");
        var projectLabels = await labels.ListAsync(project.Id, cancellationToken);

        var people = await identities.FindManyAsync(
            new[] { row.AuthorId, row.AssigneeId, row.ClaimedBy }
                .Concat(comments.Select(c => (Guid?)c.AuthorId))
                .Concat(questions.SelectMany(q => new[] { (Guid?)q.AskedBy, q.AnsweredBy }))
                .OfType<Guid>()
                .Distinct(),
            cancellationToken);

        return new IssueShape(
            row.Key,
            row.ProjectKey,
            row.Title,
            row.Description,
            row.Result,
            row.Status,
            row.Ready,
            row.Priority,
            [.. labelRows.Select(l => LabelShape.Of(l.Label)).OrderBy(l => l.Name, StringComparer.Ordinal)],
            epic is null ? null : new EpicRefShape(EpicKey.Of(row.ProjectKey, epic.Number), epic.Title, epic.Description, epic.Status),
            parent is null ? null : Ref(parent),
            [.. subIssues.Select(Ref)],
            Ref(people, row.AssigneeId),
            Claim(people, row),
            Ref(people, row.AuthorId)!,
            [.. blockedBy.Select(Link)],
            [.. blocks.Select(Link)],
            questions.Count(q => q.Open),
            blockedBy.Count(b => !b.Closed),
            subIssues.Count(i => !i.Closed),
            [.. comments.Select(c => new CommentShape(c.Id, Ref(people, c.AuthorId)!, c.Body, c.CreatedAt))],
            [.. questions.Select(q => new QuestionShape(q.Id, q.Text, Ref(people, q.AskedBy)!, q.AskedAt, q.Answer, Ref(people, q.AnsweredBy), q.AnsweredAt))],
            new ProjectContextShape(project.Key, project.Name, project.TriageRequired, project.ReviewRequired, [.. projectLabels.Select(LabelShape.Of)]),
            row.CreatedAt,
            row.UpdatedAt,
            row.ClosedAt);
    }

    private async Task<IReadOnlyDictionary<Guid, string>> EpicKeysAsync(IEnumerable<Guid?> epicIds, CancellationToken cancellationToken)
    {
        var ids = epicIds.OfType<Guid>().Distinct().ToArray();
        if (ids.Length == 0)
        {
            return new Dictionary<Guid, string>();
        }

        var found = await epics.FindManyAsync(ids, cancellationToken);
        var projectKeys = new Dictionary<Guid, string>();
        foreach (var projectId in found.Select(e => e.ProjectId).Distinct())
        {
            var project = await projects.FindByIdAsync(projectId, cancellationToken);
            if (project is not null)
            {
                projectKeys[projectId] = project.Key;
            }
        }

        return found.ToDictionary(e => e.Id, e => EpicKey.Of(projectKeys[e.ProjectId], e.Number));
    }

    private static BlockerLinkShape Link(IssueRow far) => new(far.Key, far.Title, far.Status, !far.Closed);

    private static IssueRefShape Ref(IssueRow issue) => new(issue.Key, issue.Title);

    private static IdentityRef? Ref(IReadOnlyDictionary<Guid, Identity> people, Guid? id) =>
        id is { } known && people.TryGetValue(known, out var identity) ? IdentityRef.Of(identity) : null;

    private static ClaimShape? Claim(IReadOnlyDictionary<Guid, Identity> people, IssueRow row) =>
        row.ClaimedBy is { } holder && Ref(people, holder) is { } holderRef
            ? new ClaimShape(holderRef, row.ClaimedAt!.Value, row.ClaimExpiresAt)
            : null;
}
