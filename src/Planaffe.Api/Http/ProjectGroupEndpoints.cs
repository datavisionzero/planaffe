using Planaffe.Application.Acts;
namespace Planaffe.Api.Http;

public sealed record SaveProjectGroupRequest(string? Name, string? Description);
public static class ProjectGroupEndpoints
{
    public static void MapProjectGroups(this IEndpointRouteBuilder endpoints)
    {
        var door = endpoints.MapGroup("/project-groups").RequireAuthorization().WithTags("Project groups");
        door.MapGet("", (ProjectGroupActs acts, CancellationToken ct) => acts.ListAsync(ct)).WithName("ListProjectGroups");
        door.MapPost("", async (SaveProjectGroupRequest body, ProjectGroupActs acts, CancellationToken ct) =>
        {
            var group = await acts.SaveAsync(null, body.Name, body.Description, ct);
            return Results.Created($"/project-groups/{group.Id}", group);
        }).WithName("CreateProjectGroup").Produces<ProjectGroupShape>(StatusCodes.Status201Created);
        door.MapPut("/{id:guid}", (Guid id, SaveProjectGroupRequest body, ProjectGroupActs acts, CancellationToken ct) => acts.SaveAsync(id, body.Name, body.Description, ct)).WithName("ChangeProjectGroup");
        door.MapDelete("/{id:guid}", async (Guid id, ProjectGroupActs acts, CancellationToken ct) => { await acts.DeleteAsync(id, ct); return Results.NoContent(); }).WithName("DeleteProjectGroup").Produces(StatusCodes.Status204NoContent);
    }
}
