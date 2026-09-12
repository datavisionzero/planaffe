using Planaffe.Application.Acts;
using Planaffe.Application.Ports;
using Planaffe.Domain;
using Planaffe.Domain.Identities;
using Planaffe.Domain.Spaces;

namespace Planaffe.UnitTests;

/// <summary>
/// The search across the knowledge base (VISION 18): that it asks for words,
/// that it only ever looks in the spaces the caller has — an agent's without
/// the closed ones — and that an excerpt leaves as pieces rather than as
/// markup.
/// </summary>
public sealed class SpaceSearchActsTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 13, 12, 0, 0, TimeSpan.Zero);

    private static readonly InstanceSettings Settings = InstanceSettings.Defaults;

    [Fact]
    public async Task A_search_without_words_is_refused()
    {
        var world = new World();

        var refusal = await Assert.ThrowsAsync<Refusal>(() => world.Search(world.Owner, "  "));

        Assert.Equal(RefusalCode.Validation, refusal.Code);
        Assert.Contains("q", refusal.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_limit_outside_the_range_is_refused()
    {
        var world = new World();

        Assert.Equal(
            RefusalCode.Validation,
            (await Assert.ThrowsAsync<Refusal>(() => world.Search(world.Owner, "onboarding", limit: 0))).Code);
        Assert.Equal(
            RefusalCode.Validation,
            (await Assert.ThrowsAsync<Refusal>(
                () => world.Search(world.Owner, "onboarding", limit: SearchSpacePages.MaxLimit + 1))).Code);
    }

    [Fact]
    public async Task The_whole_knowledge_base_is_every_space_the_caller_has()
    {
        var world = new World();

        await world.Search(world.Owner, "onboarding");

        Assert.Equal(new HashSet<Guid> { world.Handbook.Id, world.Personal.Id }, world.Asked!.ToHashSet());
        Assert.Equal(SearchSpacePages.DefaultLimit, world.Limit);
    }

    /// <summary>
    /// The one thing this act exists to get right: a closed space is not in
    /// the query, so it is not in a row and not in the number of rows either
    /// (ADR 0027).
    /// </summary>
    [Fact]
    public async Task An_agent_never_searches_a_space_closed_to_agents()
    {
        var world = new World();
        world.Personal.CloseToAgents(closed: true, Now);

        await world.Search(world.Worker, "onboarding");

        Assert.Equal([world.Handbook.Id], [.. world.Asked!]);
    }

    [Fact]
    public async Task One_space_by_name_searches_that_one()
    {
        var world = new World();

        await world.Search(world.Owner, "onboarding", space: "personal");

        Assert.Equal([world.Personal.Id], [.. world.Asked!]);
    }

    /// <summary>
    /// An empty list would say the space is there and holds nothing, which is
    /// exactly what the answer must not say.
    /// </summary>
    [Fact]
    public async Task A_space_this_caller_has_not_got_is_not_found()
    {
        var world = new World();

        Assert.Equal(
            RefusalCode.NotFound,
            (await Assert.ThrowsAsync<Refusal>(() => world.Search(world.Owner, "vertrag", space: "fremd"))).Code);
    }

    [Fact]
    public async Task A_hit_carries_the_way_down_to_it_and_its_excerpt()
    {
        var world = new World();
        world.Answer = [new SpacePageHitRow(
            "handbuch",
            "Handbuch",
            "onboarding/erster-tag",
            "Erster Tag",
            ["onboarding"],
            ["Onboarding"],
            $"Am {Excerpt.Start}ersten Tag{Excerpt.Stop} bekommt jede neue Person einen Zugang.")];

        var hits = await world.Search(world.Owner, "erster tag");

        var hit = Assert.Single(hits);
        Assert.Equal("onboarding/erster-tag", hit.Path);
        Assert.Equal("Handbuch", hit.SpaceTitle);
        Assert.Equal(new SpacePageStep("onboarding", "Onboarding"), Assert.Single(hit.Trail));
        Assert.Equal(
            [new ExcerptSegment("Am ", false), new ExcerptSegment("ersten Tag", true), new ExcerptSegment(" bekommt jede neue Person einen Zugang.", false)],
            hit.Excerpt);
    }

    [Fact]
    public void An_excerpt_without_a_match_is_one_piece()
    {
        var segments = Excerpts.Of("Nichts davon ist getroffen.");

        Assert.Equal([new ExcerptSegment("Nichts davon ist getroffen.", false)], segments);
    }

    [Fact]
    public void A_match_at_either_end_keeps_its_place()
    {
        Assert.Equal(
            [new ExcerptSegment("Vertrag", true), new ExcerptSegment(" und Kunde", false)],
            Excerpts.Of($"{Excerpt.Start}Vertrag{Excerpt.Stop} und Kunde"));
        Assert.Equal(
            [new ExcerptSegment("Kunde und ", false), new ExcerptSegment("Vertrag", true)],
            Excerpts.Of($"Kunde und {Excerpt.Start}Vertrag{Excerpt.Stop}"));
    }

    /// <summary>
    /// A mark whose partner was cut off is text, because a page cannot write
    /// one: swallowing the piece would lose a word of the page.
    /// </summary>
    [Fact]
    public void A_mark_without_its_partner_is_text()
    {
        var segments = Excerpts.Of($"Ein {Excerpt.Start}angeschnittener Treffer");

        Assert.Equal([new ExcerptSegment("Ein angeschnittener Treffer", false)], segments);
    }

    [Fact]
    public void An_empty_excerpt_is_no_piece_at_all()
    {
        Assert.Empty(Excerpts.Of(string.Empty));
        Assert.Empty(Excerpts.Of(null));
    }

    /// <summary>Three spaces, two identities, and a store that remembers what it was asked.</summary>
    private sealed class World : ISpaces, ISpaceAccess, ISpacePages
    {
        public World()
        {
            Owner = User.Create("maintainer", administrator: true, Now);
            Worker = Agent.Create("quiet-otter-42", Owner.Id, Now);
            Handbook = Space.Create("handbuch", "Handbuch", Owner.Id, Now);
            Personal = Space.Create("personal", "Personal", Owner.Id, Now);
            Foreign = Space.Create("fremd", "Fremd", Owner.Id, Now);
        }

        public User Owner { get; }

        public Agent Worker { get; }

        public Space Handbook { get; }

        public Space Personal { get; }

        /// <summary>A space nobody here is named on.</summary>
        public Space Foreign { get; }

        /// <summary>The spaces the store was told to look in, which is the whole point.</summary>
        public IReadOnlyCollection<Guid>? Asked { get; private set; }

        public int Limit { get; private set; }

        public IReadOnlyList<SpacePageHitRow> Answer { get; set; } = [];

        public Task<IReadOnlyList<SpacePageHitShape>> Search(
            Identity caller, string? query, string? space = null, int? limit = null) =>
            new SearchSpacePages(this, new SpaceScope(new Presented(caller), this, this), this, Settings)
                .ExecuteAsync(query, space, limit, CancellationToken.None);

        public Task<IReadOnlyList<SpacePageHitRow>> SearchAsync(
            IReadOnlyCollection<Guid> spaceIds, string query, int limit, CancellationToken cancellationToken)
        {
            Asked = spaceIds;
            Limit = limit;
            return Task.FromResult(Answer);
        }

        public Task<Space?> FindAnyAsync(string name, CancellationToken cancellationToken) =>
            Task.FromResult(new[] { Handbook, Personal, Foreign }.FirstOrDefault(s => s.Name == name));

        public Task<IReadOnlySet<Guid>> SpaceIdsAsync(Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlySet<Guid>>(
                userId == Owner.Id ? new HashSet<Guid> { Handbook.Id, Personal.Id } : new HashSet<Guid>());

        public Task<IReadOnlySet<Guid>> OpenToAgentsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlySet<Guid>>(
                ids.Where(id => !new[] { Handbook, Personal, Foreign }.Single(s => s.Id == id).ClosedToAgents).ToHashSet());

        // Everything below is a port this act holds and never reaches.
        public Task<Space?> FindLiveAsync(string name, CancellationToken cancellationToken) => throw Unasked();

        Task<Space?> ISpaces.FindByIdAsync(Guid id, CancellationToken cancellationToken) => throw Unasked();

        public Task<bool> NameTakenAsync(string name, CancellationToken cancellationToken) => throw Unasked();

        public Task<IReadOnlyList<Space>> ListAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) => throw Unasked();

        public Task<IReadOnlyList<Space>> ListAllAsync(CancellationToken cancellationToken) => throw Unasked();

        public Task AddAsync(Space space, SpaceAccess creatorAccess, CancellationToken cancellationToken) => throw Unasked();

        Task ISpaces.SaveAsync(CancellationToken cancellationToken) => throw Unasked();

        public Task<bool> HasAsync(Guid userId, Guid spaceId, CancellationToken cancellationToken) => throw Unasked();

        public Task<IReadOnlyList<User>> UsersAsync(Guid spaceId, CancellationToken cancellationToken) => throw Unasked();

        public Task GrantAsync(SpaceAccess access, CancellationToken cancellationToken) => throw Unasked();

        public Task RevokeAsync(Guid spaceId, Guid userId, CancellationToken cancellationToken) => throw Unasked();

        public Task<SpacePage?> FindLiveAsync(Guid spaceId, Guid? parentId, string slug, CancellationToken cancellationToken) => throw Unasked();

        public Task<SpacePage?> FindAnyAsync(Guid spaceId, Guid? parentId, string slug, CancellationToken cancellationToken) => throw Unasked();

        Task<SpacePage?> ISpacePages.FindByIdAsync(Guid id, CancellationToken cancellationToken) => throw Unasked();

        public Task<IReadOnlyList<SpacePage>> TreeAsync(Guid spaceId, CancellationToken cancellationToken) => throw Unasked();

        public Task<IReadOnlyList<SpacePage>> DescendantsAsync(Guid pageId, CancellationToken cancellationToken) => throw Unasked();

        public Task<IReadOnlyList<SpacePage>> CompanionsAsync(Guid pageId, CancellationToken cancellationToken) => throw Unasked();

        public Task DeleteDescendantsAsync(Guid pageId, Guid by, DateTimeOffset at, CancellationToken cancellationToken) => throw Unasked();

        public Task RestoreCompanionsAsync(Guid pageId, CancellationToken cancellationToken) => throw Unasked();

        public Task ShiftDescendantsAsync(Guid pageId, Guid spaceId, int levels, CancellationToken cancellationToken) => throw Unasked();

        public Task<SpacePage?> LoadForWriteAsync(Guid id, CancellationToken cancellationToken) => throw Unasked();

        public void Add(SpacePage page) => throw Unasked();

        Task ISpacePages.SaveAsync(CancellationToken cancellationToken) => throw Unasked();

        private static NotSupportedException Unasked() => new("This act does not go there.");
    }

    private sealed class Presented(Identity identity) : ICallerIdentity
    {
        public Caller Caller { get; } = Caller.Of(identity, Token.Issue(identity, TokenSecret.Generate(), Now));
    }
}
