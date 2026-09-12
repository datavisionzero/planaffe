using Planaffe.Application.Acts;
using Planaffe.Application.Ports;
using Planaffe.Domain;
using Planaffe.Domain.Identities;
using Planaffe.Domain.Spaces;

namespace Planaffe.UnitTests;

/// <summary>
/// Who may say who sees a space (VISION 12, ADR 0027): an administrator grants
/// and revokes, a user named on the space may read the list, everybody else is
/// told there is no such space, and an agent is refused before any of it.
/// </summary>
public sealed class SpaceAccessActsTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 12, 12, 0, 0, TimeSpan.Zero);

    private static readonly InstanceSettings Settings = InstanceSettings.Defaults;

    [Fact]
    public async Task An_administrator_grants_and_revokes()
    {
        var world = new World();

        await new GrantSpaceAccess(world.As(world.Administrator), world, world, world, Settings, TimeProvider.System)
            .ExecuteAsync("handbuch", world.Colleague.Id, CancellationToken.None);

        Assert.Contains(world.Colleague.Id, world.Granted);

        await new RevokeSpaceAccess(world.As(world.Administrator), world, world, world, Settings)
            .ExecuteAsync("handbuch", world.Colleague.Id, CancellationToken.None);

        Assert.DoesNotContain(world.Colleague.Id, world.Granted);
    }

    [Fact]
    public async Task A_user_who_is_not_an_administrator_grants_nothing()
    {
        var world = new World();

        var refusal = await Assert.ThrowsAsync<Refusal>(() =>
            new GrantSpaceAccess(world.As(world.Colleague), world, world, world, Settings, TimeProvider.System)
                .ExecuteAsync("handbuch", world.Colleague.Id, CancellationToken.None));

        Assert.Equal(RefusalCode.Forbidden, refusal.Code);
        Assert.Empty(world.Granted);
    }

    [Fact]
    public async Task An_agent_neither_grants_nor_reads_the_list()
    {
        var world = new World();

        Assert.Equal(
            RefusalCode.Forbidden,
            (await Assert.ThrowsAsync<Refusal>(() =>
                new GrantSpaceAccess(world.As(world.Worker), world, world, world, Settings, TimeProvider.System)
                    .ExecuteAsync("handbuch", world.Colleague.Id, CancellationToken.None))).Code);

        Assert.Equal(
            RefusalCode.Forbidden,
            (await Assert.ThrowsAsync<Refusal>(() =>
                new ListSpaceUsers(world.As(world.Worker), world, world, Settings)
                    .ExecuteAsync("handbuch", CancellationToken.None))).Code);
    }

    /// <summary>
    /// Administering is not access: the administrator reads the list of a
    /// space nobody named them on, and sees who is on it — not what is in it.
    /// </summary>
    [Fact]
    public async Task An_administrator_reads_the_list_without_being_on_it()
    {
        var world = new World();

        var users = await new ListSpaceUsers(world.As(world.Administrator), world, world, Settings)
            .ExecuteAsync("handbuch", CancellationToken.None);

        Assert.Empty(users);
        Assert.DoesNotContain(world.Administrator.Id, world.Granted);
    }

    [Fact]
    public async Task A_user_who_is_not_on_the_space_is_told_there_is_none()
    {
        var world = new World();

        var refusal = await Assert.ThrowsAsync<Refusal>(() =>
            new ListSpaceUsers(world.As(world.Colleague), world, world, Settings)
                .ExecuteAsync("handbuch", CancellationToken.None));

        Assert.Equal(RefusalCode.NotFound, refusal.Code);
    }

    [Fact]
    public async Task A_deleted_space_says_how_long_it_can_still_come_back()
    {
        var world = new World();
        world.Space.Delete(world.Administrator.Id, Now);

        var refusal = await Assert.ThrowsAsync<Refusal>(() =>
            new GrantSpaceAccess(world.As(world.Administrator), world, world, world, Settings, TimeProvider.System)
                .ExecuteAsync("handbuch", world.Colleague.Id, CancellationToken.None));

        Assert.Equal(RefusalCode.Deleted, refusal.Code);
        Assert.True(refusal.Extensions.ContainsKey("restorable_until"));
    }

    [Fact]
    public async Task Granting_to_somebody_who_is_not_a_user_names_that()
    {
        var world = new World();

        var refusal = await Assert.ThrowsAsync<Refusal>(() =>
            new GrantSpaceAccess(world.As(world.Administrator), world, world, world, Settings, TimeProvider.System)
                .ExecuteAsync("handbuch", Guid.CreateVersion7(), CancellationToken.None));

        Assert.Equal(RefusalCode.NotFound, refusal.Code);
    }

    /// <summary>One space, three identities and the grants between them, in memory.</summary>
    private sealed class World : ISpaces, ISpaceAccess, IIdentities
    {
        public User Administrator { get; } = User.Create("maintainer", administrator: true, Now);

        public User Colleague { get; } = User.Create("colleague", administrator: false, Now);

        public Agent Worker { get; }

        public Space Space { get; }

        public HashSet<Guid> Granted { get; } = [];

        public World()
        {
            Worker = Agent.Create("quiet-otter-42", Administrator.Id, Now);
            Space = Space.Create("handbuch", "Handbuch", Administrator.Id, Now);
        }

        public ICallerIdentity As(Identity identity) => new Presented(identity);

        public Task<Space?> FindAnyAsync(string name, CancellationToken cancellationToken) =>
            Task.FromResult(name == Space.Name ? Space : null);

        public Task<bool> HasAsync(Guid userId, Guid spaceId, CancellationToken cancellationToken) =>
            Task.FromResult(spaceId == Space.Id && Granted.Contains(userId));

        public Task<IReadOnlyList<User>> UsersAsync(Guid spaceId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<User>>(
                [.. new[] { Administrator, Colleague }.Where(u => Granted.Contains(u.Id))]);

        public Task GrantAsync(SpaceAccess access, CancellationToken cancellationToken)
        {
            Granted.Add(access.UserId);
            return Task.CompletedTask;
        }

        public Task RevokeAsync(Guid spaceId, Guid userId, CancellationToken cancellationToken)
        {
            Granted.Remove(userId);
            return Task.CompletedTask;
        }

        public Task<User?> FindUserAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(new[] { Administrator, Colleague }.FirstOrDefault(u => u.Id == id));

        // Everything below is a port these three acts hold and never reach.
        public Task<Space?> FindLiveAsync(string name, CancellationToken cancellationToken) => throw Unasked();

        public Task<Space?> FindByIdAsync(Guid id, CancellationToken cancellationToken) => throw Unasked();

        public Task<bool> NameTakenAsync(string name, CancellationToken cancellationToken) => throw Unasked();

        public Task<IReadOnlyList<Space>> ListAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) => throw Unasked();

        public Task<IReadOnlyList<Space>> ListAllAsync(CancellationToken cancellationToken) => throw Unasked();

        public Task<IReadOnlySet<Guid>> OpenToAgentsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) => throw Unasked();

        public Task AddAsync(Space space, SpaceAccess creatorAccess, CancellationToken cancellationToken) => throw Unasked();

        public Task SaveAsync(CancellationToken cancellationToken) => throw Unasked();

        public Task<IReadOnlySet<Guid>> SpaceIdsAsync(Guid userId, CancellationToken cancellationToken) => throw Unasked();

        public Task<bool> AnyAsync(CancellationToken cancellationToken) => throw Unasked();

        public Task<Identity?> FindAsync(Guid id, CancellationToken cancellationToken) => throw Unasked();

        public Task<Agent?> FindAgentAsync(Guid id, CancellationToken cancellationToken) => throw Unasked();

        public Task<Identity?> FindByNameAsync(string name, CancellationToken cancellationToken) => throw Unasked();

        public Task<IReadOnlyDictionary<Guid, Identity>> FindManyAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken) => throw Unasked();

        Task<bool> IIdentities.NameTakenAsync(string name, CancellationToken cancellationToken) => throw Unasked();

        public Task<IReadOnlyList<User>> ListUsersAsync(CancellationToken cancellationToken) => throw Unasked();

        public Task<IReadOnlyList<AgentRow>> ListAgentsAsync(CancellationToken cancellationToken) => throw Unasked();

        public Task AddAsync(Identity identity, Token token, CancellationToken cancellationToken) => throw Unasked();

        public Task RecordRenameAsync(Agent agent, CancellationToken cancellationToken) => throw Unasked();

        public Task RecordMetadataAsync(Agent agent, AgentMetadataReport report, CancellationToken cancellationToken) => throw Unasked();

        private static NotSupportedException Unasked() => new("These acts do not go there.");
    }

    private sealed class Presented(Identity identity) : ICallerIdentity
    {
        public Caller Caller { get; } = Caller.Of(identity, Token.Issue(identity, TokenSecret.Generate(), Now));
    }
}
