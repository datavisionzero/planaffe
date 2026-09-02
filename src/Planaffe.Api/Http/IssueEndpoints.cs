using System.Text.Json;
using Planaffe.Application.Acts;
using Planaffe.Domain.Issues;

namespace Planaffe.Api.Http;

/// <summary>
/// The body of <c>PATCH /issues/{key}</c> as it is on the wire: only what is
/// present changes, and a field present as <c>null</c> clears it. This type is
/// the contract's; the act takes <see cref="IssueChanges"/>, which tells the two
/// apart.
/// </summary>
public sealed record ChangeIssueRequest(
    string? Title,
    string? Description,
    string? Result,
    int? Priority,
    bool? Ready,
    string? Assignee,
    string? Epic,
    IReadOnlyList<string>? Labels,
    string? Status);

/// <summary>
/// Issues without their acts (<c>docs/api.md</c>): the bulk create, the list,
/// the complete read, the guarded <c>PATCH</c>, and the label and blocker edges.
/// The acts on an issue — claim, release, close, review, reopen — arrive with
/// their tickets.
/// </summary>
public static class IssueEndpoints
{
    public static IEndpointRouteBuilder MapIssues(this IEndpointRouteBuilder endpoints)
    {
        var door = endpoints.MapGroup("/issues")
            .RequireAuthorization()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        door.MapPost(string.Empty, async (CreateIssuesRequest? request, CreateIssues create, CancellationToken cancellationToken) =>
            {
                var created = await create.ExecuteAsync(request ?? new CreateIssuesRequest(null, null), cancellationToken);
                return Results.Created("/issues", created);
            })
            .WithName("CreateIssues")
            .WithSummary("Create one or several wired-up issues in one transaction: all of them or none.")
            .Produces<CreatedIssues>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        door.MapGet(string.Empty, (
                HttpRequest http,
                string? project,
                bool? ready,
                int? priority_min,
                int? priority_max,
                string? epic,
                string? assignee,
                string? claimed,
                string? author,
                bool? blocked,
                bool? has_open_question,
                bool? deleted,
                string? sort,
                string? order,
                string? cursor,
                int? limit,
                ListIssues list,
                CancellationToken cancellationToken) =>
                list.ExecuteAsync(
                    new IssueListRequest(
                        project,
                        [.. http.Query["status"].OfType<string>()],
                        ready,
                        priority_min,
                        priority_max,
                        [.. http.Query["label"].OfType<string>()],
                        epic, assignee, claimed, author, blocked, has_open_question, deleted, sort, order, cursor, limit),
                    cancellationToken))
            .WithName("ListIssues")
            .WithSummary("A page of slim issues, filtered and sorted; `status` and `label` repeat.")
            .ProducesProblem(StatusCodes.Status400BadRequest);

        door.MapGet("/{key}", (string key, ReadIssue read, CancellationToken cancellationToken) => read.ExecuteAsync(key, cancellationToken))
            .WithName("ReadIssue")
            .WithSummary("The complete issue: the context package.")
            .ProducesProblem(StatusCodes.Status404NotFound);

        door.MapPatch("/{key}", async (string key, HttpRequest http, ChangeIssue change, CancellationToken cancellationToken) =>
            {
                var body = await JsonDocument.ParseAsync(http.Body, cancellationToken: cancellationToken);
                return await change.ExecuteAsync(key, Changes(body.RootElement), http.Headers.IfMatch.ToString(), cancellationToken);
            })
            .WithName("ChangeIssue")
            .WithSummary("Change the fields present; `null` clears. `If-Match` with the `updated_at` last read guards the write.")
            .Accepts<ChangeIssueRequest>("application/json")
            .Produces<IssueShape>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        door.MapDelete("/{key}", async (string key, DeleteIssue delete, CancellationToken cancellationToken) =>
            {
                await delete.ExecuteAsync(key, cancellationToken);
                return Results.NoContent();
            })
            .WithName("DeleteIssue")
            .WithSummary("Soft-delete: invisible everywhere, restorable for the grace period; the claim is let go.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        door.MapPost("/{key}/restore", (string key, RestoreIssue restore, CancellationToken cancellationToken) =>
                restore.ExecuteAsync(key, cancellationToken))
            .WithName("RestoreIssue")
            .WithSummary("Back into whatever state it was in, without its claim.")
            .Produces<IssueShape>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        // The acts on an issue (ADR 0016): each returns the complete issue.
        door.MapPost("/{key}/claim", (string key, ClaimRequest? request, ClaimIssue claim, CancellationToken cancellationToken) =>
                claim.ExecuteAsync(key, request?.Force ?? false, cancellationToken))
            .WithName("ClaimIssue")
            .WithSummary("Claim: taken when unclaimed or expired, extended when held by the caller, `claim-held` otherwise unless `force`.")
            .Produces<IssueShape>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        door.MapPost("/{key}/release", (string key, ReleaseIssue release, CancellationToken cancellationToken) =>
                release.ExecuteAsync(key, cancellationToken))
            .WithName("ReleaseIssue")
            .WithSummary("Let go: the claim is cleared and the status is todo. The holder, or a user.")
            .Produces<IssueShape>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        door.MapPost("/{key}/close", (string key, CloseRequest? request, MoveIssue move, CancellationToken cancellationToken) =>
                move.CloseAsync(key, request ?? new CloseRequest(null, null), cancellationToken))
            .WithName("CloseIssue")
            .WithSummary("Close as done or canceled. An agent's close lands in review where review is required; a user's lands where it says.")
            .Produces<IssueShape>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        door.MapPost("/{key}/review", (string key, ReviewRequest? request, MoveIssue move, CancellationToken cancellationToken) =>
                move.ReviewAsync(key, request ?? new ReviewRequest(null), cancellationToken))
            .WithName("ReviewIssue")
            .WithSummary("Hand in explicitly, whatever the switch says. Clears the claim; no closed_at.")
            .Produces<IssueShape>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        door.MapPost("/{key}/reopen", (string key, ReopenRequest? request, MoveIssue move, CancellationToken cancellationToken) =>
                move.ReopenAsync(key, request ?? new ReopenRequest(null), cancellationToken))
            .WithName("ReopenIssue")
            .WithSummary("Back to todo from review, done or canceled; the comment is written first and expected on the way back from review.")
            .Produces<IssueShape>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        door.MapPost("/{key}/labels/{name}", (string key, string name, IssueEdges edges, CancellationToken cancellationToken) =>
                edges.AddLabelAsync(key, Uri.UnescapeDataString(name), cancellationToken))
            .WithName("AddIssueLabel")
            .WithSummary("Add one label, replacing another of its group.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        door.MapDelete("/{key}/labels/{name}", (string key, string name, IssueEdges edges, CancellationToken cancellationToken) =>
                edges.RemoveLabelAsync(key, Uri.UnescapeDataString(name), cancellationToken))
            .WithName("RemoveIssueLabel")
            .WithSummary("Remove one label.")
            .ProducesProblem(StatusCodes.Status404NotFound);

        door.MapPost("/{key}/blocked-by/{blockerKey}", (string key, string blockerKey, IssueEdges edges, CancellationToken cancellationToken) =>
                edges.AddBlockerAsync(key, blockerKey, cancellationToken))
            .WithName("AddBlocker")
            .WithSummary("Add a blocker, across projects if need be; `cycle` when it would close one.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        door.MapDelete("/{key}/blocked-by/{blockerKey}", (string key, string blockerKey, IssueEdges edges, CancellationToken cancellationToken) =>
                edges.RemoveBlockerAsync(key, blockerKey, cancellationToken))
            .WithName("RemoveBlocker")
            .WithSummary("Remove a blocker.")
            .ProducesProblem(StatusCodes.Status404NotFound);

        return endpoints;
    }

    // Present, present-as-null and absent are three things in a PATCH, and only
    // the raw document tells them apart.
    private static IssueChanges Changes(JsonElement body)
    {
        if (body.ValueKind is not JsonValueKind.Object)
        {
            throw Domain.Refusal.Validation("body", "A change is an object.");
        }

        return new IssueChanges(
            Text(body, "title"),
            body.TryGetProperty("description", out _),
            Text(body, "description"),
            body.TryGetProperty("result", out _),
            Text(body, "result"),
            body.TryGetProperty("priority", out var priority) && priority.ValueKind is JsonValueKind.Number
                ? (Priority)priority.GetInt32()
                : null,
            body.TryGetProperty("ready", out var ready) && ready.ValueKind is JsonValueKind.True or JsonValueKind.False
                ? ready.GetBoolean()
                : null,
            body.TryGetProperty("assignee", out _),
            Text(body, "assignee"),
            body.TryGetProperty("epic", out _),
            Text(body, "epic"),
            body.TryGetProperty("labels", out var labels) && labels.ValueKind is JsonValueKind.Array
                ? [.. labels.EnumerateArray().Select(l => l.GetString() ?? string.Empty)]
                : null,
            Text(body, "status"));
    }

    private static string? Text(JsonElement body, string property) =>
        body.TryGetProperty(property, out var value) && value.ValueKind is JsonValueKind.String ? value.GetString() : null;
}
