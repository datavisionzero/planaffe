using Planaffe.Application.Ports;
using Planaffe.Domain;
using Planaffe.Domain.History;
using Planaffe.Domain.Issues;

namespace Planaffe.Application.Acts;

public sealed record CloseRequest(IssueStatus? Status, string? Result);

public sealed record ReviewRequest(string? Result);

public sealed record ReopenRequest(string? Comment);

/// <summary>
/// What an agent is told when the Domain refuses it an act on an issue somebody
/// else holds (<c>docs/api.md</c>, The acts on an issue): <c>claim-lost</c> when
/// the newest claim entry names it as the one displaced, and <c>claim-held</c>
/// otherwise, with the holder rendered. The rule itself is the Domain's and
/// runs under the row's lock; this is only the telling.
/// </summary>
public static class ClaimGate
{
    /// <summary>Whether a refusal is the Domain's <c>claim-held</c> naming the holder.</summary>
    public static bool Names(Refusal refusal) =>
        refusal.Code is RefusalCode.ClaimHeld && refusal.Extensions.TryGetValue("holder", out var holder) && holder is Guid;

    public static async Task<Refusal> ExplainAsync(
        Refusal refusal, string key, Guid issueId, Caller caller, IHistory history, IIdentities identities, CancellationToken cancellationToken)
    {
        var holder = (Guid)refusal.Extensions["holder"]!;
        var last = await history.LastAsync(issueId, HistoryField.Claim, cancellationToken);
        var lost = last is { OldValue: { } previous } && previous == caller.Id.ToString();
        var holderRef = await identities.FindAsync(holder, cancellationToken);

        return new Refusal(
            lost ? RefusalCode.ClaimLost : RefusalCode.ClaimHeld,
            lost
                ? $"Your claim on {key} lapsed and {holderRef?.Name ?? "somebody else"} holds it now."
                : $"{key} is held by {holderRef?.Name ?? "somebody else"}.",
            new Dictionary<string, object?> { ["holder"] = holderRef is null ? null : IdentityRef.Of(holderRef) });
    }
}

/// <summary>
/// The moves of the transition table other than claim and release: close,
/// review, reopen — acts in Domain (ADR 0016), one transaction each here, with
/// the history the table implies.
/// </summary>
public sealed class MoveIssue(
    ICallerIdentity callerIdentity,
    IProjects projects,
    IIdentities identities,
    IIssues issues,
    ProjectScope scope,
    IReleases releases,
    IHistory history,
    ITransactions transactions,
    IssueAssembler assembler,
    InstanceSettings settings,
    TimeProvider clock)
{
    public Task<IssueShape> CloseAsync(string key, CloseRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var target = request.Status ?? throw Refusal.Validation("status", "A close is done or canceled.");

        return OnAsync(key, async (issue, row, caller, now) =>
        {
            var project = await projects.FindByIdAsync(row.ProjectId, cancellationToken)
                ?? throw new InvalidOperationException($"Issue {row.Key} has no project row.");

            var hadResult = issue.Result;
            var from = issue.StatusAt(now);
            var holder = issue.ClaimAt(now)?.HolderId;
            var landed = issue.Close(target, request.Result, caller.Id, caller.Kind, project.ReviewRequired, now);

            if (landed is IssueStatus.Done)
            {
                await releases.AddDoneAsync(issue, cancellationToken);
            }

            History(issue, from, landed, holder, hadResult, caller, now);
        }, cancellationToken);
    }

    public Task<IssueShape> ReviewAsync(string key, ReviewRequest request, CancellationToken cancellationToken) =>
        OnAsync(key, (issue, row, caller, now) =>
        {
            var hadResult = issue.Result;
            var from = issue.StatusAt(now);
            var holder = issue.ClaimAt(now)?.HolderId;
            issue.HandIn(request?.Result, caller.Id, caller.Kind, now);

            History(issue, from, IssueStatus.Review, holder, hadResult, caller, now);
            return Task.CompletedTask;
        }, cancellationToken);

    /// <remarks>
    /// The comment is written first, and expected on the way back from
    /// <c>review</c> — pointed out by the CLI when missing, never refused.
    /// </remarks>
    public Task<IssueShape> ReopenAsync(string key, ReopenRequest request, CancellationToken cancellationToken) =>
        OnAsync(key, async (issue, row, caller, now) =>
        {
            var from = issue.Status;
            issue.Reopen(now);
            await releases.RemoveFromOpenAsync(issue.Id, cancellationToken);

            if (!string.IsNullOrWhiteSpace(request?.Comment))
            {
                issues.Add(Comment.Write(issue.Id, caller.Id, request.Comment, now));
            }

            history.Add(HistoryEntry.OnIssue(issue.Id, caller.Id, now, HistoryField.Status, from.Name(), IssueStatus.Todo.Name()));
        }, cancellationToken);

    private void History(Issue issue, IssueStatus from, IssueStatus to, Guid? holder, string? hadResult, Caller caller, DateTimeOffset now)
    {
        if (holder is { } released)
        {
            history.Add(HistoryEntry.OnIssue(issue.Id, caller.Id, now, HistoryField.Claim, released.ToString(), null));
        }

        if (issue.Result != hadResult)
        {
            history.Add(HistoryEntry.OnIssue(issue.Id, caller.Id, now, HistoryField.Result));
        }

        history.Add(HistoryEntry.OnIssue(issue.Id, caller.Id, now, HistoryField.Status, from.Name(), to.Name()));
    }

    /// <remarks>
    /// Every decision is taken on the row as it stands under the lock; the
    /// view row read before it only finds the issue and its project.
    /// </remarks>
    private async Task<IssueShape> OnAsync(
        string key,
        Func<Issue, IssueRow, Caller, DateTimeOffset, Task> move,
        CancellationToken cancellationToken)
    {
        var caller = callerIdentity.Caller;
        var row = await issues.LiveAsync(key, settings, cancellationToken);
        await scope.RequireAsync(row.ProjectId, cancellationToken);

        try
        {
            await transactions.RunAsync(async () =>
            {
                var issue = await issues.LoadForWriteAsync(row.Id, cancellationToken)
                    ?? throw new Refusal(RefusalCode.NotFound, $"No issue {key}.");

                await move(issue, row, caller, clock.GetUtcNow());
                await issues.SaveAsync(cancellationToken);
                return true;
            }, cancellationToken);
        }
        catch (Refusal refusal) when (ClaimGate.Names(refusal))
        {
            throw await ClaimGate.ExplainAsync(refusal, row.Key, row.Id, caller, history, identities, cancellationToken);
        }

        var after = await issues.FindLiveAsync(row.ProjectKey, row.Number, cancellationToken)
            ?? throw new InvalidOperationException($"Issue {row.Key} vanished under its own move.");

        return await assembler.CompleteAsync(after, cancellationToken);
    }
}
