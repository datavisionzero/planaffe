using Microsoft.EntityFrameworkCore;
using Planaffe.Application.Acts;
using Planaffe.Application.Ports;
using Planaffe.Domain.Spaces;
using Planaffe.Infrastructure.Persistence;

namespace Planaffe.IntegrationTests;

/// <summary>
/// The one statement the search over the knowledge base is (VISION 18). No
/// substitute vouches for any of it: the generated column, the <c>simple</c>
/// configuration that lets an identifier through, the rank that orders, the
/// excerpt Postgres cuts, and the walk that says where a page stands.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class SpacePageSearchTests(PostgresFixture postgres)
{
    [Fact]
    public async Task A_page_is_found_by_its_title_and_by_its_body()
    {
        await using var db = await Migrated.SeededAsync(postgres);
        var world = await WorldAsync(db);
        var pages = new SpacePages(db.Context);

        var byTitle = await pages.SearchAsync([world.Handbook.Id], "erster tag", 20, TestContext.Current.CancellationToken);
        var byBody = await pages.SearchAsync([world.Handbook.Id], "Zugangskarte", 20, TestContext.Current.CancellationToken);

        Assert.Equal("onboarding/erster-tag", Assert.Single(byTitle).Path);
        Assert.Equal("onboarding/erster-tag", Assert.Single(byBody).Path);
    }

    /// <summary>
    /// The hit says where the page stands, because a search across several
    /// spaces has no tree to ask.
    /// </summary>
    [Fact]
    public async Task A_hit_carries_the_space_and_the_way_down_to_it()
    {
        await using var db = await Migrated.SeededAsync(postgres);
        var world = await WorldAsync(db);

        var hit = Assert.Single(await new SpacePages(db.Context)
            .SearchAsync([world.Handbook.Id], "Zugangskarte", 20, TestContext.Current.CancellationToken));

        Assert.Equal("handbuch", hit.Space);
        Assert.Equal("Handbuch", hit.SpaceTitle);
        Assert.Equal("Erster Tag", hit.Title);
        Assert.Equal(["onboarding"], hit.TrailPaths);
        Assert.Equal(["Onboarding"], hit.TrailTitles);
    }

    /// <summary>
    /// The excerpt comes marked with the two control characters and with
    /// nothing else: no markup leaves the instance (ADR 0007).
    /// </summary>
    [Fact]
    public async Task The_excerpt_marks_what_matched_and_carries_no_markup()
    {
        await using var db = await Migrated.SeededAsync(postgres);
        var world = await WorldAsync(db);

        var hit = Assert.Single(await new SpacePages(db.Context)
            .SearchAsync([world.Handbook.Id], "Zugangskarte", 20, TestContext.Current.CancellationToken));

        Assert.DoesNotContain("<", hit.Headline, StringComparison.Ordinal);
        Assert.Contains($"{Excerpt.Start}", hit.Headline, StringComparison.Ordinal);

        var matched = Excerpts.Of(hit.Headline).Where(piece => piece.Hit).Select(piece => piece.Text);
        Assert.Equal(["Zugangskarte"], matched);
    }

    /// <summary>
    /// <c>simple</c> and not <c>english</c>, for the reason the whole product
    /// has it: these texts are full of identifiers, and stemming mangles them.
    /// </summary>
    [Fact]
    public async Task An_identifier_survives_the_analysis()
    {
        await using var db = await Migrated.SeededAsync(postgres);
        var world = await WorldAsync(db);

        var hits = await new SpacePages(db.Context)
            .SearchAsync([world.Handbook.Id], "claim-held", 20, TestContext.Current.CancellationToken);

        Assert.Equal("betrieb", Assert.Single(hits).Path);
    }

    [Fact]
    public async Task A_deleted_page_is_no_hit()
    {
        await using var db = await Migrated.SeededAsync(postgres);
        var world = await WorldAsync(db);
        await db.Context.Database.ExecuteSqlRawAsync(
            "update space_page set deleted_at = now(), deleted_by = {0} where id = {1}",
            [db.User.Id, world.DayOne.Id],
            TestContext.Current.CancellationToken);

        Assert.Empty(await new SpacePages(db.Context)
            .SearchAsync([world.Handbook.Id], "Zugangskarte", 20, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// The whole of the access rule at this level: what is not in the set is
    /// not searched, so a space closed to an agent is missing from the rows
    /// and from their number alike (ADR 0027).
    /// </summary>
    [Fact]
    public async Task Only_the_spaces_asked_for_are_searched()
    {
        await using var db = await Migrated.SeededAsync(postgres);
        var world = await WorldAsync(db);
        var pages = new SpacePages(db.Context);

        Assert.Equal(
            ["handbuch", "personal"],
            (await pages.SearchAsync(
                [world.Handbook.Id, world.Personal.Id], "vertraulich", 20, TestContext.Current.CancellationToken))
                .Select(hit => hit.Space)
                .Order());

        Assert.Equal(
            ["handbuch"],
            (await pages.SearchAsync([world.Handbook.Id], "vertraulich", 20, TestContext.Current.CancellationToken))
                .Select(hit => hit.Space));

        Assert.Empty(await pages.SearchAsync([], "vertraulich", 20, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// The one read in the knowledge base that ranks: the page that carries
    /// the words twice stands above the one that carries them once.
    /// </summary>
    [Fact]
    public async Task The_better_hit_comes_first_and_the_limit_holds()
    {
        await using var db = await Migrated.SeededAsync(postgres);
        var world = await WorldAsync(db);
        var pages = new SpacePages(db.Context);

        var hits = await pages.SearchAsync([world.Handbook.Id], "urlaub", 20, TestContext.Current.CancellationToken);

        Assert.Equal(["urlaub", "betrieb"], hits.Select(hit => hit.Path));
        Assert.Single(await pages.SearchAsync([world.Handbook.Id], "urlaub", 1, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Two spaces: a handbook with a tree two levels deep, and a second one
    /// holding a page that must not turn up in the first one's search.
    /// </summary>
    private static async Task<(Space Handbook, Space Personal, SpacePage DayOne)> WorldAsync(Migrated db)
    {
        var handbook = Space.Create("handbuch", "Handbuch", db.User.Id, Migrated.Now);
        var personal = Space.Create("personal", "Personal", db.User.Id, Migrated.Now);
        db.Context.Spaces.AddRange(handbook, personal);
        db.Context.SpaceAccesses.Add(SpaceAccess.Grant(handbook.Id, db.User.Id, db.User.Id, Migrated.Now));
        db.Context.SpaceAccesses.Add(SpaceAccess.Grant(personal.Id, db.User.Id, db.User.Id, Migrated.Now));

        var onboarding = SpacePage.Create(
            handbook.Id, null, "onboarding", "Onboarding", "Wie eine neue Person ankommt.", db.User.Id, Migrated.Now);
        var dayOne = SpacePage.Create(
            handbook.Id,
            onboarding,
            "erster-tag",
            "Erster Tag",
            "Am ersten Tag bekommt jede neue Person eine Zugangskarte und einen Paten.",
            db.User.Id,
            Migrated.Now);
        var operations = SpacePage.Create(
            handbook.Id,
            null,
            "betrieb",
            "Betrieb",
            "Ein Ticket antwortet claim-held, wenn es jemand anderes hält. Urlaub wird im Kalender eingetragen. Vertraulich ist nichts davon.",
            db.User.Id,
            Migrated.Now);
        var holidays = SpacePage.Create(
            handbook.Id, null, "urlaub", "Urlaub", "Urlaub wird beantragt, Urlaub wird genehmigt.", db.User.Id, Migrated.Now);
        var contract = SpacePage.Create(
            personal.Id, null, "vertrag", "Vertrag", "Vertraulich, und deshalb hier.", db.User.Id, Migrated.Now);

        db.Context.SpacePages.AddRange(onboarding, dayOne, operations, holidays, contract);
        await db.Context.SaveChangesAsync(TestContext.Current.CancellationToken);
        db.Context.ChangeTracker.Clear();

        return (handbook, personal, dayOne);
    }
}
