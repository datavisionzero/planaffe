using Microsoft.EntityFrameworkCore;
using Npgsql;
using Planaffe.Application.Ports;
using Planaffe.Domain;
using Planaffe.Domain.Projects;
namespace Planaffe.Infrastructure.Persistence;

public sealed class ProjectGroups(PlanaffeDbContext context) : IProjectGroups
{
    public async Task<IReadOnlyList<ProjectGroup>> ListAsync(CancellationToken ct) => await context.Set<ProjectGroup>().OrderBy(g => g.Name).ToListAsync(ct);
    public Task<ProjectGroup?> FindAsync(Guid id, CancellationToken ct) => context.Set<ProjectGroup>().SingleOrDefaultAsync(g => g.Id == id, ct);
    public async Task SaveAsync(ProjectGroup group, bool create, CancellationToken ct)
    {
        if (create) context.Add(group);
        try { await context.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        { throw Refusal.Validation("name", "A project group with that name already exists."); }
    }
    public async Task DeleteAsync(ProjectGroup group, CancellationToken ct)
    {
        context.Remove(group);
        await context.SaveChangesAsync(ct);
    }
}
