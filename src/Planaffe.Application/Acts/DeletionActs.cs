using Planaffe.Application.Ports;
using Planaffe.Domain;
using Planaffe.Domain.History;
using Planaffe.Domain.Projects;

namespace Planaffe.Application.Acts;

/// <summary>
/// The soft delete of an issue (ADR 0013): anyone who may edit it may delete it,
/// agents included — the grace period is the safety net, not a permission.
/// </summary>
public sealed class DeleteIssue(
    ICallerIdentity callerIdentity,
    IIssues issues,
    IHistory history,
    ITransactions transactions,
    InstanceSettings settings,
    TimeProvider clock)
{
    public async Task ExecuteAsync(string key, CancellationToken cancellationToken)
    {
        var caller = callerIdentity.Caller;
        var row = await issues.LiveAsync(key, settings, cancellationToken);

        if (await issues.HasSubIssuesAsync(row.Id, cancellationToken))
        {
            throw new Refusal(RefusalCode.HasSubIssues, $"{row.Key} has sub-issues; detach or delete them first.");
        }

        await transactions.RunAsync(async () =>
        {
            var issue = await issues.LoadForWriteAsync(row.Id, cancellationToken)
                ?? throw new Refusal(RefusalCode.NotFound, $"No issue {key}.");

            var now = clock.GetUtcNow();
            var holder = issue.Claim?.HolderId;
            var before = issue.Status;
            issue.Delete(caller.Id, now);

            if (holder is { } released)
            {
                history.Add(HistoryEntry.OnIssue(issue.Id, caller.Id, now, HistoryField.Claim, released.ToString(), null));
            }

            if (issue.Status != before)
            {
                history.Add(HistoryEntry.OnIssue(issue.Id, caller.Id, now, HistoryField.Status, ClaimHistory.SnakeCase(before), ClaimHistory.SnakeCase(issue.Status)));
            }

            history.Add(HistoryEntry.OnIssue(issue.Id, caller.Id, now, HistoryField.Deleted, null, "true"));
            await issues.SaveAsync(cancellationToken);
            return true;
        }, cancellationToken);
    }
}

/// <summary>Back into whatever state it was in, without its claim — one command, for the agent that deleted the wrong seven.</summary>
public sealed class RestoreIssue(
    ICallerIdentity callerIdentity,
    IIssues issues,
    IHistory history,
    ITransactions transactions,
    IssueAssembler assembler,
    TimeProvider clock)
{
    public async Task<IssueShape> ExecuteAsync(string key, CancellationToken cancellationToken)
    {
        var caller = callerIdentity.Caller;

        if (!IssueKey.TryParse(key, out var projectKey, out var number))
        {
            throw new Refusal(RefusalCode.NotFound, $"{key} is not an issue key.");
        }

        var deleted = await issues.FindDeletedAsync(projectKey, number, cancellationToken);
        if (deleted is null)
        {
            throw await issues.FindLiveAsync(projectKey, number, cancellationToken) is not null
                ? new Refusal(RefusalCode.Transition, $"Issue {IssueKey.Of(projectKey, number)} is not deleted.")
                : new Refusal(RefusalCode.NotFound, $"No issue {IssueKey.Of(projectKey, number)}.");
        }

        await transactions.RunAsync(async () =>
        {
            var issue = await issues.LoadDeletedForWriteAsync(deleted.Id, cancellationToken)
                ?? throw new Refusal(RefusalCode.NotFound, $"No deleted issue {key}.");

            var now = clock.GetUtcNow();
            issue.Restore(now);
            history.Add(HistoryEntry.OnIssue(issue.Id, caller.Id, now, HistoryField.Deleted, "true", null));
            await issues.SaveAsync(cancellationToken);
            return true;
        }, cancellationToken);

        var row = await issues.FindLiveAsync(projectKey, number, cancellationToken)
            ?? throw new InvalidOperationException($"Issue {key} did not come back.");

        return await assembler.CompleteAsync(row, cancellationToken);
    }
}
