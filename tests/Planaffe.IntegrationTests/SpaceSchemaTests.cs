using Microsoft.EntityFrameworkCore;
using Npgsql;
using Planaffe.Application.Ports;
using Planaffe.Domain.Spaces;
using Planaffe.Infrastructure.Persistence;

namespace Planaffe.IntegrationTests;

/// <summary>
/// What the database holds about a space and no substitute could vouch for
/// (<c>docs/storage.md</c>, Spaces): the name is unique across the instance
/// even when two creators race for it, it stays spent while the space is
/// deleted, the purge is what gives it back, and access dies with the space it
/// was granted on.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class SpaceSchemaTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Concurrent_creators_of_one_name_produce_exactly_one_space()
    {
        await using var db = await Migrated.SeededAsync(postgres);

        var outcomes = await Task.WhenAll(Enumerable.Range(0, 20).Select(async _ =>
        {
            await using var context = Migrated.ContextFor(db.ConnectionString);
            try
            {
                context.Spaces.Add(Space.Create("handbuch", "Handbuch", db.User.Id, Migrated.Now));
                await context.SaveChangesAsync(TestContext.Current.CancellationToken);
                return true;
            }
            catch (DbUpdateException exception) when (
                exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                return false;
            }
        }));

        Assert.Equal(1, outcomes.Count(won => won));

        await using var reader = db.Reader();
        Assert.Equal(1, await reader.Spaces.CountAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Unlike a page's slug, a space's name is unique across the instance:
    /// nothing brackets a space, so there is no second scope it could be
    /// unique in (ADR 0027).
    /// </summary>
    [Fact]
    public async Task A_deleted_space_keeps_its_name_until_the_purge()
    {
        await using var db = await Migrated.SeededAsync(postgres);
        var space = Space.Create("handbuch", "Handbuch", db.User.Id, Migrated.Now);
        space.Delete(db.User.Id, Migrated.Now);
        db.Context.Spaces.Add(space);
        await db.Context.SaveChangesAsync(TestContext.Current.CancellationToken);
        db.Context.ChangeTracker.Clear();

        db.Context.Spaces.Add(Space.Create("handbuch", "Handbuch again", db.User.Id, Migrated.Now));

        var refusal = await Assert.ThrowsAsync<DbUpdateException>(() =>
            db.Context.SaveChangesAsync(TestContext.Current.CancellationToken));

        Assert.Equal("space_name", ((PostgresException)refusal.InnerException!).ConstraintName);
    }

    /// <summary>
    /// A space hangs in no project, so no project's write sweeps it. The purge
    /// takes it on the next write anywhere, which is what this proves.
    /// </summary>
    [Fact]
    public async Task The_purge_gives_the_name_back()
    {
        await using var db = await Migrated.SeededAsync(postgres);
        var grace = TimeSpan.FromDays(7);

        var space = Space.Create("handbuch", "Handbuch", db.User.Id, Migrated.Now);
        space.Delete(db.User.Id, DateTimeOffset.UtcNow - grace - TimeSpan.FromDays(1));
        db.Context.Spaces.Add(space);
        await db.Context.SaveChangesAsync(TestContext.Current.CancellationToken);
        db.Context.ChangeTracker.Clear();

        await using (var context = Migrated.ContextFor(db.ConnectionString))
        {
            var transactions = new Transactions(context, new InstanceSettings(TimeSpan.FromHours(4), grace));
            await transactions.RunAsync(async () =>
            {
                context.Spaces.Add(Space.Create("personal", "Personal", db.User.Id, Migrated.Now));
                await context.SaveChangesAsync(TestContext.Current.CancellationToken);
                return true;
            }, TestContext.Current.CancellationToken);
        }

        await using (var context = Migrated.ContextFor(db.ConnectionString))
        {
            context.Spaces.Add(Space.Create("handbuch", "Handbuch again", db.User.Id, Migrated.Now));
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var reader = db.Reader();
        var remaining = await reader.Spaces.SingleAsync(s => s.Name == "handbuch", TestContext.Current.CancellationToken);
        Assert.Equal("Handbuch again", remaining.Title);
    }

    [Fact]
    public async Task Access_is_one_row_per_user_and_dies_with_the_space()
    {
        await using var db = await Migrated.SeededAsync(postgres);
        var space = Space.Create("handbuch", "Handbuch", db.User.Id, Migrated.Now);
        db.Context.Spaces.Add(space);
        db.Context.SpaceAccesses.Add(SpaceAccess.Grant(space.Id, db.User.Id, db.User.Id, Migrated.Now));
        await db.Context.SaveChangesAsync(TestContext.Current.CancellationToken);
        db.Context.ChangeTracker.Clear();

        var second = await Assert.ThrowsAsync<PostgresException>(() =>
            db.Context.Database.ExecuteSqlRawAsync(
                "insert into space_access (space_id, user_id, granted_by, granted_at) values ({0}, {1}, {2}, now())",
                [space.Id, db.User.Id, db.User.Id],
                TestContext.Current.CancellationToken));

        Assert.Equal("pk_space_access", second.ConstraintName);

        await db.Context.Database.ExecuteSqlRawAsync(
            "delete from space where id = {0}", [space.Id], TestContext.Current.CancellationToken);

        await using var reader = db.Reader();
        Assert.Empty(await reader.SpaceAccesses.ToListAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>The space is nobody's content: no column of it names a project.</summary>
    [Fact]
    public async Task A_space_hangs_in_no_project()
    {
        await using var db = await Migrated.SeededAsync(postgres);

        var columns = await db.Context.Database
            .SqlQueryRaw<string>(
                "select column_name as \"Value\" from information_schema.columns where table_name = 'space'")
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.DoesNotContain("project_id", columns);
    }
}
