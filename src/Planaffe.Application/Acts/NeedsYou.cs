using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Planaffe.Application.Ports;
using Planaffe.Domain;

namespace Planaffe.Application.Acts;

/// <summary>One entry in the human's work list.</summary>
public sealed record NeedsYouItem(IssueSummaryShape Issue, NeedsYouBecause Because);

/// <summary>A cursor page of what only a human can resolve (VISION 10).</summary>
public sealed record NeedsYouPage(IReadOnlyList<NeedsYouItem> Items, int Total, bool HasMore, string? NextCursor);

/// <summary>The page and its validator, or an unchanged long-poll answer.</summary>
public sealed record NeedsYouAnswer(NeedsYouPage? Page, string ETag);

/// <summary>The four groups that provide more work to agents, in human-action order.</summary>
public sealed class NeedsYou(
    IProjects projects,
    IIssues issues,
    IssueAssembler assembler,
    InstanceSettings settings,
    IChanges changes)
{
    public async Task<NeedsYouPage> ExecuteAsync(
        string projectKey, string? cursor, int? requestedLimit, CancellationToken cancellationToken)
    {
        var limit = requestedLimit ?? ListIssues.DefaultLimit;
        if (limit < 1 || limit > ListIssues.MaximumLimit)
        {
            throw Refusal.Validation("limit", $"limit is 1 to {ListIssues.MaximumLimit}; larger pages are refused, not truncated (ADR 0012).");
        }

        var project = await projects.LiveForReadAsync(projectKey, settings, cancellationToken);
        var after = cursor is null ? null : NeedsYouCursor.Decode(cursor, project.Id);
        var page = await issues.NeedsYouAsync(project.Id, project.TriageRequired, after, limit, cancellationToken);
        var rows = (await issues.FindLiveManyAsync(page.Items.Select(item => item.Id), cancellationToken)).ToDictionary(row => row.Id);
        var orderedRows = page.Items.Select(item => rows[item.Id]).ToArray();
        var summaries = await assembler.SummariesAsync(orderedRows, cancellationToken);
        var items = page.Items.Zip(summaries, (item, summary) => new NeedsYouItem(summary, item.Because)).ToArray();

        return new NeedsYouPage(
            items,
            page.Total,
            page.HasMore,
            page.HasMore ? NeedsYouCursor.Encode(project.Id, page.Items[^1], orderedRows[^1]) : null);
    }

    public async Task<NeedsYouAnswer> WaitAsync(
        string projectKey,
        string? cursor,
        int? requestedLimit,
        int? wait,
        string? ifNoneMatch,
        CancellationToken cancellationToken)
    {
        Waits.Validate(wait);
        if (wait is null)
        {
            var immediate = await ExecuteAsync(projectKey, cursor, requestedLimit, cancellationToken);
            return new NeedsYouAnswer(immediate, ETag(immediate));
        }

        var project = await projects.LiveForReadAsync(projectKey, settings, cancellationToken);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(wait.Value));
        using var waiting = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        string? baseline = ifNoneMatch;

        while (true)
        {
            try
            {
                await changes.EnsureListeningAsync(project.Id, waiting.Token);
            }
            catch (OperationCanceledException) when (deadline.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                return await AtDeadlineAsync(projectKey, cursor, requestedLimit, baseline, cancellationToken);
            }

            var changed = changes.WaitAsync(project.Id, waiting.Token);
            var page = await ExecuteAsync(projectKey, cursor, requestedLimit, cancellationToken);
            var tag = ETag(page);

            if (baseline is null && page.Items.Count > 0 || baseline is not null && !Matches(baseline, tag))
            {
                await waiting.CancelAsync();
                return new NeedsYouAnswer(page, tag);
            }

            baseline ??= tag;
            try
            {
                await changed;
            }
            catch (OperationCanceledException) when (deadline.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                return await AtDeadlineAsync(projectKey, cursor, requestedLimit, baseline, cancellationToken);
            }
        }
    }

    private async Task<NeedsYouAnswer> AtDeadlineAsync(
        string projectKey, string? cursor, int? limit, string? baseline, CancellationToken cancellationToken)
    {
        var page = await ExecuteAsync(projectKey, cursor, limit, cancellationToken);
        var tag = ETag(page);
        return baseline is not null && Matches(baseline, tag)
            ? new NeedsYouAnswer(null, tag)
            : new NeedsYouAnswer(page, tag);
    }

    private static string ETag(NeedsYouPage page) =>
        $"\"{Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(page)))}\"";

    private static bool Matches(string candidates, string tag) =>
        candidates.Split(',').Select(candidate => candidate.Trim()).Any(candidate => candidate == tag || candidate == "*");
}

/// <summary>An opaque, project-bound keyset cursor for the needs-you order.</summary>
internal static class NeedsYouCursor
{
    private sealed record Payload(string F, NeedsYouBecause B, short P, DateTimeOffset T, int N, Guid I);

    public static string Encode(Guid projectId, NeedsYouRow item, IssueRow issue)
    {
        var payload = new Payload(Fingerprint(projectId), item.Because, (short)issue.Priority, issue.CreatedAt, issue.Number, issue.Id);
        return Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(payload))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    public static NeedsYouPosition Decode(string cursor, Guid projectId)
    {
        Payload? payload;
        try
        {
            var base64 = cursor.Replace('-', '+').Replace('_', '/');
            base64 = base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '=');
            payload = JsonSerializer.Deserialize<Payload>(Convert.FromBase64String(base64));
        }
        catch (Exception exception) when (exception is FormatException or JsonException)
        {
            payload = null;
        }

        if (payload is null || payload.F != Fingerprint(projectId)
            || payload.B is < NeedsYouBecause.Question or > NeedsYouBecause.Stuck
            || payload.P is < 0 or > 4)
        {
            throw new Refusal(RefusalCode.CursorInvalid, "The cursor is not one this server issued for this project's needs-you list.");
        }

        return new NeedsYouPosition(payload.B, (Domain.Issues.Priority)payload.P, payload.T, payload.N, payload.I);
    }

    private static string Fingerprint(Guid projectId) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes($"needs-you:{projectId}")))[..16];
}
