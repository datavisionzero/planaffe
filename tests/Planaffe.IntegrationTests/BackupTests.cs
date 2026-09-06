using System.Net;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Json;
using System.Text.Json;

namespace Planaffe.IntegrationTests;

/// <summary>
/// The way back out of a backup, walked rather than described.
/// </summary>
/// <remarks>
/// <para>
/// <c>docs/operations.md</c> says the database is everything and that
/// <c>pg_dump</c> of it is the whole of operations, and VISION 16 promises
/// exactly that: operations consist of Postgres backups and nothing else. What
/// stood beside that promise was a <c>pg_dump</c> with no <c>psql</c> behind
/// it — the one operation the product asks of an operator, and the only one
/// that would otherwise be carried out for the first time on the day it
/// matters.
/// </para>
/// <para>
/// So the way back is proved here: a dump is taken of an instance with content
/// in it, put into an empty database, and an instance is started on that and
/// asked for what was there before. A test says it at every change of the
/// schema, which is more than a script can do that nobody runs beforehand.
/// </para>
/// <para>
/// The restore is deliberately the plain <c>psql</c> of the documentation and
/// not a <c>pg_restore</c> with switches: what is proved has to be what an
/// operator would type.
/// </para>
/// </remarks>
[Collection(nameof(PostgresCollection))]
public sealed class BackupTests(PostgresFixture postgres)
{
    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_dump_restored_into_an_empty_database_carries_the_instance_back()
    {
        await using var instance = await AnInstance.BootstrappedAsync(postgres);
        using (var admin = instance.ClientWith(AnInstance.BootstrapToken))
        {
            using var project = await admin.PostAsJsonAsync("/projects", new { key = "PLAN", name = "planaffe" }, Ct);
            Assert.Equal(HttpStatusCode.Created, project.StatusCode);

            using var issue = await admin.PostAsJsonAsync("/issues", new { project = "PLAN", issues = new[] { new { title = "Prove the way back", description = "A dump nobody restored is a promise." } } }, Ct);
            Assert.Equal(HttpStatusCode.Created, issue.StatusCode);
        }

        // The backup, taken the way `docs/operations.md` takes it.
        var source = PostgresFixture.DatabaseOf(instance.ConnectionString);
        await postgres.RunAsync($"pg_dump --dbname={source} --file=/tmp/{source}.sql");

        // And the half that was missing: an empty database, and the dump into
        // it. `--set ON_ERROR_STOP=1` is not decoration — psql otherwise walks
        // past a failed statement and exits 0 on a restore that did not happen.
        var restored = await postgres.CreateDatabaseAsync();
        var target = PostgresFixture.DatabaseOf(restored);
        await postgres.RunAsync($"psql --set=ON_ERROR_STOP=1 --dbname={target} --file=/tmp/{source}.sql");

        // An instance on the restored database, started with no bootstrap
        // variables at all: whoever is in there came out of the dump, and a
        // second administrator invented at start-up would hide a restore that
        // brought back nothing.
        await using var recovered = new AnInstance(restored, administrator: null, token: null);

        using var client = recovered.ClientWith(AnInstance.BootstrapToken);

        var me = await client.GetFromJsonAsync<JsonElement>("/me", Ct);
        Assert.Equal(AnInstance.Administrator, me.GetProperty("name").GetString());

        var issues = await client.GetFromJsonAsync<JsonElement>("/issues?project=PLAN", Ct);
        var only = Assert.Single(issues.GetProperty("items").EnumerateArray());
        Assert.Equal("PLAN-1", only.GetProperty("key").GetString());
        Assert.Equal("Prove the way back", only.GetProperty("title").GetString());

        // The next key carries on where the dump left off. A sequence restored
        // without its value is the failure this test exists for: the instance
        // comes up, reads fine, and collides on the first write.
        using var next = await client.PostAsJsonAsync("/issues", new { project = "PLAN", issues = new[] { new { title = "The one after" } } }, Ct);
        Assert.Equal(HttpStatusCode.Created, next.StatusCode);
        var written = await next.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal("PLAN-2", Assert.Single(written.GetProperty("items").EnumerateArray()).GetProperty("key").GetString());
    }

    /// <summary>
    /// Migrations only run forward (ADR 0011), and what that means for a dump
    /// is that the version restoring it may be newer than the one that took it:
    /// the next start migrates, and nobody has to do anything. The other
    /// direction has no answer at all, which is why the documentation says to
    /// keep the dump beside the version it came from.
    /// </summary>
    [Fact]
    public async Task A_restored_dump_is_migrated_by_the_start_that_follows_it()
    {
        await using var instance = await AnInstance.BootstrappedAsync(postgres);
        using (instance.CreateClient())
        {
            // Started: the schema is applied and the first administrator is in.
        }

        var source = PostgresFixture.DatabaseOf(instance.ConnectionString);
        await postgres.RunAsync($"pg_dump --dbname={source} --file=/tmp/{source}-migrated.sql");

        var restored = await postgres.CreateDatabaseAsync();
        var target = PostgresFixture.DatabaseOf(restored);
        await postgres.RunAsync($"psql --set=ON_ERROR_STOP=1 --dbname={target} --file=/tmp/{source}-migrated.sql");

        // The migration history rides along in the dump, so the start that
        // follows finds nothing left to apply rather than trying to apply it
        // all a second time.
        await using var context = Migrated.ContextFor(restored);
        Assert.NotEmpty(await context.Database.GetAppliedMigrationsAsync(Ct));
        Assert.Empty(await context.Database.GetPendingMigrationsAsync(Ct));
    }
}
