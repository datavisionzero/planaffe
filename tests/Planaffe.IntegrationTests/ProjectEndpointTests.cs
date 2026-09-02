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
    public async Task An_agent_reads_projects_and_administers_none_and_a_user_deletes_none()
    {
        await using var instance = await AnInstance.BootstrappedAsync(postgres);
        using var admin = instance.ClientWith(AnInstance.BootstrapToken);
        await admin.PostAsJsonAsync("/projects", new { key = "PLAN", name = "planaffe" }, Ct);

        using var invited = await admin.PostAsJsonAsync("/users", new { name = "other" }, Ct);
        using var user = instance.ClientWith((await invited.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("token").GetProperty("secret").GetString());
        using var createdAgent = await admin.PostAsJsonAsync("/agents", new { }, Ct);
        using var agent = instance.ClientWith((await createdAgent.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("token").GetProperty("secret").GetString());

        Assert.Equal(HttpStatusCode.OK, (await agent.GetAsync("/projects", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await agent.GetAsync("/projects/PLAN", Ct)).StatusCode);
        await Problem(await agent.PostAsJsonAsync("/projects", new { key = "AG", name = "x" }, Ct), HttpStatusCode.Forbidden, "forbidden");
        await Problem(await agent.PatchAsJsonAsync("/projects/PLAN", new { name = "x" }, Ct), HttpStatusCode.Forbidden, "forbidden");

        Assert.Equal(HttpStatusCode.OK, (await user.PatchAsJsonAsync("/projects/PLAN", new { name = "renamed" }, Ct)).StatusCode);
        await Problem(await user.DeleteAsync("/projects/PLAN", Ct), HttpStatusCode.Forbidden, "forbidden");
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
