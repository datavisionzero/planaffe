using System.Text.Json;
using System.Text.Json.Serialization;
using Planaffe.Application.Acts;

namespace Planaffe.Api.Http;

public sealed record CreateSpacePageBody(string? Parent, string? Slug, string? Title, string? Body);

/// <summary>
/// A <c>PATCH</c> body where <c>null</c> and absent mean different things:
/// <c>"body": null</c> empties the document, an absent body leaves it. The
/// converter below is what tells the two apart.
/// </summary>
[JsonConverter(typeof(ChangeSpacePageRequestConverter))]
public sealed record ChangeSpacePageRequest(string? Slug, string? Title, bool BodyGiven, string? Body);

/// <param name="Path">The page, as the address inside its space.</param>
/// <param name="Space">The space it lands in, or absent to stay in this one.</param>
/// <param name="Parent">The page it lands under, or absent for the root of that space.</param>
public sealed record MoveSpacePageBody(string? Path, string? Space, string? Parent);

/// <param name="Path">The deleted page, as the address inside its space.</param>
public sealed record RestoreSpacePageBody(string? Path);

/// <summary>How many pages a deletion took, the page itself included.</summary>
public sealed record DeletedPages(int Deleted);

/// <inheritdoc cref="ChangeSpacePageRequest"/>
public sealed class ChangeSpacePageRequestConverter : JsonConverter<ChangeSpacePageRequest>
{
    public override ChangeSpacePageRequest Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var body = JsonElement.ParseValue(ref reader);
        if (body.ValueKind is not JsonValueKind.Object)
        {
            throw new JsonException("A page change is an object.");
        }

        return new ChangeSpacePageRequest(
            Text(body, "slug"),
            Text(body, "title"),
            body.TryGetProperty("body", out _),
            Text(body, "body"));
    }

    public override void Write(Utf8JsonWriter writer, ChangeSpacePageRequest value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(value);

        writer.WriteStartObject();
        if (value.Slug is not null)
        {
            writer.WriteString("slug", value.Slug);
        }

        if (value.Title is not null)
        {
            writer.WriteString("title", value.Title);
        }

        if (value.BodyGiven)
        {
            writer.WriteString("body", value.Body);
        }

        writer.WriteEndObject();
    }

    private static string? Text(JsonElement body, string property) =>
        body.TryGetProperty(property, out var value) && value.ValueKind is JsonValueKind.String ? value.GetString() : null;
}

/// <summary>
/// The pages of a space (<c>docs/api.md</c>, Space pages): the knowledge base's
/// tree, under the space the way the project's pages are under the project.
/// </summary>
/// <remarks>
/// <para>
/// The address carries the tree, so the path parameter carries slashes and has
/// to be the last thing in the route (ADR 0028). That is why moving and
/// restoring are <c>POST</c> on the collection with the address in the body
/// rather than <c>.../{path}/move</c>: a catch-all cannot be followed by a
/// literal segment, and inventing a second spelling of the address for two
/// routes would be worse than this.
/// </para>
/// <para>
/// There is no door in front of these routes and none is missing, for the
/// reason <see cref="SpaceEndpoints"/> gives: every act asks
/// <c>SpaceScope</c> itself, which is the only place that can subtract a space
/// closed to agents.
/// </para>
/// </remarks>
public static class SpacePageEndpoints
{
    public static IEndpointRouteBuilder MapSpacePages(this IEndpointRouteBuilder endpoints)
    {
        var door = endpoints.MapGroup("/spaces/{name}/pages")
            .RequireAuthorization()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        door.MapGet(string.Empty, (string name, ListSpacePages list, CancellationToken cancellationToken) =>
                list.ExecuteAsync(name, cancellationToken))
            .WithName("ListSpacePages")
            .WithSummary("The whole tree as slim SpacePageSummary, without the bodies: a page, then everything under it, siblings by title. Not paginated.")
            .Produces<IReadOnlyList<SpacePageSummaryShape>>();

        door.MapPost(string.Empty, async (string name, CreateSpacePageBody? request, CreateSpacePage create, CancellationToken cancellationToken) =>
            {
                var page = await create.ExecuteAsync(
                    name,
                    new CreateSpacePageRequest(request?.Parent, request?.Slug, request?.Title, request?.Body),
                    cancellationToken);
                return Results.Created($"/spaces/{name}/pages/{page.Path}", page);
            })
            .WithName("CreateSpacePage")
            .WithSummary("Create a page under `parent`, or directly under the space where it is absent. The slug is given, never derived from the title.")
            .Produces<SpacePageShape>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        door.MapPost("/move", (string name, MoveSpacePageBody? request, MoveSpacePage move, CancellationToken cancellationToken) =>
                move.ExecuteAsync(
                    name,
                    request?.Path ?? string.Empty,
                    new SpacePageMove(request?.Space, request?.Parent),
                    cancellationToken))
            .WithName("MoveSpacePage")
            .WithSummary("Move a page under another parent, in this space or another one, with everything below it. `cycle` under itself, `too-deep` where the subtree would pass the third level.")
            .Produces<SpacePageShape>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        door.MapPost("/restore", (string name, RestoreSpacePageBody? request, RestoreSpacePage restore, CancellationToken cancellationToken) =>
                restore.ExecuteAsync(name, request?.Path ?? string.Empty, cancellationToken))
            .WithName("RestoreSpacePage")
            .WithSummary("Bring a deleted page back with exactly what went along. A page whose parent is still deleted is `transition`.")
            .Produces<SpacePageShape>()
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        door.MapGet("/{*path}", (string name, string path, ReadSpacePage read, CancellationToken cancellationToken) =>
                read.ExecuteAsync(name, path, cancellationToken))
            .WithName("ReadSpacePage")
            .WithSummary("The complete page: the Markdown, its address, its parent's, the author and who touched it last. `path` is the slugs from the space down, separated by slashes.")
            .Produces<SpacePageShape>();

        door.MapPatch("/{*path}", (string name, string path, ChangeSpacePageRequest? request, HttpRequest http, ChangeSpacePage change, CancellationToken cancellationToken) =>
                change.ExecuteAsync(
                    name,
                    path,
                    new SpacePageChanges(request?.Slug, request?.Title, request?.BodyGiven ?? false, request?.Body),
                    http.Headers.IfMatch.ToString(),
                    cancellationToken))
            .WithName("ChangeSpacePage")
            .WithSummary("Change the title, the Markdown or the slug; `If-Match` with the `updated_at` last read guards the document. The parent is not here: moving is an act of its own.")
            .Produces<SpacePageShape>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed);

        door.MapDelete("/{*path}", async (string name, string path, DeleteSpacePage delete, CancellationToken cancellationToken) =>
                new DeletedPages(await delete.ExecuteAsync(name, path, cancellationToken)))
            .WithName("DeleteSpacePage")
            .WithSummary("Soft-delete a page and every page under it; the answer says how many went. Their slugs stay spent until the purge.")
            .Produces<DeletedPages>();

        return endpoints;
    }
}
