using Planaffe.Application.Acts;
using Planaffe.Application.Ports;
using Planaffe.Domain;
using Planaffe.Domain.History;
using Planaffe.Domain.Identities;
using Planaffe.Domain.Spaces;

namespace Planaffe.UnitTests;

/// <summary>
/// The acts on a page of the knowledge base (VISION 18, ADR 0028): that the
/// address carries the tree, that the third level is the last one, that a slug
/// is taken only under its own parent, that the tree comes back in the order it
/// is drawn, and that a space closed to agents answers an agent exactly as a
/// space that never existed.
/// </summary>
public sealed class SpacePageActsTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 12, 12, 0, 0, TimeSpan.Zero);

    private static readonly InstanceSettings Settings = InstanceSettings.Defaults;

    [Fact]
    public async Task A_page_under_the_space_is_addressed_by_its_slug()
    {
        var world = new World();

        var page = await world.Create(world.Owner, null, "company", "Company", "Wer wir sind.");

        Assert.Equal("company", page.Path);
        Assert.Null(page.Parent);
        Assert.Equal(0, page.Depth);
        Assert.Equal("handbuch", page.Space);
        Assert.Equal("Wer wir sind.", page.Body);
        Assert.Equal(world.Owner.Name, page.Author.Name);
    }

    [Fact]
    public async Task A_child_is_addressed_through_its_parent()
    {
        var world = new World();
        await world.Create(world.Owner, null, "company", "Company");

        var child = await world.Create(world.Owner, "company", "onboarding", "Onboarding");

        Assert.Equal("company/onboarding", child.Path);
        Assert.Equal("company", child.Parent);
        Assert.Equal(1, child.Depth);
    }

    [Fact]
    public async Task A_fourth_level_is_refused_and_says_how_deep_the_parent_is()
    {
        var world = new World();
        await world.Create(world.Owner, null, "company", "Company");
        await world.Create(world.Owner, "company", "handbook", "Handbook");
        await world.Create(world.Owner, "company/handbook", "onboarding", "Onboarding");

        var refusal = await Assert.ThrowsAsync<Refusal>(() =>
            world.Create(world.Owner, "company/handbook/onboarding", "day-one", "Day one"));

        Assert.Equal(RefusalCode.TooDeep, refusal.Code);
        Assert.Equal(SpacePage.MaxDepth, refusal.Extensions["depth"]);
    }

    [Fact]
    public async Task A_slug_is_taken_under_its_parent_and_free_under_another()
    {
        var world = new World();
        await world.Create(world.Owner, null, "company", "Company");
        await world.Create(world.Owner, null, "product", "Product");
        await world.Create(world.Owner, "company", "overview", "Company overview");

        var elsewhere = await world.Create(world.Owner, "product", "overview", "Product overview");
        Assert.Equal("product/overview", elsewhere.Path);

        var refusal = await Assert.ThrowsAsync<Refusal>(() =>
            world.Create(world.Owner, "company", "overview", "Overview again"));

        Assert.Equal(RefusalCode.Validation, refusal.Code);
    }

    /// <summary>
    /// A deleted page holds its slug until the purge, and the refusal says so
    /// rather than pretending the name is in use (ADR 0013).
    /// </summary>
    [Fact]
    public async Task The_slug_of_a_deleted_page_is_still_taken_and_the_refusal_says_why()
    {
        var world = new World();
        var page = await world.Create(world.Owner, null, "company", "Company");
        world.Row(page.Path).Delete(world.Owner.Id, Now);

        var refusal = await Assert.ThrowsAsync<Refusal>(() =>
            world.Create(world.Owner, null, "company", "Company again"));

        Assert.Equal(RefusalCode.Validation, refusal.Code);
        Assert.Contains("deleted", refusal.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_deleted_page_says_how_long_it_can_still_come_back()
    {
        var world = new World();
        var page = await world.Create(world.Owner, null, "company", "Company");
        world.Row(page.Path).Delete(world.Owner.Id, Now);

        var refusal = await Assert.ThrowsAsync<Refusal>(() => world.Read(world.Owner, "company"));

        Assert.Equal(RefusalCode.Deleted, refusal.Code);
        Assert.True(refusal.Extensions.ContainsKey("restorable_until"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("company/handbook/onboarding/day-one")]
    [InlineData("Company")]
    [InlineData("company/nothing")]
    public async Task An_address_that_names_no_page_is_not_found(string path)
    {
        var world = new World();
        await world.Create(world.Owner, null, "company", "Company");

        var refusal = await Assert.ThrowsAsync<Refusal>(() => world.Read(world.Owner, path));

        Assert.Equal(RefusalCode.NotFound, refusal.Code);
    }

    [Fact]
    public async Task The_tree_puts_children_under_their_parents_and_siblings_by_title()
    {
        var world = new World();
        await world.Create(world.Owner, null, "product", "Product");
        await world.Create(world.Owner, null, "company", "Company");
        await world.Create(world.Owner, "company", "onboarding", "Onboarding");
        await world.Create(world.Owner, "company", "handbook", "Handbook");
        await world.Create(world.Owner, "company/handbook", "day-one", "Day one");

        var tree = await new ListSpacePages(world, world.Scope(world.Owner), world, world.Assembler, Settings)
            .ExecuteAsync("handbuch", CancellationToken.None);

        Assert.Equal(
            ["company", "company/handbook", "company/handbook/day-one", "company/onboarding", "product"],
            tree.Select(p => p.Path));
        Assert.Equal([0, 1, 2, 1, 0], tree.Select(p => p.Depth));
        Assert.Equal("company", tree[1].Parent);
    }

    [Fact]
    public async Task A_deleted_page_is_not_in_the_tree()
    {
        var world = new World();
        await world.Create(world.Owner, null, "company", "Company");
        var gone = await world.Create(world.Owner, null, "product", "Product");
        world.Row(gone.Path).Delete(world.Owner.Id, Now);

        var tree = await new ListSpacePages(world, world.Scope(world.Owner), world, world.Assembler, Settings)
            .ExecuteAsync("handbuch", CancellationToken.None);

        Assert.Equal(["company"], tree.Select(p => p.Path));
    }

    /// <summary>
    /// The bracket is a human's to draw and the work inside it is not: an
    /// agent of the owner writes in an open space like anybody else
    /// (VISION 18).
    /// </summary>
    [Fact]
    public async Task An_agent_writes_in_a_space_that_is_open_to_it()
    {
        var world = new World();

        var page = await world.Create(world.Worker, null, "company", "Company");

        Assert.Equal("company", page.Path);
        Assert.Equal(world.Worker.Name, page.Author.Name);
    }

    /// <summary>
    /// Closed, the space is not one an agent may see and not read: it is not
    /// there, and the refusal is word for word the stranger's (ADR 0027).
    /// </summary>
    [Fact]
    public async Task A_space_closed_to_agents_answers_an_agent_as_it_answers_a_stranger()
    {
        var world = new World();
        await world.Create(world.Owner, null, "company", "Company");
        world.Space.CloseToAgents(true, Now);

        var closed = await Assert.ThrowsAsync<Refusal>(() => world.Read(world.Worker, "company"));
        var stranger = await Assert.ThrowsAsync<Refusal>(() => world.Read(world.Colleague, "company"));

        Assert.Equal(RefusalCode.NotFound, closed.Code);
        Assert.Equal(stranger.Code, closed.Code);
        Assert.Equal(stranger.Detail, closed.Detail);

        Assert.Equal(
            RefusalCode.NotFound,
            (await Assert.ThrowsAsync<Refusal>(() => world.Create(world.Worker, null, "product", "Product"))).Code);
    }

    [Fact]
    public async Task Changing_writes_the_title_the_body_and_the_slug_and_records_what_moved()
    {
        var world = new World();
        var page = await world.Create(world.Owner, null, "company", "Company");

        var changed = await world.Change(
            world.Owner, page.Path, new SpacePageChanges("firma", "Firma", true, "Neuer Text."), null);

        Assert.Equal("firma", changed.Path);
        Assert.Equal("Firma", changed.Title);
        Assert.Equal("Neuer Text.", changed.Body);

        var fields = world.History.Where(h => h.SpacePageId == world.Row("firma").Id).Select(h => h.Field);
        Assert.Equal([HistoryField.Created, HistoryField.Slug, HistoryField.Title, HistoryField.Body], fields);

        var rename = world.History.Single(h => h.Field == HistoryField.Slug);
        Assert.Equal("company", rename.OldValue);
        Assert.Equal("firma", rename.NewValue);
    }

    [Fact]
    public async Task A_rename_onto_a_taken_slug_is_refused()
    {
        var world = new World();
        await world.Create(world.Owner, null, "company", "Company");
        var product = await world.Create(world.Owner, null, "product", "Product");

        var refusal = await Assert.ThrowsAsync<Refusal>(() =>
            world.Change(world.Owner, product.Path, new SpacePageChanges("company", null, false, null), null));

        Assert.Equal(RefusalCode.Validation, refusal.Code);
    }

    [Fact]
    public async Task A_write_against_an_older_version_is_stale_and_carries_the_current_one()
    {
        var world = new World();
        var page = await world.Create(world.Owner, null, "company", "Company");
        var stale = page.UpdatedAt.AddMinutes(-1);

        var refusal = await Assert.ThrowsAsync<Refusal>(() =>
            world.Change(world.Owner, page.Path, new SpacePageChanges(null, "Firma", false, null), stale.ToString("o")));

        Assert.Equal(RefusalCode.Stale, refusal.Code);
        Assert.IsType<SpacePageShape>(refusal.Extensions["current"]);
        Assert.Equal("Company", world.Row("company").Title);
    }

    [Fact]
    public async Task The_version_it_was_read_at_lets_the_write_through()
    {
        var world = new World();
        var page = await world.Create(world.Owner, null, "company", "Company");

        var changed = await world.Change(
            world.Owner, page.Path, new SpacePageChanges(null, "Firma", false, null), page.UpdatedAt.ToString("o"));

        Assert.Equal("Firma", changed.Title);
    }

    [Fact]
    public async Task Moving_a_page_takes_everything_under_it()
    {
        var world = new World();
        await world.Create(world.Owner, null, "company", "Company");
        await world.Create(world.Owner, null, "product", "Product");
        await world.Create(world.Owner, "product", "handbook", "Handbook");
        await world.Create(world.Owner, "product/handbook", "day-one", "Day one");

        var moved = await world.Move(world.Owner, "product/handbook", null, "company");

        Assert.Equal("company/handbook", moved.Path);
        Assert.Equal(1, moved.Depth);
        Assert.Equal(2, world.Row("company/handbook/day-one").Depth);
    }

    [Fact]
    public async Task Moving_to_the_root_lifts_the_subtree_with_it()
    {
        var world = new World();
        await world.Create(world.Owner, null, "company", "Company");
        await world.Create(world.Owner, "company", "handbook", "Handbook");
        await world.Create(world.Owner, "company/handbook", "day-one", "Day one");

        var moved = await world.Move(world.Owner, "company/handbook", null, null);

        Assert.Equal("handbook", moved.Path);
        Assert.Null(moved.Parent);
        Assert.Equal(0, moved.Depth);
        Assert.Equal(1, world.Row("handbook/day-one").Depth);
    }

    [Fact]
    public async Task Moving_into_another_space_carries_the_subtree_into_it()
    {
        var world = new World();
        await world.Create(world.Owner, null, "company", "Company");
        await world.Create(world.Owner, "company", "handbook", "Handbook");

        var moved = await world.Move(world.Owner, "company", "personal", null);

        Assert.Equal("personal", moved.Space);
        Assert.Equal(world.Other.Id, world.Row("company", world.Other).SpaceId);
        Assert.Equal(world.Other.Id, world.Row("company/handbook", world.Other).SpaceId);
    }

    [Fact]
    public async Task A_space_the_caller_cannot_see_is_no_destination()
    {
        var world = new World();
        await world.Create(world.Owner, null, "company", "Company");

        var refusal = await Assert.ThrowsAsync<Refusal>(() => world.Move(world.Owner, "company", "fremd", null));

        Assert.Equal(RefusalCode.NotFound, refusal.Code);
        Assert.Equal(world.Space.Id, world.Row("company").SpaceId);
    }

    [Fact]
    public async Task A_page_cannot_hang_under_itself_or_under_its_own_child()
    {
        var world = new World();
        await world.Create(world.Owner, null, "company", "Company");
        await world.Create(world.Owner, "company", "handbook", "Handbook");

        Assert.Equal(
            RefusalCode.Cycle,
            (await Assert.ThrowsAsync<Refusal>(() => world.Move(world.Owner, "company", null, "company"))).Code);

        Assert.Equal(
            RefusalCode.Cycle,
            (await Assert.ThrowsAsync<Refusal>(() => world.Move(world.Owner, "company", null, "company/handbook"))).Code);
    }

    [Fact]
    public async Task A_subtree_that_would_not_fit_is_refused_with_its_height()
    {
        var world = new World();
        await world.Create(world.Owner, null, "company", "Company");
        await world.Create(world.Owner, "company", "handbook", "Handbook");
        await world.Create(world.Owner, null, "product", "Product");
        await world.Create(world.Owner, "product", "notes", "Notes");

        var refusal = await Assert.ThrowsAsync<Refusal>(() => world.Move(world.Owner, "company", null, "product/notes"));

        Assert.Equal(RefusalCode.TooDeep, refusal.Code);
        Assert.Equal(1, refusal.Extensions["depth"]);
        Assert.Equal(0, world.Row("company").Depth);
    }

    [Fact]
    public async Task Landing_on_a_taken_slug_is_refused()
    {
        var world = new World();
        await world.Create(world.Owner, null, "company", "Company");
        await world.Create(world.Owner, null, "product", "Product");
        await world.Create(world.Owner, "product", "company", "Company again");

        var refusal = await Assert.ThrowsAsync<Refusal>(() => world.Move(world.Owner, "company", null, "product"));

        Assert.Equal(RefusalCode.Validation, refusal.Code);
    }

    [Fact]
    public async Task Moving_a_page_where_it_already_is_changes_nothing()
    {
        var world = new World();
        await world.Create(world.Owner, null, "company", "Company");
        var before = await world.Create(world.Owner, "company", "handbook", "Handbook");

        var moved = await world.Move(world.Owner, "company/handbook", null, "company");

        Assert.Equal(before.UpdatedAt, moved.UpdatedAt);
        Assert.DoesNotContain(world.History, h => h.Field == HistoryField.Parent);
    }

    [Fact]
    public async Task A_move_stands_in_the_history_with_both_addresses()
    {
        var world = new World();
        await world.Create(world.Owner, null, "company", "Company");
        await world.Create(world.Owner, null, "product", "Product");

        await world.Move(world.Owner, "product", "personal", null);

        var moved = world.History.Single(h => h.Field == HistoryField.Parent);
        Assert.Equal("handbuch/product", moved.OldValue);
        Assert.Equal("personal/product", moved.NewValue);
    }

    [Fact]
    public async Task Deleting_takes_the_subtree_and_says_how_many_went()
    {
        var world = new World();
        await world.Create(world.Owner, null, "company", "Company");
        await world.Create(world.Owner, "company", "handbook", "Handbook");
        await world.Create(world.Owner, "company/handbook", "day-one", "Day one");
        await world.Create(world.Owner, null, "product", "Product");

        var gone = await world.Delete(world.Owner, "company");

        Assert.Equal(3, gone);

        var tree = await new ListSpacePages(world, world.Scope(world.Owner), world, world.Assembler, Settings)
            .ExecuteAsync("handbuch", CancellationToken.None);
        Assert.Equal(["product"], tree.Select(p => p.Path));

        Assert.Equal(3, world.History.Count(h => h.Field == HistoryField.Deleted));
    }

    [Fact]
    public async Task Restoring_brings_back_what_went_along_and_nothing_else()
    {
        var world = new World();
        await world.Create(world.Owner, null, "company", "Company");
        await world.Create(world.Owner, "company", "handbook", "Handbook");
        await world.Create(world.Owner, "company", "notes", "Notes");

        await world.Delete(world.Owner, "company/notes");
        await world.Delete(world.Owner, "company");

        var back = await world.Restore(world.Owner, "company");

        Assert.Equal("company", back.Path);
        var tree = await new ListSpacePages(world, world.Scope(world.Owner), world, world.Assembler, Settings)
            .ExecuteAsync("handbuch", CancellationToken.None);
        Assert.Equal(["company", "company/handbook"], tree.Select(p => p.Path));
    }

    [Fact]
    public async Task A_page_under_a_deleted_one_is_not_restored_on_its_own()
    {
        var world = new World();
        await world.Create(world.Owner, null, "company", "Company");
        await world.Create(world.Owner, "company", "handbook", "Handbook");
        await world.Delete(world.Owner, "company");

        var refusal = await Assert.ThrowsAsync<Refusal>(() => world.Restore(world.Owner, "company/handbook"));

        Assert.Equal(RefusalCode.Transition, refusal.Code);
        Assert.Contains("company", refusal.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Restoring_a_page_that_is_not_deleted_is_refused()
    {
        var world = new World();
        await world.Create(world.Owner, null, "company", "Company");

        Assert.Equal(
            RefusalCode.Transition,
            (await Assert.ThrowsAsync<Refusal>(() => world.Restore(world.Owner, "company"))).Code);
    }

    /// <summary>Deleting is no more an administrator's act than writing is (ADR 0013).</summary>
    [Fact]
    public async Task An_agent_deletes_and_restores_in_a_space_that_is_open_to_it()
    {
        var world = new World();
        await world.Create(world.Owner, null, "company", "Company");

        Assert.Equal(1, await world.Delete(world.Worker, "company"));
        Assert.Equal("company", (await world.Restore(world.Worker, "company")).Path);
    }

    /// <summary>One space, four identities, the pages in memory and the history beside them.</summary>
    private sealed class World : ISpaces, ISpaceAccess, ISpacePages, IIdentities, IHistory, ITransactions
    {
        private readonly List<SpacePage> _pages = [];

        private readonly List<Identity> _identities;

        private DateTimeOffset _now = Now;

        public World()
        {
            Owner = User.Create("maintainer", administrator: true, Now);
            Colleague = User.Create("colleague", administrator: false, Now);
            Worker = Agent.Create("quiet-otter-42", Owner.Id, Now);
            Space = Space.Create("handbuch", "Handbuch", Owner.Id, Now);
            Other = Space.Create("personal", "Personal", Owner.Id, Now);
            Foreign = Space.Create("fremd", "Fremd", Colleague.Id, Now);
            _identities = [Owner, Colleague, Worker];
        }

        public User Owner { get; }

        public User Colleague { get; }

        public Agent Worker { get; }

        public Space Space { get; }

        /// <summary>A second space the owner sees, for a move out of the first.</summary>
        public Space Other { get; }

        /// <summary>A space nobody here is named on: the far end of a move that fails.</summary>
        public Space Foreign { get; }

        public List<HistoryEntry> History { get; } = [];

        public SpacePageAssembler Assembler => new(this, this);

        public SpaceScope Scope(Identity identity) => new(new Presented(identity), this, this);

        /// <summary>Every write moves the clock, so that a version says something.</summary>
        private TimeProvider Clock => new StoppedClock(_now = _now.AddMinutes(1));

        public Task<SpacePageShape> Create(Identity caller, string? parent, string slug, string title, string? body = null) =>
            new CreateSpacePage(new Presented(caller), this, Scope(caller), this, this, this, Assembler, Settings, Clock)
                .ExecuteAsync("handbuch", new CreateSpacePageRequest(parent, slug, title, body), CancellationToken.None);

        public Task<SpacePageShape> Move(Identity caller, string path, string? space, string? parent) =>
            new MoveSpacePage(new Presented(caller), this, Scope(caller), this, this, this, Assembler, Settings, Clock)
                .ExecuteAsync("handbuch", path, new SpacePageMove(space, parent), CancellationToken.None);

        public Task<int> Delete(Identity caller, string path) =>
            new DeleteSpacePage(new Presented(caller), this, Scope(caller), this, this, this, Settings, Clock)
                .ExecuteAsync("handbuch", path, CancellationToken.None);

        public Task<SpacePageShape> Restore(Identity caller, string path) =>
            new RestoreSpacePage(new Presented(caller), this, Scope(caller), this, this, this, Assembler, Settings, Clock)
                .ExecuteAsync("handbuch", path, CancellationToken.None);

        public Task<SpacePageShape> Read(Identity caller, string path) =>
            new ReadSpacePage(this, Scope(caller), this, Assembler, Settings).ExecuteAsync("handbuch", path, CancellationToken.None);

        public Task<SpacePageShape> Change(Identity caller, string path, SpacePageChanges changes, string? ifMatch) =>
            new ChangeSpacePage(new Presented(caller), this, Scope(caller), this, this, this, Assembler, Settings, Clock)
                .ExecuteAsync("handbuch", path, changes, ifMatch, CancellationToken.None);

        /// <summary>The row behind an address, for a test that wants to reach past the acts.</summary>
        public SpacePage Row(string path, Space? space = null)
        {
            SpacePage? page = null;
            foreach (var slug in path.Split('/'))
            {
                page = _pages.Single(p =>
                    p.ParentId == page?.Id && p.Slug == slug && (page is not null || p.SpaceId == (space ?? Space).Id));
            }

            return page!;
        }

        public Task<Space?> FindAnyAsync(string name, CancellationToken cancellationToken) =>
            Task.FromResult(new[] { Space, Other, Foreign }.FirstOrDefault(s => s.Name == name));

        public Task<IReadOnlySet<Guid>> SpaceIdsAsync(Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlySet<Guid>>(
                userId == Owner.Id ? new HashSet<Guid> { Space.Id, Other.Id } : []);

        public Task<IReadOnlySet<Guid>> OpenToAgentsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlySet<Guid>>(
                ids.Where(id => !new[] { Space, Other, Foreign }.Single(s => s.Id == id).ClosedToAgents).ToHashSet());

        public Task<SpacePage?> FindLiveAsync(Guid spaceId, Guid? parentId, string slug, CancellationToken cancellationToken) =>
            Task.FromResult(_pages.SingleOrDefault(p =>
                p.SpaceId == spaceId && p.ParentId == parentId && p.Slug == slug && !p.Deleted));

        public Task<SpacePage?> FindAnyAsync(Guid spaceId, Guid? parentId, string slug, CancellationToken cancellationToken) =>
            Task.FromResult(_pages.SingleOrDefault(p => p.SpaceId == spaceId && p.ParentId == parentId && p.Slug == slug));

        public Task<SpacePage?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(_pages.SingleOrDefault(p => p.Id == id));

        public Task<IReadOnlyList<SpacePage>> TreeAsync(Guid spaceId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SpacePage>>(
                [.. _pages.Where(p => p.SpaceId == spaceId && !p.Deleted)
                    .OrderBy(p => p.Depth)
                    .ThenBy(p => p.Title, StringComparer.Ordinal)
                    .ThenBy(p => p.Slug, StringComparer.Ordinal)]);

        public Task<IReadOnlyList<SpacePage>> DescendantsAsync(Guid pageId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SpacePage>>([.. Below(pageId).Where(p => !p.Deleted)]);

        public Task<IReadOnlyList<SpacePage>> CompanionsAsync(Guid pageId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SpacePage>>([.. _pages.Where(p => p.DeletedWith == pageId)]);

        public Task DeleteDescendantsAsync(Guid pageId, Guid by, DateTimeOffset at, CancellationToken cancellationToken)
        {
            foreach (var page in Below(pageId).Where(p => !p.Deleted))
            {
                page.Delete(by, at, pageId);
            }

            return Task.CompletedTask;
        }

        public Task RestoreCompanionsAsync(Guid pageId, CancellationToken cancellationToken)
        {
            foreach (var page in _pages.Where(p => p.DeletedWith == pageId).ToList())
            {
                page.Restore();
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// The statement the store writes, done a row at a time: shallowest
        /// first, so that every page is carried by a parent that has arrived
        /// already. It moves the version where the statement does not, which
        /// is the one thing a double cannot help here.
        /// </summary>
        public Task ShiftDescendantsAsync(Guid pageId, Guid spaceId, int levels, CancellationToken cancellationToken)
        {
            foreach (var page in Below(pageId))
            {
                page.MoveUnder(spaceId, _pages.Single(p => p.Id == page.ParentId), page.UpdatedBy, page.UpdatedAt);
            }

            return Task.CompletedTask;
        }

        public Task<SpacePage?> LoadForWriteAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(_pages.SingleOrDefault(p => p.Id == id));

        /// <summary>The subtree under a page, shallowest first, deleted rows included.</summary>
        private IEnumerable<SpacePage> Below(Guid pageId)
        {
            var level = _pages.Where(p => p.ParentId == pageId).ToList();
            while (level.Count > 0)
            {
                foreach (var page in level)
                {
                    yield return page;
                }

                level = [.. _pages.Where(p => level.Any(parent => parent.Id == p.ParentId))];
            }
        }

        public void Add(SpacePage page) => _pages.Add(page);

        public void Add(HistoryEntry entry) => History.Add(entry);

        public Task SaveAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<T> RunAsync<T>(Func<Task<T>> work, CancellationToken cancellationToken) => work();

        public Task<Identity?> FindAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(_identities.FirstOrDefault(i => i.Id == id));

        // Everything below is a port these acts hold and never reach.
        public Task<Space?> FindLiveAsync(string name, CancellationToken cancellationToken) => throw Unasked();

        Task<Space?> ISpaces.FindByIdAsync(Guid id, CancellationToken cancellationToken) => throw Unasked();

        Task<bool> ISpaces.NameTakenAsync(string name, CancellationToken cancellationToken) => throw Unasked();

        public Task<IReadOnlyList<Space>> ListAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) => throw Unasked();

        public Task<IReadOnlyList<Space>> ListAllAsync(CancellationToken cancellationToken) => throw Unasked();

        public Task AddAsync(Space space, SpaceAccess creatorAccess, CancellationToken cancellationToken) => throw Unasked();

        Task ISpaces.SaveAsync(CancellationToken cancellationToken) => throw Unasked();

        public Task<bool> HasAsync(Guid userId, Guid spaceId, CancellationToken cancellationToken) => throw Unasked();

        public Task<IReadOnlyList<User>> UsersAsync(Guid spaceId, CancellationToken cancellationToken) => throw Unasked();

        public Task GrantAsync(SpaceAccess access, CancellationToken cancellationToken) => throw Unasked();

        public Task RevokeAsync(Guid spaceId, Guid userId, CancellationToken cancellationToken) => throw Unasked();

        public Task<IReadOnlyList<HistoryEntry>> ListAsync(Guid issueId, CancellationToken cancellationToken) => throw Unasked();

        public Task<HistoryEntry?> LastAsync(Guid issueId, string field, CancellationToken cancellationToken) => throw Unasked();

        public Task<bool> AnyAsync(CancellationToken cancellationToken) => throw Unasked();

        public Task<User?> FindUserAsync(Guid id, CancellationToken cancellationToken) => throw Unasked();

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

    private sealed class StoppedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
