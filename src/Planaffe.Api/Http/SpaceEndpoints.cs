using Planaffe.Application.Acts;

namespace Planaffe.Api.Http;

public sealed record CreateSpaceBody(string? Name, string? Title, bool? ClosedToAgents);

public sealed record ChangeSpaceBody(string? Name, string? Title, bool? ClosedToAgents);

/// <summary>
/// Spaces (<c>docs/api.md</c>): the knowledge base's bracket, at the root of
/// the API rather than under a project, because a space is the second bracket
/// and not something inside the first (ADR 0027).
/// </summary>
/// <remarks>
/// There is no equivalent of <see cref="ProjectScopeMiddleware"/> here and
/// there is nothing missing. Every act on a space asks <c>SpaceScope</c>
/// itself, which is the same guarantee by the other of the two routes
/// <c>docs/codebase.md</c> describes — and it is the only one that can answer
/// for an agent, because the switch that hides a closed space is read there.
/// </remarks>
public static class SpaceEndpoints
{
    public static IEndpointRouteBuilder MapSpaces(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/admin/spaces", (ListAdminSpaces list, CancellationToken cancellationToken) =>
                list.ExecuteAsync(cancellationToken))
            .RequireAuthorization().WithName("ListAdminSpaces")
            .WithSummary("Every space on the instance, deleted ones included — the list a deleted space is found in to be restored. Administrators only, and it shows no content.")
            .Produces<IReadOnlyList<SpaceShape>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        var door = endpoints.MapGroup("/spaces")
            .RequireAuthorization()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        door.MapGet(string.Empty, (ListSpaces list, CancellationToken cancellationToken) => list.ExecuteAsync(cancellationToken))
            .WithName("ListSpaces")
            .WithSummary("Every space the caller sees, by name. An agent's list has every space closed to agents subtracted. Not paginated.")
            .Produces<IReadOnlyList<SpaceShape>>();

        door.MapPost(string.Empty, async (CreateSpaceBody? request, CreateSpace create, CancellationToken cancellationToken) =>
            {
                var space = await create.ExecuteAsync(
                    new CreateSpaceRequest(request?.Name, request?.Title, request?.ClosedToAgents ?? false),
                    cancellationToken);
                return Results.Created($"/spaces/{space.Name}", space);
            })
            .WithName("CreateSpace")
            .WithSummary("Create a space. The name is given, never derived from the title, and the creator is named on it. Users only: an agent creates no bracket (ADR 0015, ADR 0027).")
            .Produces<SpaceShape>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        door.MapGet("/{name}", (string name, ReadSpace read, CancellationToken cancellationToken) =>
                read.ExecuteAsync(name, cancellationToken))
            .WithName("ReadSpace")
            .WithSummary("One space: its name, title, switch and author.")
            .Produces<SpaceShape>();

        door.MapPatch("/{name}", (string name, ChangeSpaceBody? request, ChangeSpace change, CancellationToken cancellationToken) =>
                change.ExecuteAsync(
                    name,
                    new SpaceChanges(request?.Name, request?.Title, request?.ClosedToAgents),
                    cancellationToken))
            .WithName("ChangeSpace")
            .WithSummary("Rename it, retitle it, or close it to agents and open it again. A rename leaves nothing behind at the old name. Users only.")
            .Produces<SpaceShape>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        door.MapDelete("/{name}", async (string name, MoveSpace move, CancellationToken cancellationToken) =>
            {
                await move.DeleteAsync(name, cancellationToken);
                return Results.NoContent();
            })
            .WithName("DeleteSpace")
            .WithSummary("Soft-delete a space; its name stays spent until the purge. Administrators only.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        door.MapPost("/{name}/restore", (string name, MoveSpace move, CancellationToken cancellationToken) =>
                move.RestoreAsync(name, cancellationToken))
            .WithName("RestoreSpace")
            .WithSummary("Bring a deleted space back, under the name it kept. Administrators only.")
            .Produces<SpaceShape>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        door.MapGet("/{name}/users", (string name, ListSpaceUsers list, CancellationToken cancellationToken) =>
                list.ExecuteAsync(name, cancellationToken))
            .WithName("ListSpaceUsers")
            .WithSummary("Who sees this space. A user named on it, or an administrator.")
            .Produces<IReadOnlyList<UserSummary>>()
            .ProducesProblem(StatusCodes.Status403Forbidden);

        door.MapPut("/{name}/users/{id:guid}", async (string name, Guid id, GrantSpaceAccess grant, CancellationToken cancellationToken) =>
            {
                await grant.ExecuteAsync(name, id, cancellationToken);
                return Results.NoContent();
            })
            .WithName("GrantSpaceAccess")
            .WithSummary("Name a user on this space. Administrators only; agents inherit their owner and are never named.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        door.MapDelete("/{name}/users/{id:guid}", async (string name, Guid id, RevokeSpaceAccess revoke, CancellationToken cancellationToken) =>
            {
                await revoke.ExecuteAsync(name, id, cancellationToken);
                return Results.NoContent();
            })
            .WithName("RevokeSpaceAccess")
            .WithSummary("Take a user off this space. Administrators only, and revoking what was never granted goes through.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        return endpoints;
    }
}
