using Microsoft.EntityFrameworkCore;
using Npgsql;
using Planaffe.Application.Ports;
using Planaffe.Domain.Spaces;
using Planaffe.Infrastructure.Persistence;

namespace Planaffe.IntegrationTests;

/// <summary>
/// What the database holds about a page of the knowledge base and no substitute
/// could vouch for (<c>docs/storage.md</c>, Pages): that a slug is unique
/// under its parent and free under another, that the pages directly under a
/// space are held to the same rule although they have no parent, that the depth
/// is a constraint and not only a rule of the type, that a slug stays spent
/// while the page is deleted, and that a subtree leaves in one piece.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class PageSchemaTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Two_children_of_one_parent_cannot_share_a_slug()
    {
        await using var db = await Migrated.SeededAsync(postgres);
        var space = await SpaceAsync(db);
        var parent = Page.Create(space.Id, null, "company", "Company", null, db.User.Id, Migrated.Now);
        db.Context.Pages.Add(parent);
        await db.Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        db.Context.Pages.Add(Page.Create(space.Id, parent, "overview", "Overview", null, db.User.Id, Migrated.Now));
        await db.Context.SaveChangesAsync(TestContext.Current.CancellationToken);
        db.Context.ChangeTracker.Clear();

        var refusal = await Assert.ThrowsAsync<DbUpdateException>(async () =>
        {
            db.Context.Pages.Add(
                Page.Create(space.Id, parent, "overview", "Overview again", null, db.User.Id, Migrated.Now));
            await db.Context.SaveChangesAsync(TestContext.Current.CancellationToken);
        });

        Assert.Equal("page_slug", ((PostgresException)refusal.InnerException!).ConstraintName);
    }

    /// <summary>
    /// The reason the index says <c>nulls not distinct</c>: a page directly
    /// under the space carries no parent, and Postgres would otherwise count
    /// every one of those rows as distinct and let them all take one slug
    /// (ADR 0028).
    /// </summary>
    [Fact]
    public async Task Two_pages_directly_under_the_space_cannot_share_a_slug_either()
    {
        await using var db = await Migrated.SeededAsync(postgres);
        var space = await SpaceAsync(db);
        db.Context.Pages.Add(Page.Create(space.Id, null, "company", "Company", null, db.User.Id, Migrated.Now));
        await db.Context.SaveChangesAsync(TestContext.Current.CancellationToken);
        db.Context.ChangeTracker.Clear();

        var refusal = await Assert.ThrowsAsync<DbUpdateException>(async () =>
        {
            db.Context.Pages.Add(
                Page.Create(space.Id, null, "company", "Company again", null, db.User.Id, Migrated.Now));
            await db.Context.SaveChangesAsync(TestContext.Current.CancellationToken);
        });

        Assert.Equal("page_slug", ((PostgresException)refusal.InnerException!).ConstraintName);
    }

    [Fact]
    public async Task The_same_slug_under_two_parents_is_two_pages()
    {
        await using var db = await Migrated.SeededAsync(postgres);
        var space = await SpaceAsync(db);
        var company = Page.Create(space.Id, null, "company", "Company", null, db.User.Id, Migrated.Now);
        var product = Page.Create(space.Id, null, "product", "Product", null, db.User.Id, Migrated.Now);
        db.Context.Pages.AddRange(company, product);
        await db.Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        db.Context.Pages.AddRange(
            Page.Create(space.Id, company, "overview", "Company overview", null, db.User.Id, Migrated.Now),
            Page.Create(space.Id, product, "overview", "Product overview", null, db.User.Id, Migrated.Now));
        await db.Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await using var reader = db.Reader();
        Assert.Equal(2, await reader.Pages.CountAsync(p => p.Slug == "overview", TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// The type cannot be constructed at a fourth level, so the only way to ask
    /// the database is to write the row past it — which is the point of the
    /// constraint standing there as well.
    /// </summary>
    [Fact]
    public async Task A_fourth_level_is_refused_by_the_database_too()
    {
        await using var db = await Migrated.SeededAsync(postgres);
        var space = await SpaceAsync(db);
        var page = Page.Create(space.Id, null, "company", "Company", null, db.User.Id, Migrated.Now);
        db.Context.Pages.Add(page);
        await db.Context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var refusal = await Assert.ThrowsAsync<PostgresException>(() =>
            db.Context.Database.ExecuteSqlRawAsync(
                """
                insert into page (id, space_id, parent_id, depth, slug, title, body, created_by, created_at, updated_by, updated_at)
                values ({0}, {1}, {2}, 3, 'day-one', 'Day one', '', {3}, now(), {3}, now())
                """,
                [Guid.CreateVersion7(), space.Id, page.Id, db.User.Id],
                TestContext.Current.CancellationToken));

        Assert.Equal("ck_page_depth", refusal.ConstraintName);
    }

    [Fact]
    public async Task A_deleted_page_keeps_its_slug_and_the_purge_gives_it_back()
    {
        await using var db = await Migrated.SeededAsync(postgres);
        var space = await SpaceAsync(db);
        var grace = TimeSpan.FromDays(7);

        var page = Page.Create(space.Id, null, "company", "Company", null, db.User.Id, Migrated.Now);
        page.Delete(db.User.Id, DateTimeOffset.UtcNow - grace - TimeSpan.FromDays(1));
        db.Context.Pages.Add(page);
        await db.Context.SaveChangesAsync(TestContext.Current.CancellationToken);
        db.Context.ChangeTracker.Clear();

        var taken = await Assert.ThrowsAsync<DbUpdateException>(async () =>
        {
            db.Context.Pages.Add(
                Page.Create(space.Id, null, "company", "Company again", null, db.User.Id, Migrated.Now));
            await db.Context.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
        Assert.Equal("page_slug", ((PostgresException)taken.InnerException!).ConstraintName);
        db.Context.ChangeTracker.Clear();

        // The purge runs at the end of any transaction, and this table is swept
        // unconditionally because it hangs in no project.
        await using (var context = Migrated.ContextFor(db.ConnectionString))
        {
            var transactions = new Transactions(context, new InstanceSettings(TimeSpan.FromHours(4), grace));
            await transactions.RunAsync(async () =>
            {
                context.Pages.Add(
                    Page.Create(space.Id, null, "product", "Product", null, db.User.Id, Migrated.Now));
                await context.SaveChangesAsync(TestContext.Current.CancellationToken);
                return true;
            }, TestContext.Current.CancellationToken);
        }

        await using (var context = Migrated.ContextFor(db.ConnectionString))
        {
            context.Pages.Add(
                Page.Create(space.Id, null, "company", "Company again", null, db.User.Id, Migrated.Now));
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var reader = db.Reader();
        var remaining = await reader.Pages.SingleAsync(p => p.Slug == "company", TestContext.Current.CancellationToken);
        Assert.Equal("Company again", remaining.Title);
    }

    /// <summary>
    /// Two cascades, and the tree depends on both: a purged page takes its
    /// descendants, and a purged space takes the whole tree.
    /// </summary>
    [Fact]
    public async Task A_subtree_leaves_with_its_root_and_a_tree_with_its_space()
    {
        await using var db = await Migrated.SeededAsync(postgres);
        var space = await SpaceAsync(db);
        var company = Page.Create(space.Id, null, "company", "Company", null, db.User.Id, Migrated.Now);
        var handbook = Page.Create(space.Id, company, "handbook", "Handbook", null, db.User.Id, Migrated.Now);
        var day = Page.Create(space.Id, handbook, "day-one", "Day one", null, db.User.Id, Migrated.Now);
        var product = Page.Create(space.Id, null, "product", "Product", null, db.User.Id, Migrated.Now);
        db.Context.Pages.AddRange(company, handbook, day, product);
        await db.Context.SaveChangesAsync(TestContext.Current.CancellationToken);
        db.Context.ChangeTracker.Clear();

        await db.Context.Database.ExecuteSqlRawAsync(
            "delete from page where id = {0}", [company.Id], TestContext.Current.CancellationToken);

        await using (var reader = db.Reader())
        {
            Assert.Equal(
                ["product"],
                await reader.Pages.Select(p => p.Slug).ToListAsync(TestContext.Current.CancellationToken));
        }

        await db.Context.Database.ExecuteSqlRawAsync(
            "delete from space where id = {0}", [space.Id], TestContext.Current.CancellationToken);

        await using var after = db.Reader();
        Assert.Empty(await after.Pages.ToListAsync(TestContext.Current.CancellationToken));
    }

    private static async Task<Space> SpaceAsync(Migrated db)
    {
        var space = Space.Create("handbuch", "Handbuch", db.User.Id, Migrated.Now);
        db.Context.Spaces.Add(space);
        db.Context.SpaceAccesses.Add(SpaceAccess.Grant(space.Id, db.User.Id, db.User.Id, Migrated.Now));
        await db.Context.SaveChangesAsync(TestContext.Current.CancellationToken);
        db.Context.ChangeTracker.Clear();
        return space;
    }
}
