using Microsoft.EntityFrameworkCore;
using Planaffe.Domain.Spaces;
using Planaffe.Infrastructure.Persistence;

namespace Planaffe.IntegrationTests;

/// <summary>
/// The four statements a subtree is found and written with. They are recursive
/// SQL rather than LINQ, so a substitute vouches for none of them: this is
/// where they are asked whether they take the whole tree, only the tree, and
/// nothing that was already away.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class SpacePageStoreTests(PostgresFixture postgres)
{
    [Fact]
    public async Task The_descendants_are_the_whole_subtree_and_only_the_live_part_of_it()
    {
        await using var db = await Migrated.SeededAsync(postgres);
        var tree = await TreeAsync(db);
        var pages = new SpacePages(db.Context);

        Assert.Equal(
            ["handbook", "day-one"],
            (await pages.DescendantsAsync(tree.Company.Id, TestContext.Current.CancellationToken)).Select(p => p.Slug));

        Assert.Empty(await pages.DescendantsAsync(tree.Product.Id, TestContext.Current.CancellationToken));

        await db.Context.Database.ExecuteSqlRawAsync(
            "update space_page set deleted_at = now(), deleted_by = {0} where id = {1}",
            [db.User.Id, tree.Handbook.Id],
            TestContext.Current.CancellationToken);

        // The deleted page is gone from the walk, and its child with it: a
        // subtree hangs together, and the recursion stops where the tree does.
        Assert.Empty(await pages.DescendantsAsync(tree.Company.Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Deleting_stamps_the_subtree_and_restoring_brings_back_exactly_it()
    {
        await using var db = await Migrated.SeededAsync(postgres);
        var tree = await TreeAsync(db);
        var pages = new SpacePages(db.Context);
        var now = DateTimeOffset.UtcNow;

        // One page went on its own beforehand, so it must stay behind when the
        // subtree comes back.
        await db.Context.Database.ExecuteSqlRawAsync(
            "update space_page set deleted_at = now(), deleted_by = {0} where id = {1}",
            [db.User.Id, tree.DayOne.Id],
            TestContext.Current.CancellationToken);

        await pages.DeleteDescendantsAsync(tree.Company.Id, db.User.Id, now, TestContext.Current.CancellationToken);

        await using (var reader = db.Reader())
        {
            var handbook = await reader.SpacePages.SingleAsync(p => p.Id == tree.Handbook.Id, TestContext.Current.CancellationToken);
            Assert.True(handbook.Deleted);
            Assert.Equal(tree.Company.Id, handbook.DeletedWith);

            var dayOne = await reader.SpacePages.SingleAsync(p => p.Id == tree.DayOne.Id, TestContext.Current.CancellationToken);
            Assert.Null(dayOne.DeletedWith);

            var product = await reader.SpacePages.SingleAsync(p => p.Id == tree.Product.Id, TestContext.Current.CancellationToken);
            Assert.False(product.Deleted);
        }

        Assert.Equal(
            [tree.Handbook.Id],
            (await pages.CompanionsAsync(tree.Company.Id, TestContext.Current.CancellationToken)).Select(p => p.Id));

        await pages.RestoreCompanionsAsync(tree.Company.Id, TestContext.Current.CancellationToken);

        await using var after = db.Reader();
        Assert.False((await after.SpacePages.SingleAsync(p => p.Id == tree.Handbook.Id, TestContext.Current.CancellationToken)).Deleted);
        Assert.True((await after.SpacePages.SingleAsync(p => p.Id == tree.DayOne.Id, TestContext.Current.CancellationToken)).Deleted);
    }

    [Fact]
    public async Task Shifting_carries_the_subtree_into_the_new_space_and_the_new_depth()
    {
        await using var db = await Migrated.SeededAsync(postgres);
        var tree = await TreeAsync(db);
        var elsewhere = Space.Create("personal", "Personal", db.User.Id, Migrated.Now);
        db.Context.Spaces.Add(elsewhere);
        await db.Context.SaveChangesAsync(TestContext.Current.CancellationToken);
        db.Context.ChangeTracker.Clear();

        await new SpacePages(db.Context)
            .ShiftDescendantsAsync(tree.Company.Id, elsewhere.Id, 0, TestContext.Current.CancellationToken);

        await using var reader = db.Reader();
        var moved = await reader.SpacePages
            .Where(p => p.SpaceId == elsewhere.Id)
            .OrderBy(p => p.Depth)
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(["handbook", "day-one"], moved.Select(p => p.Slug));
        Assert.Equal([1, 2], moved.Select(p => p.Depth));
    }

    /// <summary>
    /// Two roots, and under one of them two levels: company → handbook →
    /// day-one, and product beside it.
    /// </summary>
    private static async Task<(SpacePage Company, SpacePage Handbook, SpacePage DayOne, SpacePage Product)> TreeAsync(Migrated db)
    {
        var space = Space.Create("handbuch", "Handbuch", db.User.Id, Migrated.Now);
        db.Context.Spaces.Add(space);
        db.Context.SpaceAccesses.Add(SpaceAccess.Grant(space.Id, db.User.Id, db.User.Id, Migrated.Now));

        var company = SpacePage.Create(space.Id, null, "company", "Company", null, db.User.Id, Migrated.Now);
        var handbook = SpacePage.Create(space.Id, company, "handbook", "Handbook", null, db.User.Id, Migrated.Now);
        var dayOne = SpacePage.Create(space.Id, handbook, "day-one", "Day one", null, db.User.Id, Migrated.Now);
        var product = SpacePage.Create(space.Id, null, "product", "Product", null, db.User.Id, Migrated.Now);

        db.Context.SpacePages.AddRange(company, handbook, dayOne, product);
        await db.Context.SaveChangesAsync(TestContext.Current.CancellationToken);
        db.Context.ChangeTracker.Clear();

        return (company, handbook, dayOne, product);
    }
}
