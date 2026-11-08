using Planaffe.Domain.Projects;
namespace Planaffe.Application.Ports;

public interface IProjectGroups
{
    Task<IReadOnlyList<ProjectGroup>> ListAsync(CancellationToken ct);
    Task<ProjectGroup?> FindAsync(Guid id, CancellationToken ct);
    Task SaveAsync(ProjectGroup group, bool create, CancellationToken ct);
    Task DeleteAsync(ProjectGroup group, CancellationToken ct);
}
