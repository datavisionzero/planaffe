using Planaffe.Application.Acts;

namespace Planaffe.Api.Http;

public sealed record ChangeReleaseRequest(string? Description);

public static class ReleaseEndpoints
{
    public static IEndpointRouteBuilder MapReleases(this IEndpointRouteBuilder endpoints)
    {
        var door = endpoints.MapGroup("/projects/{key}/releases").RequireAuthorization().ProducesProblem(StatusCodes.Status401Unauthorized);
        door.MapGet(string.Empty, (string key, ListReleases list, CancellationToken ct) => list.ExecuteAsync(key, ct))
            .WithName("ListReleases").WithSummary("Every release of the project: the open one first, then published ones newest first.");
        door.MapGet("/{name}", (string key, string name, ReadRelease read, CancellationToken ct) => read.ExecuteAsync(key, name, ct))
            .WithName("ReadRelease").WithSummary("One release with the issues that shipped in it.").ProducesProblem(StatusCodes.Status404NotFound);
        door.MapPatch("/{name}", async (string key, string name, ChangeReleaseRequest? request, ChangeRelease change, CancellationToken ct) =>
            await change.ExecuteAsync(key, name, request?.Description, ct))
            .WithName("ChangeRelease").WithSummary("Annotate the release notes, whether open or published.").Accepts<ChangeReleaseRequest>("application/json").ProducesProblem(StatusCodes.Status404NotFound);
        door.MapPost("/publish", async (string key, PublishReleaseRequest? request, PublishRelease publish, CancellationToken ct) =>
            {
                var release = await publish.ExecuteAsync(key, request ?? new(null, null), ct);
                return Results.Created($"/projects/{key}/releases/{Uri.EscapeDataString(release.Name)}", release);
            })
            .WithName("PublishRelease").WithSummary("Name and freeze the open release, then create the next open one.")
            .Produces<ReleaseShape>(StatusCodes.Status201Created).ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status409Conflict);
        return endpoints;
    }
}
