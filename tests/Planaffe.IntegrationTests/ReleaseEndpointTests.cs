using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Planaffe.IntegrationTests;

[Collection(nameof(PostgresCollection))]
public sealed class ReleaseEndpointTests(PostgresFixture postgres)
{
    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Done_collects_publish_freezes_and_reopen_records_shipping_again()
    {
        await using var instance = await AnInstance.BootstrappedAsync(postgres);
        using var admin = instance.ClientWith(AnInstance.BootstrapToken);
        await admin.PostAsJsonAsync("/projects", new { key = "PLAN", name = "planaffe" }, Ct);

        var initial = await admin.GetFromJsonAsync<JsonElement>("/projects/PLAN/releases", Ct);
        var open = Assert.Single(initial.EnumerateArray());
        Assert.Equal("unreleased", open.GetProperty("name").GetString());
        Assert.Equal(0, open.GetProperty("issues").GetInt32());

        await CreateAsync(admin, "Delivered");
        await CreateAsync(admin, "Canceled");
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync("/issues/PLAN-1/close", new { status = "done", result = "Built." }, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync("/issues/PLAN-2/close", new { status = "canceled", result = "Not built." }, Ct)).StatusCode);

        var unreleased = await admin.GetFromJsonAsync<JsonElement>("/projects/PLAN/releases/unreleased", Ct);
        Assert.Equal("PLAN-1", Assert.Single(unreleased.GetProperty("issues").EnumerateArray()).GetProperty("key").GetString());
        Assert.Equal("unreleased", (await admin.GetFromJsonAsync<JsonElement>("/issues/PLAN-1", Ct)).GetProperty("release").GetString());
        Assert.Equal(JsonValueKind.Null, (await admin.GetFromJsonAsync<JsonElement>("/issues/PLAN-2", Ct)).GetProperty("release").ValueKind);

        using var publishedResponse = await admin.PostAsJsonAsync("/projects/PLAN/releases/publish", new { name = "v1.0.0", description = "First." }, Ct);
        Assert.Equal(HttpStatusCode.Created, publishedResponse.StatusCode);
        var published = await publishedResponse.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal("v1.0.0", published.GetProperty("name").GetString());
        Assert.Equal("maintainer", published.GetProperty("published_by").GetProperty("name").GetString());
        Assert.NotEqual(JsonValueKind.Null, published.GetProperty("published_at").ValueKind);

        using var duplicate = await admin.PostAsJsonAsync("/projects/PLAN/releases/publish", new { name = "V1.0.0" }, Ct);
        await ProjectEndpointTests.Problem(duplicate, HttpStatusCode.Conflict, "release-exists");

        await admin.PostAsJsonAsync("/issues/PLAN-1/reopen", new { }, Ct);
        Assert.Equal("v1.0.0", (await admin.GetFromJsonAsync<JsonElement>("/issues/PLAN-1", Ct)).GetProperty("release").GetString());
        await admin.PostAsJsonAsync("/issues/PLAN-1/close", new { status = "done", result = "Fixed." }, Ct);
        var releases = await admin.GetFromJsonAsync<JsonElement>("/projects/PLAN/releases", Ct);
        Assert.Equal(["unreleased", "v1.0.0"], releases.EnumerateArray().Select(r => r.GetProperty("name").GetString()));
        Assert.All(releases.EnumerateArray(), r => Assert.Equal(1, r.GetProperty("issues").GetInt32()));

        using var deletion = await admin.DeleteAsync("/issues/PLAN-1", Ct);
        await ProjectEndpointTests.Problem(deletion, HttpStatusCode.UnprocessableEntity, "in-published-release");
    }

    [Fact]
    public async Task Sub_issues_ship_with_the_parent_and_notes_can_be_annotated()
    {
        await using var instance = await AnInstance.BootstrappedAsync(postgres);
        using var admin = instance.ClientWith(AnInstance.BootstrapToken);
        await admin.PostAsJsonAsync("/projects", new { key = "PLAN", name = "planaffe" }, Ct);
        await CreateAsync(admin, "Parent");
        using var child = await admin.PostAsJsonAsync("/issues", new { project = "PLAN", issues = new[] { new { title = "Child", parent = "PLAN-1" } } }, Ct);
        Assert.Equal(HttpStatusCode.Created, child.StatusCode);

        await admin.PostAsJsonAsync("/issues/PLAN-2/close", new { status = "done", result = "Part." }, Ct);
        Assert.Empty((await admin.GetFromJsonAsync<JsonElement>("/projects/PLAN/releases/unreleased", Ct)).GetProperty("issues").EnumerateArray());
        await admin.PostAsJsonAsync("/issues/PLAN-1/close", new { status = "done", result = "Whole." }, Ct);

        var release = await admin.GetFromJsonAsync<JsonElement>("/projects/PLAN/releases/unreleased", Ct);
        Assert.Equal(["PLAN-1", "PLAN-2"], release.GetProperty("issues").EnumerateArray().Select(i => i.GetProperty("key").GetString()));
        using var changed = await admin.PatchAsJsonAsync("/projects/PLAN/releases/unreleased", new { description = "Draft notes." }, Ct);
        Assert.Equal("Draft notes.", (await changed.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("description").GetString());
    }

    private static async Task CreateAsync(HttpClient client, string title)
    {
        using var response = await client.PostAsJsonAsync("/issues", new { project = "PLAN", issues = new[] { new { title } } }, Ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }
}
