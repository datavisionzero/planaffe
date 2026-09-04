using Planaffe.Application.Acts;

namespace Planaffe.Api.Http;

/// <param name="Key">Upper case, a letter first, two to ten characters; never changed afterwards.</param>
public sealed record CreateProjectRequest(string? Key, string? Name, bool? TriageRequired, bool? ReviewRequired);

/// <summary>Only what is present changes; the key is not among them.</summary>
public sealed record ChangeProjectRequest(string? Name, bool? TriageRequired, bool? ReviewRequired);

/// <summary>Projects (<c>docs/api.md</c>): read by anyone, changed by a user, deleted by an administrator.</summary>
public static class ProjectEndpoints
{
    public static IEndpointRouteBuilder MapProjects(this IEndpointRouteBuilder endpoints)
    {
        var door = endpoints.MapGroup("/projects")
            .RequireAuthorization()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        door.MapPost(string.Empty, async (CreateProjectRequest? request, CreateProject create, CancellationToken cancellationToken) =>
            {
                var project = await create.ExecuteAsync(
                    request?.Key, request?.Name, request?.TriageRequired ?? false, request?.ReviewRequired ?? false, cancellationToken);
                return Results.Created($"/projects/{project.Key}", project);
            })
            .WithName("CreateProject")
            .WithSummary("Create a project with its key and the `kind` label group. Users only.")
            .Produces<ProjectShape>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        door.MapGet(string.Empty, (ListProjects list, CancellationToken cancellationToken) => list.ExecuteAsync(cancellationToken))
            .WithName("ListProjects")
            .WithSummary("Every project the caller sees. Not paginated.");

        door.MapGet("/{key}", (string key, ReadProject read, CancellationToken cancellationToken) => read.ExecuteAsync(key, cancellationToken))
            .WithName("ReadProject")
            .WithSummary("One project by key.")
            .ProducesProblem(StatusCodes.Status404NotFound);

        door.MapPatch("/{key}", (string key, ChangeProjectRequest? request, ChangeProject change, CancellationToken cancellationToken) =>
                change.ExecuteAsync(key, new ProjectChanges(request?.Name, request?.TriageRequired, request?.ReviewRequired), cancellationToken))
            .WithName("ChangeProject")
            .WithSummary("Change the name or the switches. Users only; the key is immutable.")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        door.MapDelete("/{key}", async (string key, DeleteProject delete, CancellationToken cancellationToken) =>
            {
                await delete.ExecuteAsync(key, cancellationToken);
                return Results.NoContent();
            })
            .WithName("DeleteProject")
            .WithSummary("Soft-delete the project with everything in it. Administrators only.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        door.MapPost("/{key}/restore", (string key, RestoreProject restore, CancellationToken cancellationToken) =>
                restore.ExecuteAsync(key, cancellationToken))
            .WithName("RestoreProject")
            .WithSummary("Bring a deleted project back, with everything in it. Administrators only.")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        // The question at the centre of the product (VISION 10): the list, and the act.
        door.MapGet("/{key}/next", (string key, HttpRequest http, bool? ready, string? epic, string? repo, int? limit, Next next, CancellationToken cancellationToken) =>
                next.PreviewAsync(key, new NextRequest(ready, epic, [.. http.Query["label"].OfType<string>()], repo, limit), cancellationToken))
            .WithName("PreviewNext")
            .WithSummary("What the caller would be handed, in that order — the ready-for-agents list — and why the rest is not on it.")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        door.MapPost("/{key}/next", (string key, NextRequest? request, Next next, CancellationToken cancellationToken) =>
                next.TakeAsync(key, request ?? new NextRequest(null, null, null, null, null), cancellationToken))
            .WithName("TakeNext")
            .WithSummary("Take the highest-ranked workable issue and claim it for the caller, in one transaction. 200 with `issue: null` when nothing is workable.")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        door.MapGet("/{key}/needs-you", (string key, string? cursor, int? limit, NeedsYou needsYou, CancellationToken cancellationToken) =>
                needsYou.ExecuteAsync(key, cursor, limit, cancellationToken))
            .WithName("ListNeedsYou")
            .WithSummary("What only a human can resolve: questions, review, unready under triage, then stuck blocker chains.")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return endpoints;
    }
}
