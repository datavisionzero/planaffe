using Planaffe.Application.Acts;
using Planaffe.Application.Ports;
using Planaffe.Domain;
using Planaffe.Domain.Identities;
using Planaffe.Domain.Spaces;

namespace Planaffe.UnitTests;

/// <summary>
/// The border around the bracket (ADR 0015, ADR 0027): an agent creates no
/// space, renames none, opens none it was closed out of, and deletes none —
/// and it is refused before a single store is asked, which is what the
/// throwing substitutes below prove.
/// </summary>
public sealed class SpaceActsTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 12, 12, 0, 0, TimeSpan.Zero);

    private static readonly User Maintainer = User.Create("maintainer", administrator: true, Now);

    private static readonly User Colleague = User.Create("colleague", administrator: false, Now);

    private static readonly Agent Worker = Agent.Create("quiet-otter-42", Maintainer.Id, Now);

    private static readonly InstanceSettings Settings = InstanceSettings.Defaults;

    [Fact]
    public async Task An_agent_creates_no_space()
    {
        var act = new CreateSpace(
            Whoever(Worker), new Untouched(), new Untouched(), new SpaceAssembler(new NoIdentities()),
            Settings, TimeProvider.System);

        var refusal = await Assert.ThrowsAsync<Refusal>(() =>
            act.ExecuteAsync(new CreateSpaceRequest("handbuch", "Handbuch"), CancellationToken.None));

        Assert.Equal(RefusalCode.Forbidden, refusal.Code);
        Assert.Contains("ADR 0015", refusal.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_agent_changes_no_space()
    {
        var act = new ChangeSpace(
            Whoever(Worker), new Untouched(), Scope(Worker), new Untouched(),
            new SpaceAssembler(new NoIdentities()), Settings, TimeProvider.System);

        var refusal = await Assert.ThrowsAsync<Refusal>(() =>
            act.ExecuteAsync("handbuch", new SpaceChanges(null, null, ClosedToAgents: false), CancellationToken.None));

        Assert.Equal(RefusalCode.Forbidden, refusal.Code);
    }

    [Fact]
    public async Task An_agent_deletes_no_space()
    {
        var act = new MoveSpace(Whoever(Worker), new Untouched(), new Untouched(),
            new SpaceAssembler(new NoIdentities()), TimeProvider.System);

        Assert.Equal(
            RefusalCode.Forbidden,
            (await Assert.ThrowsAsync<Refusal>(() => act.DeleteAsync("handbuch", CancellationToken.None))).Code);
    }

    /// <summary>
    /// Deleting a bracket is an administrator's act, as with a project
    /// (VISION 12): a user who is named on the space still may not take it
    /// away from everybody else named on it.
    /// </summary>
    [Fact]
    public async Task A_user_who_is_not_an_administrator_deletes_no_space()
    {
        var act = new MoveSpace(Whoever(Colleague), new Untouched(), new Untouched(),
            new SpaceAssembler(new NoIdentities()), TimeProvider.System);

        var refusal = await Assert.ThrowsAsync<Refusal>(() => act.DeleteAsync("handbuch", CancellationToken.None));

        Assert.Equal(RefusalCode.Forbidden, refusal.Code);
        Assert.Contains("administrator", refusal.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Only_an_administrator_lists_every_space()
    {
        var act = new ListAdminSpaces(Whoever(Colleague), new Untouched(), new SpaceAssembler(new NoIdentities()));

        Assert.Equal(
            RefusalCode.Forbidden,
            (await Assert.ThrowsAsync<Refusal>(() => act.ExecuteAsync(CancellationToken.None))).Code);
    }

    private static ICallerIdentity Whoever(Identity identity) => new Presented(identity);

    private static SpaceScope Scope(Identity identity) =>
        new(Whoever(identity), new Untouched(), new Untouched());

    private sealed class Presented(Identity identity) : ICallerIdentity
    {
        public Caller Caller { get; } = Caller.Of(identity, Token.Issue(identity, TokenSecret.Generate(), Now));
    }

    /// <summary>Every port these acts hold, and every method a scream: nothing here may be reached.</summary>
    private sealed class Untouched : ISpaces, ISpaceAccess, ITransactions
    {
        public Task<Space?> FindLiveAsync(string name, CancellationToken cancellationToken) => throw Reached();

        public Task<Space?> FindAnyAsync(string name, CancellationToken cancellationToken) => throw Reached();

        public Task<Space?> FindByIdAsync(Guid id, CancellationToken cancellationToken) => throw Reached();

        public Task<bool> NameTakenAsync(string name, CancellationToken cancellationToken) => throw Reached();

        public Task<IReadOnlyList<Space>> ListAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) => throw Reached();

        public Task<IReadOnlyList<Space>> ListAllAsync(CancellationToken cancellationToken) => throw Reached();

        public Task<IReadOnlySet<Guid>> OpenToAgentsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) => throw Reached();

        public Task AddAsync(Space space, SpaceAccess creatorAccess, CancellationToken cancellationToken) => throw Reached();

        public Task SaveAsync(CancellationToken cancellationToken) => throw Reached();

        public Task<bool> HasAsync(Guid userId, Guid spaceId, CancellationToken cancellationToken) => throw Reached();

        public Task<IReadOnlySet<Guid>> SpaceIdsAsync(Guid userId, CancellationToken cancellationToken) => throw Reached();

        public Task<IReadOnlyList<User>> UsersAsync(Guid spaceId, CancellationToken cancellationToken) => throw Reached();

        public Task GrantAsync(SpaceAccess access, CancellationToken cancellationToken) => throw Reached();

        public Task RevokeAsync(Guid spaceId, Guid userId, CancellationToken cancellationToken) => throw Reached();

        public Task<T> RunAsync<T>(Func<Task<T>> work, CancellationToken cancellationToken) => throw Reached();

        private static InvalidOperationException Reached() =>
            new("The refusal comes before the stores, or the border is drawn in the wrong place.");
    }

    /// <summary>The assembler's port, present because it is a constructor argument and for no other reason.</summary>
    private sealed class NoIdentities : IIdentities
    {
        public Task<bool> AnyAsync(CancellationToken cancellationToken) => throw Unasked();

        public Task<Identity?> FindAsync(Guid id, CancellationToken cancellationToken) => throw Unasked();

        public Task<Agent?> FindAgentAsync(Guid id, CancellationToken cancellationToken) => throw Unasked();

        public Task<Identity?> FindByNameAsync(string name, CancellationToken cancellationToken) => throw Unasked();

        public Task<IReadOnlyDictionary<Guid, Identity>> FindManyAsync(
            IEnumerable<Guid> ids, CancellationToken cancellationToken) => throw Unasked();

        public Task<bool> NameTakenAsync(string name, CancellationToken cancellationToken) => throw Unasked();

        public Task<IReadOnlyList<User>> ListUsersAsync(CancellationToken cancellationToken) => throw Unasked();

        public Task<IReadOnlyList<AgentRow>> ListAgentsAsync(CancellationToken cancellationToken) => throw Unasked();

        public Task AddAsync(Identity identity, Token token, CancellationToken cancellationToken) => throw Unasked();

        public Task RecordRenameAsync(Agent agent, CancellationToken cancellationToken) => throw Unasked();

        public Task RecordMetadataAsync(Agent agent, AgentMetadataReport report, CancellationToken cancellationToken) =>
            throw Unasked();

        private static NotSupportedException Unasked() =>
            new("No identity is resolved on a route that refuses before it begins.");
    }
}
