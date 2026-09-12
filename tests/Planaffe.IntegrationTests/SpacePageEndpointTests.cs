using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Planaffe.IntegrationTests;

/// <summary>
/// The pages of a space over HTTP (<c>docs/api.md</c>, Space pages): an address
/// that carries the tree, three levels and no fourth, a subtree that goes and
/// comes back whole, and a space closed to agents that answers an agent exactly
/// as it answers a stranger (ADR 0027, ADR 0028).
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class SpacePageEndpointTests(PostgresFixture postgres)
{
    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_page_is_created_under_the_space_and_read_back_under_its_address()
    {
        await using var instance = await AnInstance.BootstrappedAsync(postgres);
        using var admin = instance.ClientWith(AnInstance.BootstrapToken);
        await admin.PostAsJsonAsync("/spaces", new { name = "handbuch", title = "Handbuch" }, Ct);

        using var created = await admin.PostAsJsonAsync(
            "/spaces/handbuch/pages", new { slug = "company", title = "Company", body = "Wer wir sind." }, Ct);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal("/spaces/handbuch/pages/company", created.Headers.Location?.ToString());
        var page = await created.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal("company", page.GetProperty("path").GetString());
        Assert.Equal("handbuch", page.GetProperty("space").GetString());
        Assert.Equal(0, page.GetProperty("depth").GetInt32());
        Assert.Equal(JsonValueKind.Null, page.GetProperty("parent").ValueKind);
        Assert.Equal(AnInstance.Administrator, page.GetProperty("author").GetProperty("name").GetString());

        var read = await admin.GetFromJsonAsync<JsonElement>("/spaces/handbuch/pages/company", Ct);
        Assert.Equal("Wer wir sind.", read.GetProperty("body").GetString());
    }

    [Fact]
    public async Task The_address_carries_the_tree_and_stops_at_the_third_level()
    {
        await using var instance = await AnInstance.BootstrappedAsync(postgres);
        using var admin = instance.ClientWith(AnInstance.BootstrapToken);
        await Tree(admin);

        var deep = await admin.GetFromJsonAsync<JsonElement>(
            "/spaces/handbuch/pages/company/handbook/day-one", Ct);
        Assert.Equal("company/handbook", deep.GetProperty("parent").GetString());
        Assert.Equal(2, deep.GetProperty("depth").GetInt32());

        var refusal = await ProjectEndpointTests.Problem(
            await admin.PostAsJsonAsync(
                "/spaces/handbuch/pages",
                new { parent = "company/handbook/day-one", slug = "hour-one", title = "Hour one" },
                Ct),
            HttpStatusCode.UnprocessableEntity, "too-deep");
        Assert.Equal(2, refusal.GetProperty("depth").GetInt32());

        // A fourth segment names nothing, and says so as an address rather than
        // as a complaint about a field.
        await ProjectEndpointTests.Problem(
            await admin.GetAsync("/spaces/handbuch/pages/company/handbook/day-one/hour-one", Ct),
            HttpStatusCode.NotFound, "not-found");
        await ProjectEndpointTests.Problem(
            await admin.GetAsync("/spaces/handbuch/pages/Nichts%20Da", Ct), HttpStatusCode.NotFound, "not-found");
    }

    [Fact]
    public async Task The_tree_is_drawn_in_order_and_carries_no_bodies()
    {
        await using var instance = await AnInstance.BootstrappedAsync(postgres);
        using var admin = instance.ClientWith(AnInstance.BootstrapToken);
        await Tree(admin);

        var tree = await admin.GetFromJsonAsync<JsonElement>("/spaces/handbuch/pages", Ct);

        Assert.Equal(
            ["company", "company/handbook", "company/handbook/day-one", "company/notes", "product"],
            tree.EnumerateArray().Select(p => p.GetProperty("path").GetString()));
        Assert.All(tree.EnumerateArray(), p => Assert.False(p.TryGetProperty("body", out _)));
    }

    [Fact]
    public async Task A_slug_is_taken_under_its_parent_and_free_under_another()
    {
        await using var instance = await AnInstance.BootstrappedAsync(postgres);
        using var admin = instance.ClientWith(AnInstance.BootstrapToken);
        await Tree(admin);

        Assert.Equal(
            HttpStatusCode.Created,
            (await admin.PostAsJsonAsync(
                "/spaces/handbuch/pages", new { parent = "product", slug = "notes", title = "Product notes" }, Ct)).StatusCode);

        await ProjectEndpointTests.Problem(
            await admin.PostAsJsonAsync(
                "/spaces/handbuch/pages", new { parent = "company", slug = "notes", title = "Again" }, Ct),
            HttpStatusCode.BadRequest, "validation");
    }

    [Fact]
    public async Task A_rename_leaves_nothing_behind_and_If_Match_guards_the_document()
    {
        await using var instance = await AnInstance.BootstrappedAsync(postgres);
        using var admin = instance.ClientWith(AnInstance.BootstrapToken);
        await Tree(admin);
        var before = await admin.GetFromJsonAsync<JsonElement>("/spaces/handbuch/pages/company", Ct);

        using var stale = new HttpRequestMessage(HttpMethod.Patch, "/spaces/handbuch/pages/company")
        {
            Content = JsonContent.Create(new { title = "Firma" }),
        };
        stale.Headers.TryAddWithoutValidation("If-Match", "\"2020-01-01T00:00:00.000000Z\"");
        await ProjectEndpointTests.Problem(
            await admin.SendAsync(stale, Ct), HttpStatusCode.PreconditionFailed, "stale");

        using var write = new HttpRequestMessage(HttpMethod.Patch, "/spaces/handbuch/pages/company")
        {
            Content = JsonContent.Create(new { slug = "firma", title = "Firma", body = (string?)null }),
        };
        write.Headers.TryAddWithoutValidation("If-Match", $"\"{before.GetProperty("updated_at").GetString()}\"");
        using var changed = await admin.SendAsync(write, Ct);

        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        var page = await changed.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal("firma", page.GetProperty("path").GetString());
        Assert.Equal(string.Empty, page.GetProperty("body").GetString());

        // The children moved with the name, and the old address is gone.
        await ProjectEndpointTests.Problem(
            await admin.GetAsync("/spaces/handbuch/pages/company", Ct), HttpStatusCode.NotFound, "not-found");
        Assert.Equal(
            HttpStatusCode.OK,
            (await admin.GetAsync("/spaces/handbuch/pages/firma/handbook/day-one", Ct)).StatusCode);
    }

    [Fact]
    public async Task Moving_takes_the_subtree_and_refuses_a_cycle_and_a_fourth_level()
    {
        await using var instance = await AnInstance.BootstrappedAsync(postgres);
        using var admin = instance.ClientWith(AnInstance.BootstrapToken);
        await Tree(admin);

        await ProjectEndpointTests.Problem(
            await admin.PostAsJsonAsync(
                "/spaces/handbuch/pages/move", new { path = "company", parent = "company/handbook" }, Ct),
            HttpStatusCode.UnprocessableEntity, "cycle");

        await ProjectEndpointTests.Problem(
            await admin.PostAsJsonAsync(
                "/spaces/handbuch/pages/move", new { path = "company", parent = "product" }, Ct),
            HttpStatusCode.UnprocessableEntity, "too-deep");

        using var moved = await admin.PostAsJsonAsync(
            "/spaces/handbuch/pages/move", new { path = "company/handbook", parent = "product" }, Ct);

        Assert.Equal(HttpStatusCode.OK, moved.StatusCode);
        Assert.Equal("product/handbook", (await moved.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("path").GetString());
        Assert.Equal(
            HttpStatusCode.OK,
            (await admin.GetAsync("/spaces/handbuch/pages/product/handbook/day-one", Ct)).StatusCode);
    }

    [Fact]
    public async Task A_page_moves_into_another_space_and_never_into_one_the_caller_cannot_see()
    {
        await using var instance = await AnInstance.BootstrappedAsync(postgres);
        using var admin = instance.ClientWith(AnInstance.BootstrapToken);
        await Tree(admin);
        await admin.PostAsJsonAsync("/spaces", new { name = "personal", title = "Personal" }, Ct);

        using var colleague = instance.ClientWith(await instance.AddActiveUserAsync("colleague"));
        await ProjectEndpointTests.Problem(
            await colleague.PostAsJsonAsync(
                "/spaces/handbuch/pages/move", new { path = "company", space = "personal" }, Ct),
            HttpStatusCode.NotFound, "not-found");

        using var moved = await admin.PostAsJsonAsync(
            "/spaces/handbuch/pages/move", new { path = "company", space = "personal" }, Ct);

        Assert.Equal(HttpStatusCode.OK, moved.StatusCode);
        Assert.Equal("personal", (await moved.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("space").GetString());
        Assert.Equal(
            ["company", "company/handbook", "company/handbook/day-one", "company/notes"],
            (await admin.GetFromJsonAsync<JsonElement>("/spaces/personal/pages", Ct))
                .EnumerateArray().Select(p => p.GetProperty("path").GetString()));
        Assert.Equal(
            ["product"],
            (await admin.GetFromJsonAsync<JsonElement>("/spaces/handbuch/pages", Ct))
                .EnumerateArray().Select(p => p.GetProperty("path").GetString()));
    }

    [Fact]
    public async Task Deleting_takes_the_subtree_and_restoring_brings_back_what_went()
    {
        await using var instance = await AnInstance.BootstrappedAsync(postgres);
        using var admin = instance.ClientWith(AnInstance.BootstrapToken);
        await Tree(admin);

        // One page goes on its own first, and has to stay behind afterwards.
        using var alone = await admin.DeleteAsync("/spaces/handbuch/pages/company/notes", Ct);
        Assert.Equal(1, (await alone.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("deleted").GetInt32());

        // The page, the handbook under it and the day under that — three, and
        // not the one that had already gone.
        using var deleted = await admin.DeleteAsync("/spaces/handbuch/pages/company", Ct);
        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);
        Assert.Equal(3, (await deleted.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("deleted").GetInt32());

        var gone = await ProjectEndpointTests.Problem(
            await admin.GetAsync("/spaces/handbuch/pages/company/handbook", Ct), HttpStatusCode.NotFound, "deleted");
        Assert.True(gone.TryGetProperty("restorable_until", out _));

        // The slug is not free while the page can come back.
        await ProjectEndpointTests.Problem(
            await admin.PostAsJsonAsync("/spaces/handbuch/pages", new { slug = "company", title = "Etwas anderes" }, Ct),
            HttpStatusCode.BadRequest, "validation");

        // And a page below it cannot come back on its own.
        await ProjectEndpointTests.Problem(
            await admin.PostAsJsonAsync("/spaces/handbuch/pages/restore", new { path = "company/handbook" }, Ct),
            HttpStatusCode.UnprocessableEntity, "transition");

        using var restored = await admin.PostAsJsonAsync("/spaces/handbuch/pages/restore", new { path = "company" }, Ct);
        Assert.Equal(HttpStatusCode.OK, restored.StatusCode);

        Assert.Equal(
            ["company", "company/handbook", "company/handbook/day-one", "product"],
            (await admin.GetFromJsonAsync<JsonElement>("/spaces/handbuch/pages", Ct))
                .EnumerateArray().Select(p => p.GetProperty("path").GetString()));
    }

    /// <summary>
    /// The border of VISION 18 from the other side: the bracket is a human's,
    /// and everything inside it is the agent's too.
    /// </summary>
    [Fact]
    public async Task An_agent_writes_pages_in_a_space_that_is_open_to_it()
    {
        await using var instance = await AnInstance.BootstrappedAsync(postgres);
        using var admin = instance.ClientWith(AnInstance.BootstrapToken);
        await admin.PostAsJsonAsync("/spaces", new { name = "handbuch", title = "Handbuch" }, Ct);
        using var agent = await Agent(instance, admin);

        using var created = await agent.PostAsJsonAsync(
            "/spaces/handbuch/pages", new { slug = "company", title = "Company" }, Ct);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await agent.PatchAsJsonAsync(
            "/spaces/handbuch/pages/company", new { title = "Firma" }, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await agent.DeleteAsync("/spaces/handbuch/pages/company", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await agent.PostAsJsonAsync(
            "/spaces/handbuch/pages/restore", new { path = "company" }, Ct)).StatusCode);
    }

    /// <summary>
    /// A closed space is absent for an agent on every one of these routes, and
    /// the answer is the one a space that never existed gives — a stranger's,
    /// word for word (ADR 0027).
    /// </summary>
    [Fact]
    public async Task A_closed_space_has_no_pages_for_an_agent()
    {
        await using var instance = await AnInstance.BootstrappedAsync(postgres);
        using var admin = instance.ClientWith(AnInstance.BootstrapToken);
        await Tree(admin);
        using var agent = await Agent(instance, admin);
        Assert.Equal(HttpStatusCode.OK, (await agent.GetAsync("/spaces/handbuch/pages", Ct)).StatusCode);

        await admin.PatchAsJsonAsync("/spaces/handbuch", new { closed_to_agents = true }, Ct);
        using var stranger = instance.ClientWith(await instance.AddActiveUserAsync("colleague"));

        var closed = await ProjectEndpointTests.Problem(
            await agent.GetAsync("/spaces/handbuch/pages/company", Ct), HttpStatusCode.NotFound, "not-found");
        var unknown = await ProjectEndpointTests.Problem(
            await stranger.GetAsync("/spaces/handbuch/pages/company", Ct), HttpStatusCode.NotFound, "not-found");
        Assert.Equal(unknown.GetProperty("detail").GetString(), closed.GetProperty("detail").GetString());

        await ProjectEndpointTests.Problem(
            await agent.GetAsync("/spaces/handbuch/pages", Ct), HttpStatusCode.NotFound, "not-found");
        await ProjectEndpointTests.Problem(
            await agent.PostAsJsonAsync("/spaces/handbuch/pages", new { slug = "eigenes", title = "Eigenes" }, Ct),
            HttpStatusCode.NotFound, "not-found");
        await ProjectEndpointTests.Problem(
            await agent.DeleteAsync("/spaces/handbuch/pages/company", Ct), HttpStatusCode.NotFound, "not-found");
    }

    [Fact]
    public async Task A_deleted_space_has_no_pages_at_all()
    {
        await using var instance = await AnInstance.BootstrappedAsync(postgres);
        using var admin = instance.ClientWith(AnInstance.BootstrapToken);
        await Tree(admin);

        await admin.DeleteAsync("/spaces/handbuch", Ct);

        var refusal = await ProjectEndpointTests.Problem(
            await admin.GetAsync("/spaces/handbuch/pages/company", Ct), HttpStatusCode.NotFound, "deleted");
        Assert.True(refusal.TryGetProperty("restorable_until", out _));

        await admin.PostAsync("/spaces/handbuch/restore", null, Ct);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/spaces/handbuch/pages/company", Ct)).StatusCode);
    }

    /// <summary>Two roots, and under one of them two levels.</summary>
    private static async Task Tree(HttpClient admin)
    {
        await admin.PostAsJsonAsync("/spaces", new { name = "handbuch", title = "Handbuch" }, Ct);
        await admin.PostAsJsonAsync("/spaces/handbuch/pages", new { slug = "company", title = "Company" }, Ct);
        await admin.PostAsJsonAsync("/spaces/handbuch/pages", new { slug = "product", title = "Product" }, Ct);
        await admin.PostAsJsonAsync(
            "/spaces/handbuch/pages", new { parent = "company", slug = "handbook", title = "Handbook" }, Ct);
        await admin.PostAsJsonAsync(
            "/spaces/handbuch/pages", new { parent = "company", slug = "notes", title = "Notes" }, Ct);
        await admin.PostAsJsonAsync(
            "/spaces/handbuch/pages", new { parent = "company/handbook", slug = "day-one", title = "Day one" }, Ct);
    }

    private static async Task<HttpClient> Agent(AnInstance instance, HttpClient admin)
    {
        using var created = await admin.PostAsJsonAsync("/agents", new { name = "one" }, Ct);
        return instance.ClientWith(
            (await created.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("token").GetProperty("secret").GetString());
    }
}
