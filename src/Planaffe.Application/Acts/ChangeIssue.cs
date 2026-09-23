using System.Globalization;
using Planaffe.Application.Ports;
using Planaffe.Domain;
using Planaffe.Domain.History;
using Planaffe.Domain.Issues;
using Planaffe.Domain.Projects;

namespace Planaffe.Application.Acts;

/// <summary>
/// What a <c>PATCH</c> carries: present fields change, a field present as
/// <c>null</c> clears, an absent one is left alone.
/// </summary>
public sealed record IssueChanges(
    string? Title,
    bool DescriptionGiven,
    string? Description,
    bool ResultGiven,
    string? Result,
    Priority? Priority,
    bool? Ready,
    bool AssigneeGiven,
    string? Assignee,
    bool EpicGiven,
    string? Epic,
    bool ParentGiven,
    string? Parent,
    IReadOnlyList<string>? Labels,
    string? Status);

/// <summary>What a bulk <c>PATCH /issues</c> answers, in the order the keys were given.</summary>
public sealed record ChangedIssues(IReadOnlyList<IssueShape> Items);

/// <summary>
/// The scalar fields, the assignee and the epic by name and key, and the label
/// set with its groups enforced — under the row's lock, guarded by
/// <c>If-Match</c> against <c>updated_at</c> (<c>docs/api.md</c>, Concurrency
/// on text fields). Every change is a history entry.
/// </summary>
public sealed class ChangeIssue(
    ICallerIdentity callerIdentity,
    ProjectScope scope,
    IProjects projects,
    ILabels labels,
    IEpics epics,
    IIdentities identities,
    IIssues issues,
    IHistory history,
    ITransactions transactions,
    IssueAssembler assembler,
    InstanceSettings settings,
    TimeProvider clock)
{
    public async Task<IssueShape> ExecuteAsync(
        string key, IssueChanges changes, string? ifMatch, CancellationToken cancellationToken)
    {
        var after = await transactions.RunAsync(
            () => ExecuteWithinTransactionAsync(key, changes, ifMatch, cancellationToken),
            cancellationToken);
        return await assembler.CompleteAsync(after, cancellationToken);
    }

    /// <summary>The same change on several issues, committed only when every issue accepts it.</summary>
    /// <remarks>
    /// The answers are put together after the commit rather than inside the
    /// transaction: each is a dozen reads, and a hundred of them would hold
    /// every row lock of the request while they ran.
    /// </remarks>
    public async Task<ChangedIssues> ExecuteManyAsync(
        IReadOnlyList<string>? keys, IssueChanges changes, CancellationToken cancellationToken)
    {
        Paging.BulkKeys(keys);

        var rows = await transactions.RunAsync(async () =>
        {
            var changed = new List<IssueRow>(keys!.Count);
            foreach (var key in keys)
            {
                try
                {
                    changed.Add(await ExecuteWithinTransactionAsync(key, changes, ifMatch: null, cancellationToken));
                }
                catch (Refusal refusal)
                {
                    var extensions = refusal.Extensions.ToDictionary(pair => pair.Key, pair => pair.Value);
                    extensions["key"] = key;
                    throw new Refusal(refusal.Code, refusal.Detail, extensions);
                }
            }

            return changed;
        }, cancellationToken);

        var shapes = new List<IssueShape>(rows.Count);
        foreach (var row in rows)
        {
            shapes.Add(await assembler.CompleteAsync(row, cancellationToken));
        }

        return new ChangedIssues(shapes);
    }

    private async Task<IssueRow> ExecuteWithinTransactionAsync(
        string key, IssueChanges changes, string? ifMatch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(changes);
        var caller = callerIdentity.Caller;

        IssueStatus? parking = null;
        if (changes.Status is not null)
        {
            parking = changes.Status.ToLowerInvariant() switch
            {
                "backlog" => IssueStatus.Backlog,
                "todo" => IssueStatus.Todo,
                _ => throw new Refusal(RefusalCode.Transition, "The status changes through the acts — claim, release, close, review, reopen — not through PATCH; only backlog and todo are written, as parking (ADR 0016)."),
            };
        }

        var before = await issues.LiveAsync(key, settings, cancellationToken);
        await scope.RequireAsync(before.ProjectId, cancellationToken);

        var project = await projects.FindByIdAsync(before.ProjectId, cancellationToken)
            ?? throw new InvalidOperationException($"Issue {before.Key} has no project row.");

        var expected = Expected(ifMatch);
        var newLabels = changes.Labels is null ? null : await labels.ResolveLabelsAsync(project, changes.Labels, "labels", cancellationToken);
        var assignee = changes is { AssigneeGiven: true, Assignee: not null }
            ? await identities.FindByNameAsync(changes.Assignee, cancellationToken) ?? throw Refusal.Validation("assignee", $"No identity named {changes.Assignee}.")
            : null;
        var epic = changes is { EpicGiven: true, Epic: not null } ? await EpicAsync(project, changes.Epic, cancellationToken) : null;
        var parentRow = changes is { ParentGiven: true, Parent: not null }
            ? await ParentAsync(changes.Parent, cancellationToken)
            : null;

        // Against the parent the issue has after this change: `parent: null`
        // with an epic detaches first and then attaches.
        if (changes.EpicGiven && (changes.ParentGiven ? parentRow is not null : before.ParentId is not null))
        {
            throw new Refusal(RefusalCode.EpicInherited, "A sub-issue's epic follows its parent.");
        }

        // A new parent is locked with the issue, the lower id first, so that
        // two writes that would each add a level — X under P, P under Q —
        // meet on P's row and the second sees the first.
        Issue? parent = null;
        if (parentRow is not null && parentRow.Id.CompareTo(before.Id) < 0)
        {
            parent = await issues.LoadForWriteAsync(parentRow.Id, cancellationToken)
                ?? throw Refusal.Validation("parent", $"No issue {parentRow.Key}.");
        }

        var issue = await issues.LoadForWriteAsync(before.Id, cancellationToken)
                ?? throw new Refusal(RefusalCode.NotFound, $"No issue {key}.");

        if (parentRow is not null && parent is null && parentRow.Id != before.Id)
        {
            parent = await issues.LoadForWriteAsync(parentRow.Id, cancellationToken)
                ?? throw Refusal.Validation("parent", $"No issue {parentRow.Key}.");
        }

        if (expected is { } version && issue.UpdatedAt != version)
        {
            throw new Refusal(
                RefusalCode.Stale,
                $"{before.Key} changed at {issue.UpdatedAt:yyyy-MM-dd'T'HH:mm:ss.ffffff'Z'}; you last read it at {version:yyyy-MM-dd'T'HH:mm:ss.ffffff'Z'}.",
                new Dictionary<string, object?> { ["current"] = await assembler.CompleteAsync(before, cancellationToken) });
        }

        var now = clock.GetUtcNow();

        if (changes.ParentGiven && parentRow?.Id != issue.ParentId)
        {
            if (issue.Closed)
            {
                throw new Refusal(RefusalCode.Transition, "A closed issue cannot change parent; reopen it first.");
            }
            if (parent is not null)
            {
                if (parent.ProjectId != issue.ProjectId)
                {
                    throw new Refusal(RefusalCode.OtherProject, "A parent and its sub-issue stay in one project.");
                }
                if (parent.ParentId is not null || await issues.HasSubIssuesAsync(issue.Id, cancellationToken))
                {
                    throw new Refusal(RefusalCode.OneLevel, "Sub-issues are exactly one level deep.");
                }
            }

            var oldParent = issue.ParentId is { } oldParentId
                ? (await issues.FindLiveManyAsync([oldParentId], cancellationToken)).SingleOrDefault()?.Key
                : null;
            issue.AttachToParent(parentRow?.Id, now);
            history.Add(HistoryEntry.OnIssue(issue.Id, caller.Id, now, HistoryField.Parent, oldParent, parentRow?.Key));

            // The sub-issue takes its parent's epic, and that is a move under
            // an epic like any other: written down, and a closed epic reopens.
            if (parent is not null && parent.EpicId != issue.EpicId)
            {
                var oldEpic = issue.EpicId is { } oldEpicId ? await epics.FindAsync(oldEpicId, cancellationToken) : null;
                var newEpic = parent.EpicId is { } newEpicId ? await epics.FindAsync(newEpicId, cancellationToken) : null;
                issue.AttachTo(parent.EpicId, now);
                history.Add(HistoryEntry.OnIssue(
                    issue.Id, caller.Id, now, HistoryField.Epic,
                    oldEpic is null ? null : EpicKey.Of(project.Key, oldEpic.Number),
                    newEpic is null ? null : EpicKey.Of(project.Key, newEpic.Number)));

                if (newEpic is { Closed: true })
                {
                    newEpic.Reopen(now);
                    history.Add(HistoryEntry.OnEpic(newEpic.Id, caller.Id, now, HistoryField.Status, "closed", "open", "reopened by attaching an issue"));
                }
            }
        }

        if (parking is { } target)
        {
            // Decided on the row under the lock, where a claim that lapsed is
            // no claim and its in_progress reads todo, as every reader saw it.
            var from = issue.StatusAt(now);
            try
            {
                issue.MoveTo(target, caller.Id, caller.Kind, now);
            }
            catch (Refusal refusal) when (ClaimGate.Names(refusal))
            {
                throw await ClaimGate.ExplainAsync(refusal, before.Key, issue.Id, caller, history, identities, cancellationToken);
            }

            history.Add(HistoryEntry.OnIssue(issue.Id, caller.Id, now, HistoryField.Status, from.Name(), target.Name()));
        }

        if (changes.Title is not null && changes.Title != issue.Title)
        {
            var old = issue.Title;
            Validated.Field("title", () => { issue.Retitle(changes.Title, now); return true; });
            history.Add(HistoryEntry.OnIssue(issue.Id, caller.Id, now, HistoryField.Title, old, issue.Title));
        }

        if (changes.DescriptionGiven && (changes.Description ?? string.Empty) != issue.Description)
        {
            issue.Describe(changes.Description, now);
            history.Add(HistoryEntry.OnIssue(issue.Id, caller.Id, now, HistoryField.Description));
        }

        if (changes.ResultGiven && changes.Result != issue.Result)
        {
            issue.RecordResult(changes.Result, now);
            history.Add(HistoryEntry.OnIssue(issue.Id, caller.Id, now, HistoryField.Result));
        }

        if (changes.Priority is { } priority && priority != issue.Priority)
        {
            var old = issue.Priority;
            Validated.Field("priority", () => { issue.Prioritize(priority, now); return true; });
            history.Add(HistoryEntry.OnIssue(issue.Id, caller.Id, now, HistoryField.Priority, ((int)old).ToString(CultureInfo.InvariantCulture), ((int)priority).ToString(CultureInfo.InvariantCulture)));
        }

        if (changes.Ready is { } flag && flag != issue.Ready)
        {
            issue.SetReady(flag, now);
            history.Add(HistoryEntry.OnIssue(issue.Id, caller.Id, now, HistoryField.Ready, (!flag).ToString().ToLowerInvariant(), flag.ToString().ToLowerInvariant()));
        }

        if (changes.AssigneeGiven && assignee?.Id != issue.AssigneeId)
        {
            var old = issue.AssigneeId;
            issue.Assign(assignee?.Id, now);
            history.Add(HistoryEntry.OnIssue(issue.Id, caller.Id, now, HistoryField.Assignee, old?.ToString(), assignee?.Id.ToString()));
        }

        if (changes.EpicGiven && epic?.Id != issue.EpicId)
        {
            var old = issue.EpicId is { } oldId ? await epics.FindAsync(oldId, cancellationToken) : null;
            issue.AttachTo(epic?.Id, now);
            history.Add(HistoryEntry.OnIssue(
                issue.Id, caller.Id, now, HistoryField.Epic,
                old is null ? null : EpicKey.Of(project.Key, old.Number),
                epic is null ? null : EpicKey.Of(project.Key, epic.Number)));

            if (epic is { Closed: true })
            {
                epic.Reopen(now);
                history.Add(HistoryEntry.OnEpic(epic.Id, caller.Id, now, HistoryField.Status, "closed", "open", "reopened by attaching an issue"));
            }

            foreach (var childRow in await issues.SubIssuesOfAsync(issue.Id, cancellationToken))
            {
                var child = await issues.LoadForWriteAsync(childRow.Id, cancellationToken)
                    ?? throw new InvalidOperationException($"Sub-issue {childRow.Key} vanished under its parent's write.");
                child.AttachTo(epic?.Id, now);
                history.Add(HistoryEntry.OnIssue(child.Id, caller.Id, now, HistoryField.Epic,
                    old is null ? null : EpicKey.Of(project.Key, old.Number),
                    epic is null ? null : EpicKey.Of(project.Key, epic.Number)));
            }
        }

        if (newLabels is not null)
        {
            var current = (await issues.LabelsOfAsync([issue.Id], cancellationToken)).Select(l => l.Label).ToList();
            foreach (var gone in current.Where(c => newLabels.All(n => n.Id != c.Id)))
            {
                await issues.DetachAsync(issue.Id, gone.Id, cancellationToken);
                history.Add(HistoryEntry.OnIssue(issue.Id, caller.Id, now, HistoryField.Label, oldValue: gone.Name));
            }

            foreach (var added in newLabels.Where(n => current.All(c => c.Id != n.Id)))
            {
                issues.Attach(IssueLabel.Attach(issue.Id, added.Id));
                history.Add(HistoryEntry.OnIssue(issue.Id, caller.Id, now, HistoryField.Label, newValue: added.Name));
            }

            if (current.Count != newLabels.Count || current.Any(c => newLabels.All(n => n.Id != c.Id)))
            {
                issue.Touch(now);
            }
        }

        // The holder's write is the sign of life (VISION 11); anybody else's
        // moves updated_at and nothing about the claim.
        issue.ExtendClaimIfHeldBy(caller.Id, caller.Kind, now, settings.ClaimExpiry);

        await issues.SaveAsync(cancellationToken);

        var after = await issues.FindLiveAsync(before.ProjectKey, before.Number, cancellationToken)
            ?? throw new InvalidOperationException($"Issue {before.Key} vanished under its own write.");

        return after;
    }

    /// <summary>The `If-Match` value — the `updated_at` as the client last read it, quoted or not.</summary>
    /// <exception cref="Refusal"><c>validation</c> when it is not a timestamp.</exception>
    public static DateTimeOffset? Expected(string? ifMatch)
    {
        if (string.IsNullOrWhiteSpace(ifMatch))
        {
            return null;
        }

        var text = ifMatch.Trim().Trim('"');
        return DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed)
            ? parsed
            : throw Refusal.Validation("If-Match", "If-Match carries the updated_at as it was read, quoted.");
    }

    private async Task<Domain.Epics.Epic> EpicAsync(Project project, string key, CancellationToken cancellationToken)
    {
        if (!EpicKey.TryParse(key, out var projectKey, out var number) || projectKey != project.Key)
        {
            throw Refusal.Validation("epic", $"{key} is not an epic key of {project.Key}; an epic and its issues stay in one project.");
        }

        return await epics.FindLiveAsync(project.Id, number, cancellationToken)
            ?? throw Refusal.Validation("epic", $"No epic {key}.");
    }

    private async Task<IssueRow> ParentAsync(string key, CancellationToken cancellationToken)
    {
        if (!IssueKey.TryParse(key, out var projectKey, out var number))
        {
            throw Refusal.Validation("parent", $"{key} is not an issue key.");
        }
        var parent = await issues.FindLiveAsync(projectKey, number, cancellationToken)
            ?? throw Refusal.Validation("parent", $"No issue {key}.");
        await scope.RequireAsync(parent.ProjectId, cancellationToken);
        return parent;
    }
}
