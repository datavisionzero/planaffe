using System.Text.Json;
using Planaffe.Application.Acts;

namespace Planaffe.Api.Http;

/// <summary>The body of <c>PATCH /epics/{key}</c> as it is on the wire; the act takes <see cref="EpicChanges"/>.</summary>
public sealed record ChangeEpicRequest(string? Title, string? Description, IReadOnlyList<string>? Labels);

/// <summary>Epics (<c>docs/api.md</c>): the bracket, its living document, and the four moves that gate nothing.</summary>
public static class EpicEndpoints
{
    public static IEndpointRouteBuilder MapEpics(this IEndpointRouteBuilder endpoints)
    {
        var door = endpoints.MapGroup("/epics")
            .RequireAuthorization()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        door.MapPost(string.Empty, async (CreateEpicRequest? request, CreateEpic create, CancellationToken cancellationToken) =>
            {
                var epic = await create.ExecuteAsync(request ?? new CreateEpicRequest(null, null, null, null), cancellationToken);
                return Results.Created($"/epics/{epic.Key}", epic);
            })
            .WithName("CreateEpic")
            .WithSummary("Create an epic: a theme several issues will hang under, with the plan as its description.")
            .Produces<EpicShape>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        door.MapGet(string.Empty, (HttpRequest http, string? project, string? status, string? cursor, int? limit, ListEpics list, CancellationToken cancellationToken) =>
                list.ExecuteAsync(new EpicListRequest(project, status, [.. http.Query["label"].OfType<string>()], cursor, limit), cancellationToken))
            .WithName("ListEpics")
            .WithSummary("A page of slim epics with their progress, newest first; open ones by default.")
            .ProducesProblem(StatusCodes.Status400BadRequest);

        door.MapGet("/{key}", (string key, ReadEpic read, CancellationToken cancellationToken) => read.ExecuteAsync(key, cancellationToken))
            .WithName("ReadEpic")
            .WithSummary("The complete epic: the living document, the author, the labels, the progress.")
            .ProducesProblem(StatusCodes.Status404NotFound);

        door.MapPatch("/{key}", async (string key, HttpRequest http, ChangeEpic change, CancellationToken cancellationToken) =>
            {
                var root = await PatchBody.ReadObjectAsync(http, "A change", cancellationToken);
                var changes = new EpicChanges(
                    PatchBody.Text(root, "title"),
                    PatchBody.Given(root, "description"),
                    PatchBody.Text(root, "description"),
                    PatchBody.Texts(root, "labels"));

                return await change.ExecuteAsync(key, changes, http.Headers.IfMatch.ToString(), cancellationToken);
            })
            .WithName("ChangeEpic")
            .WithSummary("Change the title, the description or the labels; `If-Match` with the `updated_at` last read guards the living document.")
            .Accepts<ChangeEpicRequest>("application/json")
            .Produces<EpicShape>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed);

        door.MapPost("/{key}/close", (string key, MoveEpic move, CancellationToken cancellationToken) => move.CloseAsync(key, cancellationToken))
            .WithName("CloseEpic")
            .WithSummary("Close the epic, whatever is still open; it gates nothing, and the answer carries the progress.")
            .Produces<EpicShape>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        door.MapPost("/{key}/reopen", (string key, MoveEpic move, CancellationToken cancellationToken) => move.ReopenAsync(key, cancellationToken))
            .WithName("ReopenEpic")
            .WithSummary("Back to open.")
            .Produces<EpicShape>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        door.MapDelete("/{key}", async (string key, MoveEpic move, CancellationToken cancellationToken) =>
            {
                await move.DeleteAsync(key, cancellationToken);
                return Results.NoContent();
            })
            .WithName("DeleteEpic")
            .WithSummary("Soft-delete an epic nothing references; `has-issues` while issues do.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        door.MapPost("/{key}/restore", (string key, MoveEpic move, CancellationToken cancellationToken) => move.RestoreAsync(key, cancellationToken))
            .WithName("RestoreEpic")
            .WithSummary("Bring a deleted epic back.")
            .Produces<EpicShape>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        return endpoints;
    }

}
