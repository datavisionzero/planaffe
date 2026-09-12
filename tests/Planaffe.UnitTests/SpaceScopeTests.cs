using Planaffe.Application.Acts;
using Planaffe.Application.Ports;
using Planaffe.Domain;
using Planaffe.Domain.Identities;
using Planaffe.Domain.Spaces;

namespace Planaffe.UnitTests;

/// <summary>
/// The one authorization decision of the knowledge base, against substituted
/// stores: a user sees what they are named on, an agent sees its owner's
/// spaces minus every closed one, and what a caller may not see is missing
/// rather than refused (ADR 0027).
/// </summary>
public sealed class SpaceScopeTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 12, 12, 0, 0, TimeSpan.Zero);

    private static readonly User Maintainer = User.Create("maintainer", administrator: true, Now);

    private static readonly Agent Worker = Agent.Create("quiet-otter-42", Maintainer.Id, Now);

    [Fact]
    public async Task A_user_sees_every_space_they_are_named_on()
    {
        var open = Space.Create("handbuch", "Handbuch", Maintainer.Id, Now);
        var closed = Closed("personal");
        var scope = ScopeFor(Maintainer, [open, closed], [open.Id, closed.Id]);

        Assert.Equal(
            new[] { open.Id, closed.Id }.Order(),
            (await scope.SpaceIdsAsync(CancellationToken.None)).Order());
        await scope.RequireAsync(closed.Id, CancellationToken.None);
    }

    /// <summary>
    /// The switch is what the agent's set is missing, not what the owner's is:
    /// nothing about the grant changes, and the same owner keeps seeing it.
    /// </summary>
    [Fact]
    public async Task An_agent_inherits_its_owner_minus_the_closed_ones()
    {
        var open = Space.Create("handbuch", "Handbuch", Maintainer.Id, Now);
        var closed = Closed("personal");
        var scope = ScopeFor(Worker, [open, closed], [open.Id, closed.Id]);

        Assert.Equal([open.Id], await scope.SpaceIdsAsync(CancellationToken.None));
    }

    [Fact]
    public async Task A_closed_space_is_not_there_for_an_agent_rather_than_refused()
    {
        var closed = Closed("personal");
        var scope = ScopeFor(Worker, [closed], [closed.Id]);

        var refusal = await Assert.ThrowsAsync<Refusal>(() => scope.RequireAsync(closed.Id, CancellationToken.None));

        Assert.Equal(RefusalCode.NotFound, refusal.Code);
        Assert.DoesNotContain("closed", refusal.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("personal", refusal.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>A space nobody granted answers exactly as a closed one does.</summary>
    [Fact]
    public async Task A_space_nobody_granted_answers_the_same_way()
    {
        var stranger = Space.Create("fremd", "Fremd", Maintainer.Id, Now);
        var closed = Closed("personal");

        var user = await Assert.ThrowsAsync<Refusal>(() =>
            ScopeFor(Maintainer, [stranger], []).RequireAsync(stranger.Id, CancellationToken.None));
        var agent = await Assert.ThrowsAsync<Refusal>(() =>
            ScopeFor(Worker, [closed], [closed.Id]).RequireAsync(closed.Id, CancellationToken.None));

        Assert.Equal(agent.Code, user.Code);
        Assert.Equal(agent.Message, user.Message);
    }

    [Fact]
    public async Task An_agent_resolves_to_its_owner_before_anything_is_asked()
    {
        var open = Space.Create("handbuch", "Handbuch", Maintainer.Id, Now);
        var access = new Access(Maintainer.Id, [open.Id]);
        var scope = new SpaceScope(new Whoever(Worker), access, new Store([open]));

        Assert.Equal([open.Id], await scope.SpaceIdsAsync(CancellationToken.None));
        Assert.Equal(Maintainer.Id, access.AskedAbout);
    }

    private static Space Closed(string name)
    {
        var space = Space.Create(name, name, Maintainer.Id, Now);
        space.CloseToAgents(true, Now);
        return space;
    }

    private static SpaceScope ScopeFor(Identity identity, IReadOnlyList<Space> spaces, IReadOnlyList<Guid> granted) =>
        new(new Whoever(identity), new Access(Maintainer.Id, [.. granted]), new Store(spaces));

    private sealed class Whoever(Identity identity) : ICallerIdentity
    {
        public Caller Caller { get; } = Caller.Of(identity, Token.Issue(identity, TokenSecret.Generate(), Now));
    }

    private sealed class Access(Guid expectedUser, HashSet<Guid> granted) : ISpaceAccess
    {
        public Guid? AskedAbout { get; private set; }

        public Task<IReadOnlySet<Guid>> SpaceIdsAsync(Guid userId, CancellationToken cancellationToken)
        {
            AskedAbout = userId;
            return Task.FromResult<IReadOnlySet<Guid>>(userId == expectedUser ? granted : new HashSet<Guid>());
        }

        public Task<bool> HasAsync(Guid userId, Guid spaceId, CancellationToken cancellationToken) =>
            throw new NotSupportedException("The scope answers from the set, not row by row.");

        public Task<IReadOnlyList<User>> UsersAsync(Guid spaceId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task GrantAsync(SpaceAccess access, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task RevokeAsync(Guid spaceId, Guid userId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class Store(IReadOnlyList<Space> spaces) : ISpaces
    {
        public Task<IReadOnlySet<Guid>> OpenToAgentsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlySet<Guid>>(
                spaces.Where(s => ids.Contains(s.Id) && !s.Deleted && !s.ClosedToAgents).Select(s => s.Id).ToHashSet());

        public Task<Space?> FindLiveAsync(string name, CancellationToken cancellationToken) =>
            Task.FromResult(spaces.FirstOrDefault(s => s.Name == name && !s.Deleted));

        public Task<Space?> FindAnyAsync(string name, CancellationToken cancellationToken) =>
            Task.FromResult(spaces.FirstOrDefault(s => s.Name == name));

        public Task<Space?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(spaces.FirstOrDefault(s => s.Id == id));

        public Task<bool> NameTakenAsync(string name, CancellationToken cancellationToken) =>
            Task.FromResult(spaces.Any(s => s.Name == name));

        public Task<IReadOnlyList<Space>> ListAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Space>>([.. spaces.Where(s => ids.Contains(s.Id) && !s.Deleted)]);

        public Task<IReadOnlyList<Space>> ListAllAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Space>>([.. spaces]);

        public Task AddAsync(Space space, SpaceAccess creatorAccess, CancellationToken cancellationToken) =>
            throw new NotSupportedException("Nothing in these tests writes.");

        public Task SaveAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException("Nothing in these tests writes.");
    }
}
