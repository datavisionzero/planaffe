using Microsoft.EntityFrameworkCore;
using Npgsql;
using Planaffe.Application.Ports;
using Planaffe.Domain;
using Planaffe.Domain.Spaces;

namespace Planaffe.Infrastructure.Persistence;

/// <summary>The space rows, ordered by the name they are addressed by.</summary>
public sealed class Spaces(PlanaffeDbContext context) : ISpaces
{
    public Task<Space?> FindLiveAsync(string name, CancellationToken cancellationToken) =>
        context.Spaces.SingleOrDefaultAsync(s => s.Name == name && s.DeletedAt == null, cancellationToken);

    public Task<Space?> FindAnyAsync(string name, CancellationToken cancellationToken) =>
        context.Spaces.SingleOrDefaultAsync(s => s.Name == name, cancellationToken);

    public Task<Space?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
        context.Spaces.SingleOrDefaultAsync(s => s.Id == id, cancellationToken);

    public Task<bool> NameTakenAsync(string name, CancellationToken cancellationToken) =>
        context.Spaces.AnyAsync(s => s.Name == name, cancellationToken);

    public async Task<IReadOnlyList<Space>> ListAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
        ids.Count == 0
            ? []
            : await context.Spaces.Where(s => ids.Contains(s.Id) && s.DeletedAt == null)
                .OrderBy(s => s.Name).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Space>> ListAllAsync(CancellationToken cancellationToken) =>
        await context.Spaces.OrderBy(s => s.Name).ToListAsync(cancellationToken);

    public async Task<IReadOnlySet<Guid>> OpenToAgentsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
        ids.Count == 0
            ? new HashSet<Guid>()
            : (await context.Spaces
                .Where(s => ids.Contains(s.Id) && s.DeletedAt == null && !s.ClosedToAgents)
                .Select(s => s.Id)
                .ToListAsync(cancellationToken)).ToHashSet();

    public async Task AddAsync(Space space, SpaceAccess creatorAccess, CancellationToken cancellationToken)
    {
        context.Spaces.Add(space);
        context.SpaceAccesses.Add(creatorAccess);
        await SaveAsync(cancellationToken);
    }

    public async Task SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException collision) when (collision.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "space_name",
        })
        {
            throw Refusal.Validation(
                "name", "That name is taken — by a space, or by a deleted one waiting out its grace period.");
        }
    }
}
