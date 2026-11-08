using Planaffe.Application.Ports;
using Planaffe.Domain;
using Planaffe.Domain.Projects;
namespace Planaffe.Application.Acts;

public sealed record ProjectGroupShape(Guid Id, string Name, string? Description)
{
    public static ProjectGroupShape Of(ProjectGroup group) => new(group.Id, group.Name, group.Description);
}
public sealed class ProjectGroupActs(ICallerIdentity identity, IProjectGroups groups, IProjects projects, ProjectScope scope)
{
    public async Task<IReadOnlyList<ProjectGroupShape>> ListAsync(CancellationToken ct)
    {
        var all = await groups.ListAsync(ct);
        if (identity.Caller.Administrator) return all.Select(ProjectGroupShape.Of).ToArray();
        var ids = await scope.ProjectIdsAsync(ct);
        var visible = (await projects.ListAsync(ct)).Where(p => ids.Contains(p.Id)).Select(p => p.GroupId).ToHashSet();
        return all.Where(g => visible.Contains(g.Id)).Select(ProjectGroupShape.Of).ToArray();
    }
    public async Task<ProjectGroupShape> SaveAsync(Guid? id, string? name, string? description, CancellationToken ct)
    {
        identity.Caller.RequireAdministrator("manage project groups");
        var group = id is null ? Validated.Field("name", () => ProjectGroup.Create(name!, description)) : await FindAsync(id.Value, ct);
        if (id is not null) Validated.Field("name", () => { group.Change(name!, description); return true; });
        await groups.SaveAsync(group, id is null, ct);
        return ProjectGroupShape.Of(group);
    }
    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        identity.Caller.RequireAdministrator("dissolve a project group");
        await groups.DeleteAsync(await FindAsync(id, ct), ct);
    }
    private async Task<ProjectGroup> FindAsync(Guid id, CancellationToken ct) => await groups.FindAsync(id, ct) ?? throw new Refusal(RefusalCode.NotFound, "No such project group.");
}
