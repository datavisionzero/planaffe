using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Planaffe.Domain.Epics;

namespace Planaffe.IntegrationTests;

/// <summary>
/// The issue without its acts (<c>docs/api.md</c>): the transactional bulk
/// create, the two shapes, the cursor that holds under a concurrent writer, the
/// guarded change, and the edges.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class IssueEndpointTests(PostgresFixture postgres)
{
    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Seven_wired_up_issues_round_trip_as_seven_complete_issues()
    {
        await using var instance = await AnInstance.BootstrappedAsync(postgres);
        using var admin = await Ready(instance);
        await admin.PostAsJsonAsync("/projects/PLAN/labels", new { name = "cut-1" }, Ct);

        // An epic, closed, to be reopened by the attachment (VISION 7).
        await using (var context = Migrated.ContextFor(instance.ConnectionString))
        {
            var project = await context.Projects.SingleAsync(Ct);
            var author = await context.Users.SingleAsync(Ct);
            var epic = Epic.Create(project.Id, 2, "Backend and data model", author.Id, Migrated.Now);
            context.Epics.Add(epic);
            await context.SaveChangesAsync(Ct);
            await context.Database.ExecuteSqlRawAsync("update epic set status = 'closed', closed_at = now() where id = {0}", [epic.Id], Ct);
        }

        var body = new
        {
            project = "PLAN",
            issues = new object[]
            {
                new { @ref = "schema", title = "The schema", description = "Every table.", priority = 3, ready = true, labels = new[] { "feature", "cut-1" }, epic = "PLAN-E2" },
                new { @ref = "identity", title = "Identity and bootstrap", blocked_by = new[] { "schema" } },
                new { @ref = "contract", title = "The contract", blocked_by = new[] { "identity" } },
                new { @ref = "users", title = "Users, agents, tokens", blocked_by = new[] { "identity" }, assignee = "maintainer" },
                new { @ref = "projects", title = "Projects and labels", blocked_by = new[] { "identity" } },
                new { @ref = "issues", title = "Creating and reading issues", blocked_by = new[] { "projects" }, blocks = new[] { "switch" } },
                new { @ref = "switch", title = "The switch-over", status = "backlog" },
            },
        };

        using var created = await admin.PostAsJsonAsync("/issues", body, Ct);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var items = (await created.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("items").EnumerateArray().ToArray();

        Assert.Equal(Enumerable.Range(1, 7).Select(n => $"PLAN-{n}"), items.Select(i => i.GetProperty("key").GetString()));
        var schema = items[0];
        Assert.Equal(3, schema.GetProperty("priority").GetInt32());
        Assert.True(schema.GetProperty("ready").GetBoolean());
        Assert.Equal(["cut-1", "feature"], schema.GetProperty("labels").EnumerateArray().Select(l => l.GetProperty("name").GetString()));
        Assert.Equal("PLAN-E2", schema.GetProperty("epic").GetProperty("key").GetString());
        Assert.Equal("open", schema.GetProperty("epic").GetProperty("status").GetString());
        Assert.Equal("maintainer", schema.GetProperty("author").GetProperty("name").GetString());
        Assert.Equal(["bug", "chore", "cut-1", "feature"], schema.GetProperty("project_context").GetProperty("labels").EnumerateArray().Select(l => l.GetProperty("name").GetString()));
        Assert.Equal(["PLAN-2"], schema.GetProperty("blocks").EnumerateArray().Select(b => b.GetProperty("key").GetString()));

        var identity = items[1];
        Assert.Equal(["PLAN-1"], identity.GetProperty("blocked_by").EnumerateArray().Select(b => b.GetProperty("key").GetString()));
        Assert.Equal(1, identity.GetProperty("open_blockers").GetInt32());
        Assert.Equal(["PLAN-3", "PLAN-4", "PLAN-5"], identity.GetProperty("blocks").EnumerateArray().Select(b => b.GetProperty("key").GetString()));

        Assert.Equal("maintainer", items[3].GetProperty("assignee").GetProperty("name").GetString());
        Assert.Equal("backlog", items[6].GetProperty("status").GetString());
        Assert.Equal(["PLAN-6"], items[6].GetProperty("blocked_by").EnumerateArray().Select(b => b.GetProperty("key").GetString()));
        Assert.Equal("todo", items[5].GetProperty("status").GetString());

        // Read back complete, and listed slim.
        var read = await admin.GetFromJsonAsync<JsonElement>("/issues/PLAN-2", Ct);
        Assert.Equal("Identity and bootstrap", read.GetProperty("title").GetString());
        Assert.True(read.TryGetProperty("description", out _));

        var listed = await admin.GetFromJsonAsync<JsonElement>("/issues?project=PLAN&sort=created&order=asc", Ct);
        Assert.Equal(7, listed.GetProperty("total").GetInt32());
        Assert.False(listed.GetProperty("has_more").GetBoolean());
        var first = listed.GetProperty("items")[0];
        Assert.False(first.TryGetProperty("description", out _));
        Assert.Equal(["cut-1", "feature"], first.GetProperty("labels").EnumerateArray().Select(l => l.GetString()));
        Assert.Equal("PLAN-E2", first.GetProperty("epic").GetString());
        Assert.Equal(0, first.GetProperty("open_sub_issues").GetInt32());
        Assert.Matches(@"^\d{4}-\d\d-\d\dT\d\d:\d\d:\d\d\.\d{6}Z$", first.GetProperty("created_at").GetString());

        // History was written: a birth per issue, the labels, the edges.
        await using var reader = Migrated.ContextFor(instance.ConnectionString);
        Assert.Equal(7, await reader.History.CountAsync(h => h.Field == "created", Ct));
        Assert.Equal(6, await reader.History.CountAsync(h => h.Field == "blocked_by", Ct));
        Assert.Equal(1, await reader.History.CountAsync(h => h.EpicId != null, Ct));
    }

    [Fact]
    public async Task A_cycle_among_the_new_issues_refuses_the_whole_request()
    {
        await using var instance = await AnInstance.BootstrappedAsync(postgres);
        using var admin = await Ready(instance);

        var body = new
        {
            project = "PLAN",
            issues = new object[]
            {
                new { @ref = "a", title = "A", blocked_by = new[] { "c" } },
                new { @ref = "b", title = "B", blocked_by = new[] { "a" } },
                new { @ref = "c", title = "C", blocked_by = new[] { "b" } },
            },
        };

        using var refused = await admin.PostAsJsonAsync("/issues", body, Ct);
        var problem = await ProjectEndpointTests.Problem(refused, HttpStatusCode.UnprocessableEntity, "cycle");
        Assert.True(problem.GetProperty("path").GetArrayLength() >= 3);

        // Nothing was created, and the keys were not spent.
        var listed = await admin.GetFromJsonAsync<JsonElement>("/issues?project=PLAN", Ct);
        Assert.Equal(0, listed.GetProperty("total").GetInt32());

        using var next = await admin.PostAsJsonAsync("/issues", new { project = "PLAN", issues = new[] { new { title = "First for real" } } }, Ct);
        Assert.Equal("PLAN-1", (await next.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("items")[0].GetProperty("key").GetString());
    }

    [Fact]
    public async Task Three_hundred_issues_page_without_a_gap_or_a_duplicate_while_another_writer_inserts()
    {
        await using var instance = await AnInstance.BootstrappedAsync(postgres);
        using var admin = await Ready(instance);

        for (var batch = 0; batch < 3; batch++)
        {
            var issues = Enumerable.Range(0, 100).Select(i => new { title = $"Issue {batch * 100 + i}", priority = i % 5 }).ToArray();
            using var created = await admin.PostAsJsonAsync("/issues", new { project = "PLAN", issues }, Ct);
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        }

        var originals = Enumerable.Range(1, 300).Select(n => $"PLAN-{n}").ToHashSet();
        var seen = new List<string>();
        string? cursor = null;
        var pages = 0;

        do
        {
            var url = "/issues?project=PLAN&sort=priority&limit=50" + (cursor is null ? string.Empty : $"&cursor={Uri.EscapeDataString(cursor)}");
            var page = await admin.GetFromJsonAsync<JsonElement>(url, Ct);
            seen.AddRange(page.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("key").GetString()!));
            cursor = page.GetProperty("has_more").GetBoolean() ? page.GetProperty("next_cursor").GetString() : null;
            pages++;

            // Another writer between every page: seven more, with priorities
            // that land ahead of and behind the cursor.
            using var more = await admin.PostAsJsonAsync("/issues", new { project = "PLAN", issues = Enumerable.Range(0, 7).Select(i => new { title = $"Late {pages}-{i}", priority = i % 5 }).ToArray() }, Ct);
            Assert.Equal(HttpStatusCode.Created, more.StatusCode);
        }
        while (cursor is not null);

        Assert.Equal(seen.Count, seen.Distinct().Count());
        Assert.True(originals.IsSubsetOf(seen), $"{originals.Except(seen).Count()} of the original issues never appeared.");
        Assert.True(pages >= 6);

        // The cursor is bound to its filters and sort.
        var firstPage = await admin.GetFromJsonAsync<JsonElement>("/issues?project=PLAN&sort=priority&limit=50", Ct);
        var bound = firstPage.GetProperty("next_cursor").GetString()!;
        await ProjectEndpointTests.Problem(await admin.GetAsync($"/issues?project=PLAN&sort=created&cursor={Uri.EscapeDataString(bound)}", Ct), HttpStatusCode.BadRequest, "cursor-invalid");
        await ProjectEndpointTests.Problem(await admin.GetAsync("/issues?limit=201", Ct), HttpStatusCode.BadRequest, "validation");
    }

    [Fact]
    public async Task The_filters_narrow_the_list()
    {
        await using var instance = await AnInstance.BootstrappedAsync(postgres);
        using var admin = await Ready(instance);
        using var createdAgent = await admin.PostAsJsonAsync("/agents", new { name = "quiet-otter-42" }, Ct);
        using var agent = instance.ClientWith((await createdAgent.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("token").GetProperty("secret").GetString());

        await admin.PostAsJsonAsync("/issues", new
        {
            project = "PLAN",
            issues = new object[]
            {
                new { @ref = "a", title = "A", priority = 4, ready = true, labels = new[] { "bug" }, assignee = "maintainer" },
                new { @ref = "b", title = "B", priority = 1, labels = new[] { "feature" }, blocked_by = new[] { "a" } },
                new { @ref = "c", title = "C", status = "backlog", assignee = "quiet-otter-42" },
            },
        }, Ct);

        Assert.Equal(["PLAN-1"], await Keys(admin, "/issues?project=PLAN&label=bug"));
        Assert.Equal(["PLAN-1"], await Keys(admin, "/issues?project=PLAN&ready=true"));
        Assert.Equal(["PLAN-1"], await Keys(admin, "/issues?project=PLAN&priority_min=2"));
        Assert.Equal(["PLAN-1"], await Keys(admin, "/issues?project=PLAN&assignee=me"));
        Assert.Equal(["PLAN-3"], await Keys(agent, "/issues?project=PLAN&assignee=me"));
        Assert.Equal(["PLAN-2"], await Keys(admin, "/issues?project=PLAN&assignee=none"));
        Assert.Equal(["PLAN-3"], await Keys(admin, "/issues?project=PLAN&status=backlog"));
        Assert.Equal(["PLAN-2", "PLAN-1"], await Keys(admin, "/issues?project=PLAN&status=todo&status=in_progress&sort=created&order=desc"));
        Assert.Equal(["PLAN-2"], await Keys(admin, "/issues?project=PLAN&blocked=true"));
        // PLAN-2 waits on PLAN-1, which is open; the other two wait on nothing.
        Assert.Equal(["PLAN-1", "PLAN-3"], await Keys(admin, "/issues?project=PLAN&blocked=false&epic=none&claimed=false&author=maintainer&sort=created"));
        Assert.Equal(["PLAN-1", "PLAN-2", "PLAN-3"], await Keys(admin, "/issues?project=PLAN&sort=priority"));
        Assert.Empty(await Keys(admin, "/issues?project=PLAN&deleted=true"));
    }

    [Fact]
    public async Task A_patch_changes_what_is_present_and_a_stale_one_is_refused_with_the_current_issue()
    {
        await using var instance = await AnInstance.BootstrappedAsync(postgres);
        using var admin = await Ready(instance);
        await admin.PostAsJsonAsync("/projects/PLAN/labels", new { name = "cut-1" }, Ct);
        await admin.PostAsJsonAsync("/issues", new { project = "PLAN", issues = new[] { new { title = "Before", description = "Old text", labels = new[] { "bug" } } } }, Ct);

        var before = await admin.GetFromJsonAsync<JsonElement>("/issues/PLAN-1", Ct);
        var version = before.GetProperty("updated_at").GetString()!;

        using var request = new HttpRequestMessage(HttpMethod.Patch, "/issues/PLAN-1")
        {
            Content = JsonContent.Create(new { title = "After", description = (string?)null, priority = 2, ready = true, assignee = "maintainer", labels = new[] { "feature", "cut-1" } }),
        };
        request.Headers.TryAddWithoutValidation("If-Match", $"\"{version}\"");
        using var changed = await admin.SendAsync(request, Ct);
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        var after = await changed.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal("After", after.GetProperty("title").GetString());
        Assert.Equal(string.Empty, after.GetProperty("description").GetString());
        Assert.Equal(2, after.GetProperty("priority").GetInt32());
        Assert.True(after.GetProperty("ready").GetBoolean());
        Assert.Equal("maintainer", after.GetProperty("assignee").GetProperty("name").GetString());
        Assert.Equal(["cut-1", "feature"], after.GetProperty("labels").EnumerateArray().Select(l => l.GetProperty("name").GetString()));
        Assert.NotEqual(version, after.GetProperty("updated_at").GetString());

        // The old version again: stale, and the current issue comes with it.
        using var staleRequest = new HttpRequestMessage(HttpMethod.Patch, "/issues/PLAN-1") { Content = JsonContent.Create(new { title = "Later" }) };
        staleRequest.Headers.TryAddWithoutValidation("If-Match", $"\"{version}\"");
        using var stale = await admin.SendAsync(staleRequest, Ct);
        var problem = await ProjectEndpointTests.Problem(stale, HttpStatusCode.PreconditionFailed, "stale");
        Assert.Equal("After", problem.GetProperty("current").GetProperty("title").GetString());

        // Two labels of one group in the set; a label the project lacks; the status.
        await ProjectEndpointTests.Problem(await admin.PatchAsJsonAsync("/issues/PLAN-1", new { labels = new[] { "bug", "feature" } }, Ct), HttpStatusCode.BadRequest, "validation");
        await ProjectEndpointTests.Problem(await admin.PatchAsJsonAsync("/issues/PLAN-1", new { labels = new[] { "nope" } }, Ct), HttpStatusCode.UnprocessableEntity, "unknown-label");
        await ProjectEndpointTests.Problem(await admin.PatchAsJsonAsync("/issues/PLAN-1", new { status = "done" }, Ct), HttpStatusCode.UnprocessableEntity, "transition");
        await ProjectEndpointTests.Problem(await admin.GetAsync("/issues/PLAN-99", Ct), HttpStatusCode.NotFound, "not-found");

        await using var reader = Migrated.ContextFor(instance.ConnectionString);
        // The birth wrote `created` and one `label`; the patch wrote the rest:
        // `bug` off, `feature` and `cut-1` on, and the six fields.
        var fields = await reader.History.Where(h => h.Field != "created").Select(h => h.Field).ToListAsync(Ct);
        Assert.Equal(["assignee", "description", "label", "label", "label", "label", "priority", "ready", "title"], fields.Order().ToArray());
    }

    [Fact]
    public async Task Under_triage_required_an_agent_may_clear_ready_and_never_set_it()
    {
        await using var instance = await AnInstance.BootstrappedAsync(postgres);
        using var admin = await Ready(instance);
        await admin.PatchAsJsonAsync("/projects/PLAN", new { triage_required = true }, Ct);
        using var createdAgent = await admin.PostAsJsonAsync("/agents", new { }, Ct);
        using var agent = instance.ClientWith((await createdAgent.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("token").GetProperty("secret").GetString());

        await ProjectEndpointTests.Problem(
            await agent.PostAsJsonAsync("/issues", new { project = "PLAN", issues = new[] { new { title = "Thin", ready = true } } }, Ct),
            HttpStatusCode.Forbidden, "ready-requires-user");

        using var created = await agent.PostAsJsonAsync("/issues", new { project = "PLAN", issues = new[] { new { title = "Thin", ready = false } } }, Ct);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await admin.PatchAsJsonAsync("/issues/PLAN-1", new { ready = true }, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await agent.PatchAsJsonAsync("/issues/PLAN-1", new { ready = false }, Ct)).StatusCode);
        await ProjectEndpointTests.Problem(await agent.PatchAsJsonAsync("/issues/PLAN-1", new { ready = true }, Ct), HttpStatusCode.Forbidden, "ready-requires-user");
    }

    [Fact]
    public async Task The_edges_add_and_remove_a_label_and_a_blocker()
    {
        await using var instance = await AnInstance.BootstrappedAsync(postgres);
        using var admin = await Ready(instance);
        await admin.PostAsJsonAsync("/issues", new { project = "PLAN", issues = new object[] { new { title = "A", labels = new[] { "bug" } }, new { title = "B" }, new { title = "C" } } }, Ct);

        // A label of the same group replaces the other.
        using var relabelled = await admin.PostAsync("/issues/PLAN-1/labels/feature", null, Ct);
        Assert.Equal(HttpStatusCode.OK, relabelled.StatusCode);
        Assert.Equal(["feature"], (await relabelled.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("labels").EnumerateArray().Select(l => l.GetProperty("name").GetString()));
        using var unlabelled = await admin.DeleteAsync("/issues/PLAN-1/labels/feature", Ct);
        Assert.Empty((await unlabelled.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("labels").EnumerateArray());
        await ProjectEndpointTests.Problem(await admin.PostAsync("/issues/PLAN-1/labels/nope", null, Ct), HttpStatusCode.UnprocessableEntity, "unknown-label");

        // A blocks B blocks C; C blocking A would close the cycle.
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync("/issues/PLAN-2/blocked-by/PLAN-1", null, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync("/issues/PLAN-3/blocked-by/PLAN-2", null, Ct)).StatusCode);
        var problem = await ProjectEndpointTests.Problem(await admin.PostAsync("/issues/PLAN-1/blocked-by/PLAN-3", null, Ct), HttpStatusCode.UnprocessableEntity, "cycle");
        Assert.Equal(["PLAN-1", "PLAN-3", "PLAN-2", "PLAN-1"], problem.GetProperty("path").EnumerateArray().Select(p => p.GetString()));
        Assert.Empty((await admin.GetFromJsonAsync<JsonElement>("/issues/PLAN-1", Ct)).GetProperty("blocked_by").EnumerateArray());

        using var unblocked = await admin.DeleteAsync("/issues/PLAN-3/blocked-by/PLAN-2", Ct);
        Assert.Equal(HttpStatusCode.OK, unblocked.StatusCode);
        Assert.Empty((await unblocked.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("blocked_by").EnumerateArray());
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync("/issues/PLAN-1/blocked-by/PLAN-3", null, Ct)).StatusCode);
    }

    [Fact]
    public async Task A_deleted_issue_reads_as_deleted_and_lists_only_under_the_switch()
    {
        await using var instance = await AnInstance.BootstrappedAsync(postgres);
        using var admin = await Ready(instance);
        await admin.PostAsJsonAsync("/issues", new { project = "PLAN", issues = new[] { new { title = "Gone" }, new { title = "Here" } } }, Ct);

        await using (var context = Migrated.ContextFor(instance.ConnectionString))
        {
            var user = await context.Users.SingleAsync(Ct);
            await context.Database.ExecuteSqlRawAsync("update issue set deleted_at = now(), deleted_by = {0} where number = 1", [user.Id], Ct);
        }

        var problem = await ProjectEndpointTests.Problem(await admin.GetAsync("/issues/PLAN-1", Ct), HttpStatusCode.NotFound, "deleted");
        Assert.True(problem.TryGetProperty("restorable_until", out _));
        Assert.Equal(["PLAN-2"], await Keys(admin, "/issues?project=PLAN"));

        var deleted = await admin.GetFromJsonAsync<JsonElement>("/issues?project=PLAN&deleted=true", Ct);
        var item = Assert.Single(deleted.GetProperty("items").EnumerateArray());
        Assert.Equal("PLAN-1", item.GetProperty("key").GetString());
        Assert.Equal("maintainer", item.GetProperty("deleted_by").GetProperty("name").GetString());
    }

    private static async Task<HttpClient> Ready(AnInstance instance)
    {
        var admin = instance.ClientWith(AnInstance.BootstrapToken);
        using var project = await admin.PostAsJsonAsync("/projects", new { key = "PLAN", name = "planaffe" }, Ct);
        Assert.Equal(HttpStatusCode.Created, project.StatusCode);
        return admin;
    }

    private static async Task<string[]> Keys(HttpClient client, string url)
    {
        var page = await client.GetFromJsonAsync<JsonElement>(url, Ct);
        return [.. page.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("key").GetString()!)];
    }
}
