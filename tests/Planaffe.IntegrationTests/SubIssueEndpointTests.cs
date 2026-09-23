using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace Planaffe.IntegrationTests;

/// <summary>Sub-issues are full issues, one level deep, with their parent's gates and epic.</summary>
[Collection(nameof(PostgresCollection))]
public sealed class SubIssueEndpointTests(PostgresFixture postgres)
{
    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_forward_parent_ref_copies_priority_inherits_epic_and_round_trips_both_shapes()
    {
        await using var instance = await AnInstance.BootstrappedAsync(postgres);
        using var admin = await Project(instance);
        await admin.PostAsJsonAsync("/epics", new { project = "PLAN", title = "Cut two" }, Ct);

        using var response = await admin.PostAsJsonAsync("/issues", new
        {
            project = "PLAN",
            issues = new object[]
            {
                new { title = "Child", parent = "parent" },
                new { @ref = "parent", title = "Parent", priority = 3, epic = "PLAN-E1" },
            },
        }, Ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var child = await admin.GetFromJsonAsync<JsonElement>("/issues/PLAN-1", Ct);
        Assert.Equal(3, child.GetProperty("priority").GetInt32());
        Assert.Equal("PLAN-E1", child.GetProperty("epic").GetProperty("key").GetString());
        Assert.Equal("PLAN-2", child.GetProperty("parent").GetProperty("key").GetString());

        var parent = await admin.GetFromJsonAsync<JsonElement>("/issues/PLAN-2", Ct);
        Assert.Equal(["PLAN-1"], parent.GetProperty("sub_issues").EnumerateArray().Select(i => i.GetProperty("key").GetString()));
        Assert.Equal(1, parent.GetProperty("open_sub_issues").GetInt32());

        var list = await admin.GetFromJsonAsync<JsonElement>("/issues?project=PLAN&sort=created", Ct);
        Assert.Equal("PLAN-2", list.GetProperty("items")[0].GetProperty("parent").GetString());
        Assert.Equal(1, list.GetProperty("items")[1].GetProperty("open_sub_issues").GetInt32());
    }

    [Fact]
    public async Task Hierarchy_refusals_and_parent_gates_are_enforced()
    {
        await using var instance = await AnInstance.BootstrappedAsync(postgres);
        using var admin = await Project(instance);
        await admin.PostAsJsonAsync("/projects", new { key = "OTHER", name = "other" }, Ct);
        await Create(admin, "PLAN", new { @ref = "parent", title = "Parent" }, new { title = "Child", parent = "parent" });
        await Create(admin, "OTHER", new { title = "Elsewhere" });

        await Problem(await admin.PostAsJsonAsync("/issues", new { project = "PLAN", issues = new[] { new { title = "Grandchild", parent = "PLAN-2" } } }, Ct), "one-level");
        await Problem(await admin.PostAsJsonAsync("/issues", new { project = "PLAN", issues = new[] { new { title = "Cross", parent = "OTHER-1" } } }, Ct), "other-project");
        await Problem(await admin.PostAsJsonAsync("/issues", new { project = "PLAN", issues = new[] { new { title = "Own epic", parent = "PLAN-1", epic = "PLAN-E1" } } }, Ct), "epic-inherited");
        await Problem(await admin.DeleteAsync("/issues/PLAN-1", Ct), "has-sub-issues");

        using var createdAgent = await admin.PostAsJsonAsync("/agents", new { name = "worker" }, Ct);
        using var agent = instance.ClientWith((await createdAgent.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("token").GetProperty("secret").GetString());
        var next = await agent.GetFromJsonAsync<JsonElement>("/projects/PLAN/next", Ct);
        Assert.Equal(["PLAN-2"], next.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("key").GetString()));

        await using (var context = Migrated.ContextFor(instance.ConnectionString))
        {
            await context.Database.ExecuteSqlRawAsync("update issue set status = 'backlog' where number = 1", Ct);
        }
        next = await agent.GetFromJsonAsync<JsonElement>("/projects/PLAN/next", Ct);
        Assert.Empty(next.GetProperty("items").EnumerateArray());
        Assert.Equal(1, next.GetProperty("reasons").GetProperty("parent_gated").GetInt32());
    }

    [Fact]
    public async Task Two_concurrent_moves_that_would_each_add_a_level_never_make_two()
    {
        await using var instance = await AnInstance.BootstrappedAsync(postgres);
        using var admin = await Project(instance);

        for (var round = 0; round < 8; round++)
        {
            // X, P and Q, three fresh top-level issues per round.
            var x = round * 3 + 1;
            var p = x + 1;
            var q = x + 2;
            await Create(admin, "PLAN", new { title = $"X{round}" }, new { title = $"P{round}" }, new { title = $"Q{round}" });

            var answers = await Task.WhenAll(
                admin.PatchAsJsonAsync($"/issues/PLAN-{x}", new { parent = $"PLAN-{p}" }, Ct),
                admin.PatchAsJsonAsync($"/issues/PLAN-{p}", new { parent = $"PLAN-{q}" }, Ct));

            Assert.Single(answers, a => a.StatusCode == HttpStatusCode.OK);
            await Problem(Assert.Single(answers, a => a.StatusCode != HttpStatusCode.OK), "one-level");
        }
    }

    [Fact]
    public async Task Taking_the_parents_epic_is_history_and_reopens_it_and_detaching_frees_the_epic()
    {
        await using var instance = await AnInstance.BootstrappedAsync(postgres);
        using var admin = await Project(instance);
        await admin.PostAsJsonAsync("/epics", new { project = "PLAN", title = "Cut two" }, Ct);
        await admin.PostAsJsonAsync("/epics", new { project = "PLAN", title = "Cut three" }, Ct);
        await Create(admin, "PLAN", new { title = "Parent", epic = "PLAN-E1" }, new { title = "Loose" });
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync("/epics/PLAN-E1/close", null, Ct)).StatusCode);

        using var moved = await admin.PatchAsJsonAsync("/issues/PLAN-2", new { parent = "PLAN-1" }, Ct);
        Assert.Equal(HttpStatusCode.OK, moved.StatusCode);
        Assert.Equal("PLAN-E1", (await moved.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("epic").GetProperty("key").GetString());
        Assert.Equal("open", (await admin.GetFromJsonAsync<JsonElement>("/epics/PLAN-E1", Ct)).GetProperty("status").GetString());

        var history = await admin.GetFromJsonAsync<JsonElement>("/issues/PLAN-2/history", Ct);
        var epic = Assert.Single(history.EnumerateArray(), e => e.GetProperty("field").GetString() == "epic");
        Assert.Equal("PLAN-E1", epic.GetProperty("new_value").GetString());

        // Out from under the parent and into an epic of its own, in one PATCH.
        using var detached = await admin.PatchAsJsonAsync("/issues/PLAN-2", new { parent = (string?)null, epic = "PLAN-E2" }, Ct);
        Assert.Equal(HttpStatusCode.OK, detached.StatusCode);
        var after = await detached.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal(JsonValueKind.Null, after.GetProperty("parent").ValueKind);
        Assert.Equal("PLAN-E2", after.GetProperty("epic").GetProperty("key").GetString());

        // Still a sub-issue: the epic follows the parent.
        await Problem(await admin.PatchAsJsonAsync("/issues/PLAN-2", new { parent = "PLAN-1", epic = "PLAN-E2" }, Ct), "epic-inherited");
    }

    private static async Task<HttpClient> Project(AnInstance instance)
    {
        var admin = instance.ClientWith(AnInstance.BootstrapToken);
        using var project = await admin.PostAsJsonAsync("/projects", new { key = "PLAN", name = "planaffe" }, Ct);
        Assert.Equal(HttpStatusCode.Created, project.StatusCode);
        return admin;
    }

    private static async Task Create(HttpClient client, string project, params object[] issues)
    {
        using var response = await client.PostAsJsonAsync("/issues", new { project, issues }, Ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private static async Task Problem(HttpResponseMessage response, string code)
    {
        using (response)
        {
            await ProjectEndpointTests.Problem(response, HttpStatusCode.UnprocessableEntity, code);
        }
    }
}
