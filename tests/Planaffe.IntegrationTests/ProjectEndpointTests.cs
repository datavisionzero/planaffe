using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Planaffe.IntegrationTests;

/// <summary>Projects over HTTP (<c>docs/api.md</c>, Projects).</summary>
[Collection(nameof(PostgresCollection))]
public sealed class ProjectEndpointTests(PostgresFixture postgres)
{
    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Administration_lists_live_and_deleted_projects()
    {
        await using var instance = await AnInstance.BootstrappedAsync(postgres);
        using var admin = instance.ClientWith(AnInstance.BootstrapToken);
        await admin.PostAsJsonAsync("/projects", new { key = "LIVE", name = "live" }, Ct);
        await admin.PostAsJsonAsync("/projects", new { key = "GONE", name = "gone" }, Ct);
        await admin.DeleteAsync("/projects/GONE", Ct);

        var all = await admin.GetFromJsonAsync<JsonElement>("/admin/projects?deleted=all", Ct);
        Assert.Equal(2, all.GetArrayLength());
        Assert.Equal(JsonValueKind.Null, all.EnumerateArray().Single(x => x.GetProperty("key").GetString() == "LIVE").GetProperty("deleted_at").ValueKind);
        Assert.NotEqual(JsonValueKind.Null, all.EnumerateArray().Single(x => x.GetProperty("key").GetString() == "GONE").GetProperty("deleted_at").ValueKind);
    }

    [Fact]
    public async Task A_project_round_trips_with_its_switches_and_its_kind_labels()
    {
        await using var instance = await AnInstance.BootstrappedAsync(postgres);
        using var admin = instance.ClientWith(AnInstance.BootstrapToken);

        using var created = await admin.PostAsJsonAsync("/projects", new { key = "PLAN", name = "planaffe", review_required = true }, Ct);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal("/projects/PLAN", created.Headers.Location?.ToString());
        var project = await created.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal("PLAN", project.GetProperty("key").GetString());
        Assert.False(project.GetProperty("triage_required").GetBoolean());
        Assert.True(project.GetProperty("review_required").GetBoolean());

        var read = await admin.GetFromJsonAsync<JsonElement>("/projects/PLAN", Ct);
        Assert.Equal("planaffe", read.GetProperty("name").GetString());

        var listed = await admin.GetFromJsonAsync<JsonElement>("/projects", Ct);
        Assert.Equal("PLAN", Assert.Single(listed.EnumerateArray()).GetProperty("key").GetString());

        // The `kind` group, with a line each.
        var labels = await admin.GetFromJsonAsync<JsonElement>("/projects/PLAN/labels", Ct);
        Assert.Equal(["bug", "chore", "feature"], labels.EnumerateArray().Select(l => l.GetProperty("name").GetString()));
        Assert.All(labels.EnumerateArray(), l =>
        {
            Assert.Equal("kind", l.GetProperty("group").GetString());
            Assert.False(string.IsNullOrEmpty(l.GetProperty("description").GetString()));
        });

        // PATCH changes what is present and nothing else.
        using var changed = await admin.PatchAsJsonAsync("/projects/PLAN", new { triage_required = true }, Ct);
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        var after = await changed.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.True(after.GetProperty("triage_required").GetBoolean());
        Assert.True(after.GetProperty("review_required").GetBoolean());
        Assert.Equal("planaffe", after.GetProperty("name").GetString());
    }

    [Fact]
    public async Task A_key_is_the_pattern_and_is_taken_even_by_a_deleted_project()
    {
        await using var instance = await AnInstance.BootstrappedAsync(postgres);
        using var admin = instance.ClientWith(AnInstance.BootstrapToken);

        using var lower = await admin.PostAsJsonAsync("/projects", new { key = "plan", name = "x" }, Ct);
        var problem = await Problem(lower, HttpStatusCode.BadRequest, "validation");
        Assert.True(problem.GetProperty("errors").TryGetProperty("key", out _));

        using var first = await admin.PostAsJsonAsync("/projects", new { key = "PLAN", name = "x" }, Ct);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        using var deleted = await admin.DeleteAsync("/projects/PLAN", Ct);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        using var again = await admin.PostAsJsonAsync("/projects", new { key = "PLAN", name = "y" }, Ct);
        await Problem(again, HttpStatusCode.BadRequest, "validation");
    }

    [Fact]
    public async Task Deleting_hides_a_project_and_restoring_brings_it_back()
    {
        await using var instance = await AnInstance.BootstrappedAsync(postgres);
        using var admin = instance.ClientWith(AnInstance.BootstrapToken);
        await admin.PostAsJsonAsync("/projects", new { key = "PLAN", name = "planaffe" }, Ct);

        using var deleted = await admin.DeleteAsync("/projects/PLAN", Ct);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        var listed = await admin.GetFromJsonAsync<JsonElement>("/projects", Ct);
        Assert.Empty(listed.EnumerateArray());

        using var read = await admin.GetAsync("/projects/PLAN", Ct);
        var problem = await Problem(read, HttpStatusCode.NotFound, "deleted");
        Assert.True(problem.TryGetProperty("restorable_until", out _));

        using var labels = await admin.GetAsync("/projects/PLAN/labels", Ct);
        await Problem(labels, HttpStatusCode.NotFound, "deleted");

        using var restored = await admin.PostAsync("/projects/PLAN/restore", null, Ct);
        Assert.Equal(HttpStatusCode.OK, restored.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/projects/PLAN", Ct)).StatusCode);

        using var notDeleted = await admin.PostAsync("/projects/PLAN/restore", null, Ct);
        await Problem(notDeleted, HttpStatusCode.UnprocessableEntity, "transition");

        using var unknown = await admin.GetAsync("/projects/NOPE", Ct);
        await Problem(unknown, HttpStatusCode.NotFound, "not-found");
    }

    [Fact]
    public async Task Project_access_is_granted_to_users_inherited_by_agents_and_hidden_as_not_found()
    {
        await using var instance = await AnInstance.BootstrappedAsync(postgres);
        using var admin = instance.ClientWith(AnInstance.BootstrapToken);
        await admin.PostAsJsonAsync("/projects", new { key = "PLAN", name = "planaffe" }, Ct);

        var otherToken = await instance.AddActiveUserAsync("other");
        using var user = instance.ClientWith(otherToken);
        using var users = await admin.GetAsync("/users", Ct);
        var otherId = (await users.Content.ReadFromJsonAsync<JsonElement>(Ct)).EnumerateArray()
            .Single(value => value.GetProperty("name").GetString() == "other").GetProperty("id").GetGuid();

        using var otherAgentResponse = await user.PostAsJsonAsync("/agents", new { }, Ct);
        using var agent = instance.ClientWith((await otherAgentResponse.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("token").GetProperty("secret").GetString());

        Assert.Equal(HttpStatusCode.OK, (await agent.GetAsync("/projects", Ct)).StatusCode);
        Assert.Empty((await agent.GetFromJsonAsync<JsonElement>("/projects", Ct)).EnumerateArray());
        await Problem(await agent.GetAsync("/projects/PLAN", Ct), HttpStatusCode.NotFound, "not-found");
        await Problem(await user.GetAsync("/issues?project=PLAN", Ct), HttpStatusCode.NotFound, "not-found");

        using var granted = await admin.PutAsync($"/projects/PLAN/users/{otherId}", null, Ct);
        Assert.Equal(HttpStatusCode.NoContent, granted.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await agent.GetAsync("/projects/PLAN", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await user.PatchAsJsonAsync("/projects/PLAN", new { name = "renamed" }, Ct)).StatusCode);

        var assigned = await user.GetFromJsonAsync<JsonElement>("/projects/PLAN/users", Ct);
        Assert.Equal(["maintainer", "other"], assigned.EnumerateArray().Select(value => value.GetProperty("name").GetString()).Order());

        using var revoked = await admin.DeleteAsync($"/projects/PLAN/users/{otherId}", Ct);
        Assert.Equal(HttpStatusCode.NoContent, revoked.StatusCode);
        await Problem(await agent.GetAsync("/projects/PLAN", Ct), HttpStatusCode.NotFound, "not-found");

        await Problem(await agent.PostAsJsonAsync("/projects", new { key = "AG", name = "x" }, Ct), HttpStatusCode.Forbidden, "forbidden");
        await Problem(await agent.PatchAsJsonAsync("/projects/PLAN", new { name = "x" }, Ct), HttpStatusCode.NotFound, "not-found");
        await Problem(await user.DeleteAsync("/projects/PLAN", Ct), HttpStatusCode.Forbidden, "forbidden");
    }

    [Fact]
    public async Task Collections_are_scoped_and_a_foreign_blocker_stays_anonymous_but_open()
    {
        await using var instance = await AnInstance.BootstrappedAsync(postgres);
        using var admin = instance.ClientWith(AnInstance.BootstrapToken);
        await admin.PostAsJsonAsync("/projects", new { key = "ONE", name = "one" }, Ct);
        await admin.PostAsJsonAsync("/projects", new { key = "TWO", name = "two" }, Ct);
        await admin.PostAsJsonAsync("/issues", new { project = "ONE", issues = new[] { new { title = "Visible" } } }, Ct);
        await admin.PostAsJsonAsync("/issues", new { project = "TWO", issues = new[] { new { title = "Hidden" } } }, Ct);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync("/issues/ONE-1/blocked-by/TWO-1", null, Ct)).StatusCode);

        using var user = instance.ClientWith(await instance.AddActiveUserAsync("reader"));
        var users = await admin.GetFromJsonAsync<JsonElement>("/users", Ct);
        var userId = users.EnumerateArray().Single(value => value.GetProperty("name").GetString() == "reader").GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsync($"/projects/ONE/users/{userId}", null, Ct)).StatusCode);

        var listed = await user.GetFromJsonAsync<JsonElement>("/issues", Ct);
        Assert.Equal(1, listed.GetProperty("total").GetInt32());
        Assert.Equal("ONE-1", listed.GetProperty("items")[0].GetProperty("key").GetString());
        var blocker = Assert.Single(listed.GetProperty("items")[0].GetProperty("blocked_by").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, blocker.GetProperty("key").ValueKind);
        Assert.True(blocker.GetProperty("open").GetBoolean());

        await Problem(await user.GetAsync("/issues/TWO-1", Ct), HttpStatusCode.NotFound, "not-found");
        await Problem(await user.GetAsync("/issues?project=TWO", Ct), HttpStatusCode.NotFound, "not-found");
    }

    /// <summary>
    /// Routing matches literal segments without regard to case, so every
    /// project-content route answers under a spelling the scope guard has to
    /// recognise as its own. It once compared the request path and let
    /// <c>/Issues/PLAN-1</c> past with no check at all.
    /// </summary>
    [Fact]
    public async Task Project_scope_holds_when_the_route_is_spelled_in_another_case()
    {
        await using var instance = await AnInstance.BootstrappedAsync(postgres);
        using var admin = instance.ClientWith(AnInstance.BootstrapToken);
        await admin.PostAsJsonAsync("/projects", new { key = "PLAN", name = "planaffe" }, Ct);
        await admin.PostAsJsonAsync("/issues", new { project = "PLAN", issues = new[] { new { title = "Secret work" } } }, Ct);
        await admin.PostAsJsonAsync("/projects/PLAN/labels", new { name = "secret" }, Ct);
        using var asked = await admin.PostAsJsonAsync("/issues/PLAN-1/questions", new { question = "Which secret?" }, Ct);
        var question = (await asked.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetString();

        using var outsider = instance.ClientWith(await instance.AddActiveUserAsync("outsider"));
        foreach (var (method, path, body) in new (HttpMethod Method, string Path, object? Body)[]
        {
            (HttpMethod.Get, "/Projects/PLAN", null),
            (HttpMethod.Get, "/Projects/PLAN/labels", null),
            (HttpMethod.Post, "/Projects/PLAN/labels", new { name = "planted" }),
            (HttpMethod.Delete, "/Projects/PLAN/labels/secret", null),
            (HttpMethod.Get, "/Projects/PLAN/releases", null),
            (HttpMethod.Get, "/Projects/PLAN/needs-you", null),
            (HttpMethod.Get, "/Issues/PLAN-1", null),
            (HttpMethod.Patch, "/Issues/PLAN-1", new { title = "hijacked" }),
            (HttpMethod.Get, "/Issues/PLAN-1/history", null),
            (HttpMethod.Post, "/Issues/PLAN-1/comments", new { body = "hello" }),
            (HttpMethod.Post, "/Issues/PLAN-1/questions", new { question = "leak?" }),
            (HttpMethod.Post, "/Issues/PLAN-1/claim", null),
            (HttpMethod.Get, $"/Questions/{question}", null),
            (HttpMethod.Post, $"/Questions/{question}/answer", new { answer = "planted" }),
        })
        {
            using var request = new HttpRequestMessage(method, path);
            if (body is not null) request.Content = JsonContent.Create(body);
            await Problem(await outsider.SendAsync(request, Ct), HttpStatusCode.NotFound, "not-found");
        }

        // Nothing the outsider sent was written: the refusal came before the handler.
        var labels = await admin.GetFromJsonAsync<JsonElement>("/projects/PLAN/labels", Ct);
        Assert.Contains("secret", labels.EnumerateArray().Select(label => label.GetProperty("name").GetString()));
        Assert.DoesNotContain("planted", labels.EnumerateArray().Select(label => label.GetProperty("name").GetString()));

        var issue = await admin.GetFromJsonAsync<JsonElement>("/issues/PLAN-1", Ct);
        Assert.Equal("Secret work", issue.GetProperty("title").GetString());
        Assert.Equal("todo", issue.GetProperty("status").GetString());
        Assert.Empty(issue.GetProperty("comments").EnumerateArray());
        Assert.Single(issue.GetProperty("questions").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, issue.GetProperty("questions")[0].GetProperty("answer").ValueKind);
    }

    internal static async Task<JsonElement> Problem(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        using (response)
        {
            Assert.Equal(status, response.StatusCode);
            var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
            Assert.Equal($"/problems/{code}", problem.GetProperty("type").GetString());
            return problem;
        }
    }
}
