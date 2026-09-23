using System.Text.Json;
using System.Text.Json.Serialization;
using Planaffe.Application.Acts;

namespace Planaffe.Api.Http;

public sealed record CreatePageBody(string? Parent, string? Slug, string? Title, string? Body);

/// <summary>
/// A <c>PATCH</c> body where <c>null</c> and absent mean different things:
/// <c>"body": null</c> empties the document, an absent body leaves it. The
/// converter below is what tells the two apart.
/// </summary>
[JsonConverter(typeof(ChangePageRequestConverter))]
public sealed record ChangePageRequest(string? Slug, string? Title, bool BodyGiven, string? Body);

/// <param name="Path">The page, as the address inside its space.</param>
/// <param name="Space">The space it lands in, or absent to stay in this one.</param>
/// <param name="Parent">The page it lands under, or absent for the root of that space.</param>
public sealed record MovePageBody(string? Path, string? Space, string? Parent);

/// <param name="Path">The deleted page, as the address inside its space.</param>
public sealed record RestorePageBody(string? Path);

/// <summary>How many pages a deletion took, the page itself included.</summary>
public sealed record DeletedPages(int Deleted);

/// <inheritdoc cref="ChangePageRequest"/>
public sealed class ChangePageRequestConverter : JsonConverter<ChangePageRequest>
{
    public override ChangePageRequest Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var body = JsonElement.ParseValue(ref reader);
        if (body.ValueKind is not JsonValueKind.Object)
        {
            throw new JsonException("A page change is an object.");
        }

        return new ChangePageRequest(
            PatchBody.Text(body, "slug"),
            PatchBody.Text(body, "title"),
            PatchBody.Given(body, "body"),
            PatchBody.Text(body, "body"));
    }

    public override void Write(Utf8JsonWriter writer, ChangePageRequest value, JsonSerializerOptions options)
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

}

/// <summary>
/// The pages of a space (<c>docs/api.md</c>, Pages): the knowledge base's tree,
/// under the space the way an issue is under its project.
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
public static class PageEndpoints
{
    public static IEndpointRouteBuilder MapPages(this IEndpointRouteBuilder endpoints)
    {
        var door = endpoints.MapGroup("/spaces/{name}/pages")
            .RequireAuthorization()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        door.MapGet(string.Empty, (string name, ListPages list, CancellationToken cancellationToken) =>
                list.ExecuteAsync(name, cancellationToken))
            .WithName("ListPages")
            .WithSummary("The whole tree as slim PageSummary, without the bodies: a page, then everything under it, siblings by title. Not paginated.")
            .Produces<IReadOnlyList<PageSummaryShape>>();

        door.MapPost(string.Empty, async (string name, CreatePageBody? request, CreatePage create, CancellationToken cancellationToken) =>
            {
                var page = await create.ExecuteAsync(
                    name,
                    new CreatePageRequest(request?.Parent, request?.Slug, request?.Title, request?.Body),
                    cancellationToken);
                return Results.Created($"/spaces/{name}/pages/{page.Path}", page);
            })
            .WithName("CreatePage")
            .WithSummary("Create a page under `parent`, or directly under the space where it is absent. The slug is given, never derived from the title.")
            .Produces<PageShape>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        door.MapPost("/move", (string name, MovePageBody? request, MovePage move, CancellationToken cancellationToken) =>
                move.ExecuteAsync(
                    name,
                    request?.Path ?? string.Empty,
                    new PageMove(request?.Space, request?.Parent),
                    cancellationToken))
            .WithName("MovePage")
            .WithSummary("Move a page under another parent, in this space or another one, with everything below it. `cycle` under itself, `too-deep` where the subtree would pass the third level.")
            .Produces<PageShape>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        door.MapPost("/restore", (string name, RestorePageBody? request, RestorePage restore, CancellationToken cancellationToken) =>
                restore.ExecuteAsync(name, request?.Path ?? string.Empty, cancellationToken))
            .WithName("RestorePage")
            .WithSummary("Bring a deleted page back with exactly what went along. A page whose parent is still deleted is `transition`.")
            .Produces<PageShape>()
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        door.MapGet("/{*path}", (string name, string path, ReadPage read, CancellationToken cancellationToken) =>
                read.ExecuteAsync(name, path, cancellationToken))
            .WithName("ReadPage")
            .WithSummary("The complete page: the Markdown, its address, its parent's, the author and who touched it last. `path` is the slugs from the space down, separated by slashes.")
            .Produces<PageShape>();

        door.MapPatch("/{*path}", (string name, string path, ChangePageRequest? request, HttpRequest http, ChangePage change, CancellationToken cancellationToken) =>
            {
                var given = PatchBody.Required(request, "A page change");
                return change.ExecuteAsync(
                    name,
                    path,
                    new PageChanges(given.Slug, given.Title, given.BodyGiven, given.Body),
                    http.Headers.IfMatch.ToString(),
                    cancellationToken);
            })
            .WithName("ChangePage")
            .WithSummary("Change the title, the Markdown or the slug; `If-Match` with the `updated_at` last read guards the document. The parent is not here: moving is an act of its own.")
            .Produces<PageShape>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed);

        door.MapDelete("/{*path}", async (string name, string path, DeletePage delete, CancellationToken cancellationToken) =>
                new DeletedPages(await delete.ExecuteAsync(name, path, cancellationToken)))
            .WithName("DeletePage")
            .WithSummary("Soft-delete a page and every page under it; the answer says how many went. Their slugs stay spent until the purge.")
            .Produces<DeletedPages>();

        return endpoints;
    }

    /// <summary>
    /// The one search across the knowledge base (VISION 18), and it is
    /// deliberately not under a space.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>/spaces/{name}/pages</c> could not hold it twice over: it is the
    /// tree, a catch-all follows it and no literal segment can follow that,
    /// and a <c>q</c> there would be a search that never reaches past one
    /// space. <c>/spaces/search</c> is out for the reason a space is created
    /// through a dialog rather than at <c>/spaces/new</c> — <c>search</c> is a
    /// name somebody may take.
    /// </para>
    /// <para>
    /// So it is <c>/pages</c>, which the knowledge base now has to itself: the
    /// project's wiki is withdrawn (VISION 18). Without <c>q</c> the answer is
    /// <c>validation</c> rather than every page in the instance — the address
    /// is a search and not a list.
    /// </para>
    /// </remarks>
    public static IEndpointRouteBuilder MapKnowledgeSearch(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGet(
                "/pages",
                (string? q, string? space, int? limit, SearchPages search, CancellationToken cancellationToken) =>
                    search.ExecuteAsync(q, space, limit, cancellationToken))
            .RequireAuthorization()
            .WithName("SearchPages")
            .WithSummary(
                "Full-text search over the titles and bodies of the knowledge base's pages, best first, in the spaces the caller may see. "
                + "`q` takes the words a search box takes and is required; `space` narrows to one space by name; `limit` is 1 to 100 and defaults to 20. "
                + "A hit carries where the page stands and an excerpt, never a body.")
            .Produces<IReadOnlyList<PageHitShape>>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return endpoints;
    }
}
