using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Planaffe.Application.Ports;

namespace Planaffe.IntegrationTests;

/// <summary>
/// The one search across the knowledge base over HTTP (<c>docs/api.md</c>,
/// Search): that <c>/pages</c> asks for words, that a hit says where the page
/// stands and shows what matched, and that a space closed to agents is missing
/// from an agent's hits and from their number (ADR 0027).
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class KnowledgeSearchEndpointTests(PostgresFixture postgres)
{
    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_search_crosses_the_spaces_the_caller_may_see()
    {
        await using var instance = await AnInstance.BootstrappedAsync(postgres);
        using var admin = instance.ClientWith(AnInstance.BootstrapToken);
        await World(admin);

        var hits = await admin.GetFromJsonAsync<JsonElement>("/pages?q=vertraulich", Ct);

        Assert.Equal(
            ["handbuch", "personal"],
            hits.EnumerateArray().Select(hit => hit.GetProperty("space").GetString()).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task A_hit_says_where_the_page_stands_and_what_matched()
    {
        await using var instance = await AnInstance.BootstrappedAsync(postgres);
        using var admin = instance.ClientWith(AnInstance.BootstrapToken);
        await World(admin);

        var hits = await admin.GetFromJsonAsync<JsonElement>("/pages?q=Zugangskarte", Ct);

        var hit = Assert.Single(hits.EnumerateArray());
        Assert.Equal("onboarding/erster-tag", hit.GetProperty("path").GetString());
        Assert.Equal("handbuch", hit.GetProperty("space").GetString());
        Assert.Equal("Handbuch", hit.GetProperty("space_title").GetString());
        Assert.Equal("Erster Tag", hit.GetProperty("title").GetString());

        var step = Assert.Single(hit.GetProperty("trail").EnumerateArray());
        Assert.Equal("onboarding", step.GetProperty("path").GetString());
        Assert.Equal("Onboarding", step.GetProperty("title").GetString());

        // The excerpt is pieces with a flag each, and the marks stay inside the
        // instance: nothing that leaves here is markup (ADR 0007).
        var excerpt = hit.GetProperty("excerpt").EnumerateArray().ToList();
        Assert.Equal(
            ["Zugangskarte"],
            excerpt.Where(piece => piece.GetProperty("hit").GetBoolean()).Select(piece => piece.GetProperty("text").GetString()));
        Assert.All(excerpt, piece => Assert.DoesNotContain(
            $"{Excerpt.Start}", piece.GetProperty("text").GetString(), StringComparison.Ordinal));

        // A hit is the way to a page, not the page (ADR 0012).
        Assert.False(hit.TryGetProperty("body", out _));
    }

    [Fact]
    public async Task One_space_by_name_narrows_the_search()
    {
        await using var instance = await AnInstance.BootstrappedAsync(postgres);
        using var admin = instance.ClientWith(AnInstance.BootstrapToken);
        await World(admin);

        var hits = await admin.GetFromJsonAsync<JsonElement>("/pages?q=vertraulich&space=personal", Ct);

        Assert.Equal("vertrag", Assert.Single(hits.EnumerateArray()).GetProperty("path").GetString());
    }

    /// <summary>The address is a search, not a list of every page in the instance.</summary>
    [Fact]
    public async Task A_search_without_words_is_refused()
    {
        await using var instance = await AnInstance.BootstrappedAsync(postgres);
        using var admin = instance.ClientWith(AnInstance.BootstrapToken);
        await World(admin);

        await ProjectEndpointTests.Problem(
            await admin.GetAsync("/pages", Ct), HttpStatusCode.BadRequest, "validation");
        await ProjectEndpointTests.Problem(
            await admin.GetAsync("/pages?q=%20", Ct), HttpStatusCode.BadRequest, "validation");
        await ProjectEndpointTests.Problem(
            await admin.GetAsync("/pages?q=vertrag&limit=0", Ct), HttpStatusCode.BadRequest, "validation");
    }

    [Fact]
    public async Task The_limit_is_the_number_of_hits()
    {
        await using var instance = await AnInstance.BootstrappedAsync(postgres);
        using var admin = instance.ClientWith(AnInstance.BootstrapToken);
        await World(admin);

        var hits = await admin.GetFromJsonAsync<JsonElement>("/pages?q=vertraulich&limit=1", Ct);

        Assert.Single(hits.EnumerateArray());
    }

    [Fact]
    public async Task A_space_the_caller_has_not_got_is_not_found()
    {
        await using var instance = await AnInstance.BootstrappedAsync(postgres);
        using var admin = instance.ClientWith(AnInstance.BootstrapToken);
        await World(admin);

        await ProjectEndpointTests.Problem(
            await admin.GetAsync("/pages?q=vertrag&space=gibtsnicht", Ct), HttpStatusCode.NotFound, "not-found");
    }

    /// <summary>
    /// What the switch is for: a closed space leaves the agent's hits, and the
    /// number of them says nothing about it either (VISION 18).
    /// </summary>
    [Fact]
    public async Task A_closed_space_is_in_no_hit_of_an_agents()
    {
        await using var instance = await AnInstance.BootstrappedAsync(postgres);
        using var admin = instance.ClientWith(AnInstance.BootstrapToken);
        await World(admin);
        using var created = await admin.PostAsJsonAsync("/agents", new { name = "one" }, Ct);
        using var agent = instance.ClientWith(
            (await created.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("token").GetProperty("secret").GetString());

        Assert.Equal(2, (await agent.GetFromJsonAsync<JsonElement>("/pages?q=vertraulich", Ct)).GetArrayLength());

        await admin.PatchAsJsonAsync("/spaces/personal", new { closed_to_agents = true }, Ct);

        var hits = await agent.GetFromJsonAsync<JsonElement>("/pages?q=vertraulich", Ct);
        Assert.Equal(1, hits.GetArrayLength());
        Assert.Equal("handbuch", hits[0].GetProperty("space").GetString());

        // And naming it is the same answer a space that never existed gives.
        await ProjectEndpointTests.Problem(
            await agent.GetAsync("/pages?q=vertraulich&space=personal", Ct), HttpStatusCode.NotFound, "not-found");
    }

    [Fact]
    public async Task A_search_without_a_token_is_refused()
    {
        await using var instance = await AnInstance.BootstrappedAsync(postgres);
        using var stranger = instance.ClientWith(null);

        Assert.Equal(HttpStatusCode.Unauthorized, (await stranger.GetAsync("/pages?q=vertraulich", Ct)).StatusCode);
    }

    /// <summary>Two spaces, a tree in the first, and the word both of them carry.</summary>
    private static async Task World(HttpClient admin)
    {
        await admin.PostAsJsonAsync("/spaces", new { name = "handbuch", title = "Handbuch" }, Ct);
        await admin.PostAsJsonAsync("/spaces", new { name = "personal", title = "Personal" }, Ct);

        await admin.PostAsJsonAsync(
            "/spaces/handbuch/pages",
            new { slug = "onboarding", title = "Onboarding", body = "Wie eine neue Person ankommt. Vertraulich ist das nicht." },
            Ct);
        await admin.PostAsJsonAsync(
            "/spaces/handbuch/pages",
            new
            {
                parent = "onboarding",
                slug = "erster-tag",
                title = "Erster Tag",
                body = "Am ersten Tag bekommt jede neue Person eine Zugangskarte und einen Paten.",
            },
            Ct);
        await admin.PostAsJsonAsync(
            "/spaces/personal/pages",
            new { slug = "vertrag", title = "Vertrag", body = "Vertraulich, und deshalb hier." },
            Ct);
    }
}
