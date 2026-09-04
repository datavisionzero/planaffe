using Microsoft.EntityFrameworkCore;
using Npgsql;
using Planaffe.Application.Ports;
using Planaffe.Domain;
using Planaffe.Domain.Identities;

namespace Planaffe.Infrastructure.Persistence;

/// <summary>The identity rows, out of the one table that holds both kinds.</summary>
public sealed class Identities(PlanaffeDbContext context) : IIdentities
{
    public Task<bool> AnyAsync(CancellationToken cancellationToken) =>
        context.Identities.AnyAsync(cancellationToken);

    public Task<Identity?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        context.Identities.SingleOrDefaultAsync(i => i.Id == id, cancellationToken);

    public Task<Agent?> FindAgentAsync(Guid id, CancellationToken cancellationToken) =>
        context.Agents.SingleOrDefaultAsync(a => a.Id == id, cancellationToken);

    public Task<Identity?> FindByNameAsync(string name, CancellationToken cancellationToken)
    {
        var lowered = name.Trim().ToLower();
        return context.Identities.SingleOrDefaultAsync(i => i.Name.ToLower() == lowered, cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, Identity>> FindManyAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken)
    {
        var list = ids.Distinct().ToArray();
        return list.Length == 0
            ? new Dictionary<Guid, Identity>()
            : await context.Identities.Where(i => list.Contains(i.Id)).ToDictionaryAsync(i => i.Id, cancellationToken);
    }

    // The same comparison the unique index `identity_name` makes, so that the
    // check and the constraint agree about what "taken" means.
    public Task<bool> NameTakenAsync(string name, CancellationToken cancellationToken) =>
        context.Identities.AnyAsync(i => i.Name.ToLower() == name.ToLower(), cancellationToken);

    public async Task<IReadOnlyList<User>> ListUsersAsync(CancellationToken cancellationToken) =>
        await context.Users.OrderBy(u => u.CreatedAt).ThenBy(u => u.Id).ToListAsync(cancellationToken);

    // One statement for the list: the agent, its owner and its one token,
    // which the partial unique index `token_agent` guarantees is one. Projected
    // to a plain shape and built afterwards: EF Core translates joins on
    // columns, not the construction of a record out of three entities.
    public async Task<IReadOnlyList<AgentRow>> ListAgentsAsync(CancellationToken cancellationToken)
    {
        var rows = await (
            from agent in context.Agents
            join owner in context.Identities on agent.OwnerId equals owner.Id
            join token in context.Tokens on agent.Id equals token.IdentityId
            where token.Kind == IdentityKind.Agent
            orderby agent.CreatedAt, agent.Id
            select new { agent, owner, token }).ToListAsync(cancellationToken);

        return [.. rows.Select(row => new AgentRow(row.agent, row.owner, row.token))];
    }

    // One SaveChanges is one transaction: the identity and its token arrive
    // together or not at all.
    public async Task AddAsync(Identity identity, Token token, CancellationToken cancellationToken)
    {
        context.Identities.Add(identity);
        context.Tokens.Add(token);
        await SaveOrRefuseTheNameAsync(identity.Name, cancellationToken);
    }

    public async Task RecordMetadataAsync(Agent agent, AgentMetadataReport report, CancellationToken cancellationToken)
    {
        context.AgentMetadataReports.Add(report);
        await context.SaveChangesAsync(cancellationToken);
    }

    public Task RecordRenameAsync(Agent agent, CancellationToken cancellationToken) =>
        SaveOrRefuseTheNameAsync(agent.Name, cancellationToken);

    // The race between NameTakenAsync and the write lands on the unique index,
    // and the answer is the same one the check would have given.
    private async Task SaveOrRefuseTheNameAsync(string name, CancellationToken cancellationToken)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException collision) when (collision.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "identity_name",
        })
        {
            throw Refusal.Validation("name", $"The name {name} is taken; names are unique across users and agents, whatever the case.");
        }
    }
}
