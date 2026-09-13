using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Npgsql;

namespace Planaffe.IntegrationTests;

/// <summary>
/// The migrations that carry data rather than only shape. <see cref="SchemaTests"/>
/// proves they apply; these prove that what was in the database before one of
/// them is still findable after it — which is the only thing an installation
/// upgrading into a withdrawal cares about.
/// </summary>
/// <remarks>
/// Each test migrates to the migration before the one it is about, writes rows
/// the way that older schema held them, and then lets the rest run. The rows go
/// in as SQL on purpose: the model in this assembly is today's, and the whole
/// question is what happens to a row shaped like yesterday's.
/// </remarks>
[Collection(nameof(PostgresCollection))]
public sealed class MigrationTests(PostgresFixture postgres)
{
    private const string BeforeTheField = "20260912215911_AddSpacePageSearch";

    private const string BeforeTheWithdrawal = "20260913061805_InstructionsBecomeAField";

    /// <summary>
    /// The designated page's body becomes the project's instructions
    /// (VISION 18). A project pointing at nothing, and one pointing at a page
    /// that is deleted, come out carrying none — a deleted page delivered
    /// nothing while it was designated either.
    /// </summary>
    [Fact]
    public async Task Instructions_move_out_of_the_page_they_were_written_in()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        await MigrateToAsync(connectionString, BeforeTheField);

        var user = Guid.CreateVersion7();
        var (written, pointing, none, gone) = (Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7());

        await ExecuteAsync(connectionString,
            $"""
             insert into identity (id, kind, name, administrator, created_at, email, normalized_email, user_state)
                  values ('{user}', 'user', 'maintainer', true, now(), 'maintainer@example.test', 'maintainer@example.test', 'active');

             insert into project (id, key, name, created_by, created_at, updated_at)
                  values ('{pointing}', 'PLAN', 'planaffe', '{user}', now(), now()),
                         ('{none}', 'DOCS', 'the docs', '{user}', now(), now()),
                         ('{gone}', 'OLD', 'the old one', '{user}', now(), now());

             insert into page (id, project_id, slug, title, body, created_by, created_at, updated_by, updated_at)
                  values ('{written}', '{pointing}', 'agents', 'How work runs here',
                          'Tests run with `just test`.', '{user}', now(), '{user}', now());

             insert into page (id, project_id, slug, title, body, created_by, created_at, updated_by, updated_at, deleted_at, deleted_by)
                  values ('{Guid.CreateVersion7()}', '{gone}', 'agents', 'Withdrawn',
                          'Nobody reads this.', '{user}', now(), '{user}', now(), now(), '{user}');

             update project set instructions_page_id = '{written}' where id = '{pointing}';
             update project set instructions_page_id = (select id from page where project_id = '{gone}') where id = '{gone}';
             """);

        await MigrateToAsync(connectionString, BeforeTheWithdrawal);

        await using var context = Migrated.ContextFor(connectionString);
        var projects = await context.Projects.OrderBy(p => p.Key).ToDictionaryAsync(p => p.Key, p => p.Instructions, TestContext.Current.CancellationToken);

        Assert.Equal("Tests run with `just test`.", projects["PLAN"]);
        Assert.Null(projects["DOCS"]);
        Assert.Null(projects["OLD"]);

        // The page it was written in is still there. It leaves with the rest of
        // the project's wiki, not with its designation.
        Assert.Equal(1L, await ScalarAsync(connectionString, $"select count(*) from page where id = '{written}'"));
    }

    /// <summary>
    /// The way across (VISION 18): every project with pages gets a space, its
    /// access is carried over, the pages hang directly under it and their
    /// history comes with them. The page the instructions were taken out of is
    /// the one that stays behind — its text is on the project now.
    /// </summary>
    [Fact]
    public async Task Project_pages_are_carried_into_a_space_before_the_table_goes()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        await MigrateToAsync(connectionString, BeforeTheWithdrawal);

        var user = Guid.CreateVersion7();
        var (withPages, withoutPages) = (Guid.CreateVersion7(), Guid.CreateVersion7());
        var (architecture, deleted, brief) = (Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7());

        await ExecuteAsync(connectionString,
            $"""
             insert into identity (id, kind, name, administrator, created_at, email, normalized_email, user_state)
                  values ('{user}', 'user', 'maintainer', true, now(), 'maintainer@example.test', 'maintainer@example.test', 'active');

             insert into project (id, key, name, instructions, created_by, created_at, updated_at)
                  values ('{withPages}', 'PLAN', 'planaffe', 'Tests run with `just test`.', '{user}', now(), now()),
                         ('{withoutPages}', 'DOCS', 'the docs', null, '{user}', now(), now());

             insert into project_access (project_id, user_id, granted_by, granted_at)
                  values ('{withPages}', '{user}', '{user}', now());

             -- A space already called `plan`, so the project's own has to count up.
             insert into space (id, name, title, closed_to_agents, created_by, created_at, updated_at)
                  values ('{Guid.CreateVersion7()}', 'plan', 'Somebody was here first', false, '{user}', now(), now());

             insert into page (id, project_id, slug, title, body, created_by, created_at, updated_by, updated_at)
                  values ('{architecture}', '{withPages}', 'architecture', 'Architecture', 'Four layers.', '{user}', now(), '{user}', now()),
                         ('{brief}', '{withPages}', 'agents', 'How work runs here', 'Tests run with `just test`.', '{user}', now(), '{user}', now());

             insert into page (id, project_id, slug, title, body, created_by, created_at, updated_by, updated_at, deleted_at, deleted_by)
                  values ('{deleted}', '{withPages}', 'old', 'The old plan', 'Superseded.', '{user}', now(), '{user}', now(), now(), '{user}');

             insert into history (page_id, actor_id, at, field)
                  values ('{architecture}', '{user}', now(), 'created'),
                         ('{brief}', '{user}', now(), 'created');
             """);

        await MigrateToAsync(connectionString, target: null);

        await using var context = Migrated.ContextFor(connectionString);

        // One space, named `plan2` because `plan` was taken, titled after the
        // project, open to agents and granted to whoever had the project.
        var space = Assert.Single(await context.Spaces.Where(s => s.Name != "plan").ToListAsync(TestContext.Current.CancellationToken));
        Assert.Equal("plan2", space.Name);
        Assert.Equal("planaffe", space.Title);
        Assert.False(space.ClosedToAgents);
        Assert.Equal(1L, await ScalarAsync(connectionString, $"select count(*) from space_access where space_id = '{space.Id}' and user_id = '{user}'"));

        // The pages, directly under it and keeping their ids — the deleted one
        // among them, still deleted, because a migration does not end a grace
        // period on the quiet. The page the instructions came out of stayed
        // behind and went with the table.
        var pages = await context.Pages.Where(p => p.SpaceId == space.Id).ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(["architecture", "old"], pages.Select(p => p.Slug).Order(StringComparer.Ordinal));
        Assert.All(pages, page => { Assert.Null(page.ParentId); Assert.Equal(0, page.Depth); });
        Assert.NotNull(pages.Single(p => p.Slug == "old").DeletedAt);
        Assert.Equal("Four layers.", pages.Single(p => p.Slug == "architecture").Body);
        Assert.DoesNotContain(brief, pages.Select(p => p.Id));

        // The history followed its page; the instructions page's went with it.
        Assert.Equal(1L, await ScalarAsync(connectionString, $"select count(*) from history where page_id = '{architecture}'"));
        Assert.Equal(0L, await ScalarAsync(connectionString, $"select count(*) from history where page_id = '{brief}'"));

        // A project without pages is given no space at all.
        Assert.Equal(0L, await ScalarAsync(connectionString, "select count(*) from space where title = 'the docs'"));

        // And the text the instructions were carried into is untouched.
        var project = await context.Projects.SingleAsync(p => p.Key == "PLAN", TestContext.Current.CancellationToken);
        Assert.Equal("Tests run with `just test`.", project.Instructions);
    }

    private static async Task MigrateToAsync(string connectionString, string? target)
    {
        await using var context = Migrated.ContextFor(connectionString);
        await context.GetService<IMigrator>().MigrateAsync(target, TestContext.Current.CancellationToken);
    }

    private static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlDataSourceBuilder(connectionString).Build();
        await using var command = connection.CreateCommand(sql);
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<long> ScalarAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlDataSourceBuilder(connectionString).Build();
        await using var command = connection.CreateCommand(sql);
        return (long)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
    }
}
