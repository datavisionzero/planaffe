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
    private const string Before = "20260912215911_AddSpacePageSearch";

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
        await MigrateToAsync(connectionString, Before);

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

        await MigrateToAsync(connectionString, target: null);

        await using var context = Migrated.ContextFor(connectionString);
        var projects = await context.Projects.OrderBy(p => p.Key).ToDictionaryAsync(p => p.Key, p => p.Instructions, TestContext.Current.CancellationToken);

        Assert.Equal("Tests run with `just test`.", projects["PLAN"]);
        Assert.Null(projects["DOCS"]);
        Assert.Null(projects["OLD"]);

        // The page it was written in is still there. It leaves with the rest of
        // the project's wiki, not with its designation.
        Assert.Equal(1L, await ScalarAsync(connectionString, $"select count(*) from page where id = '{written}'"));
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
