using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Planaffe.IntegrationTests;

/// <summary>
/// Spaces over HTTP (<c>docs/api.md</c>, Spaces): the knowledge base's bracket,
/// addressed by a name, opened by a user and never by an agent, and closed to
/// agents in a way that leaves nothing for one to find
/// ([ADR 0027](../../docs/adr/0027-the-knowledge-base-hangs-on-a-space-not-on-a-project.md)).
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class SpaceEndpointTests(PostgresFixture postgres)
{
    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_user_opens_a_space_and_is_named_on_it()
    {
        await using var instance = await AnInstance.BootstrappedAsync(postgres);
        using var admin = instance.ClientWith(AnInstance.BootstrapToken);

        using var created = await admin.PostAsJsonAsync("/spaces", new { name = "handbuch", title = "Handbuch" }, Ct);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal("/spaces/handbuch", created.Headers.Location?.ToString());
        var space = await created.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal("handbuch", space.GetProperty("name").GetString());
        Assert.Equal("Handbuch", space.GetProperty("title").GetString());
        Assert.False(space.GetProperty("closed_to_agents").GetBoolean());
        Assert.Equal(AnInstance.Administrator, space.GetProperty("author").GetProperty("name").GetString());

        Assert.Equal(
            ["handbuch"],
            (await admin.GetFromJsonAsync<JsonElement>("/spaces", Ct)).EnumerateArray().Select(s => s.GetProperty("name").GetString()));

        // Whoever opens the bracket is in it, with nobody granting anything.
        Assert.Equal(
            [AnInstance.Administrator],
            (await admin.GetFromJsonAsync<JsonElement>("/spaces/handbuch/users", Ct))
                .EnumerateArray().Select(u => u.GetProperty("name").GetString()));
    }

    [Fact]
    public async Task A_name_is_given_validated_and_taken_only_once()
    {
        await using var instance = await AnInstance.BootstrappedAsync(postgres);
        using var admin = instance.ClientWith(AnInstance.BootstrapToken);

        await ProjectEndpointTests.Problem(
            await admin.PostAsJsonAsync("/spaces", new { name = "Kein Slug", title = "No" }, Ct),
            HttpStatusCode.BadRequest, "validation");
        await ProjectEndpointTests.Problem(
            await admin.PostAsJsonAsync("/spaces", new { name = "handbuch", title = "" }, Ct),
            HttpStatusCode.BadRequest, "validation");

        await admin.PostAsJsonAsync("/spaces", new { name = "handbuch", title = "Handbuch" }, Ct);
        await ProjectEndpointTests.Problem(
            await admin.PostAsJsonAsync("/spaces", new { name = "handbuch", title = "Again" }, Ct),
            HttpStatusCode.BadRequest, "validation");

        // A name that could not be one names nothing, and it arrived in the
        // path rather than in a body.
        await ProjectEndpointTests.Problem(await admin.GetAsync("/spaces/Nichts%20Da", Ct), HttpStatusCode.NotFound, "not-found");
        await ProjectEndpointTests.Problem(await admin.GetAsync("/spaces/personal", Ct), HttpStatusCode.NotFound, "not-found");
    }

    [Fact]
    public async Task Renaming_leaves_nothing_behind_and_the_switch_goes_both_ways()
    {
        await using var instance = await AnInstance.BootstrappedAsync(postgres);
        using var admin = instance.ClientWith(AnInstance.BootstrapToken);
        await admin.PostAsJsonAsync("/spaces", new { name = "handbuch", title = "Handbuch" }, Ct);

        using var renamed = await admin.PatchAsJsonAsync(
            "/spaces/handbuch", new { name = "firmenhandbuch", closed_to_agents = true }, Ct);

        Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);
        var space = await renamed.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal("firmenhandbuch", space.GetProperty("name").GetString());
        Assert.True(space.GetProperty("closed_to_agents").GetBoolean());

        await ProjectEndpointTests.Problem(await admin.GetAsync("/spaces/handbuch", Ct), HttpStatusCode.NotFound, "not-found");

        using var opened = await admin.PatchAsJsonAsync("/spaces/firmenhandbuch", new { closed_to_agents = false }, Ct);
        Assert.False((await opened.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("closed_to_agents").GetBoolean());
    }

    /// <summary>
    /// The border of ADR 0027: an agent reads the knowledge base and shapes
    /// none of it. Every refusal here is the one a user gets nowhere else.
    /// </summary>
    [Fact]
    public async Task An_agent_reads_spaces_and_opens_none()
    {
        await using var instance = await AnInstance.BootstrappedAsync(postgres);
        using var admin = instance.ClientWith(AnInstance.BootstrapToken);
        await admin.PostAsJsonAsync("/spaces", new { name = "handbuch", title = "Handbuch" }, Ct);
        using var agent = await Agent(instance, admin, "one");

        Assert.Equal(
            ["handbuch"],
            (await agent.GetFromJsonAsync<JsonElement>("/spaces", Ct)).EnumerateArray().Select(s => s.GetProperty("name").GetString()));
        Assert.Equal(HttpStatusCode.OK, (await agent.GetAsync("/spaces/handbuch", Ct)).StatusCode);

        await ProjectEndpointTests.Problem(
            await agent.PostAsJsonAsync("/spaces", new { name = "eigenes", title = "Eigenes" }, Ct),
            HttpStatusCode.Forbidden, "forbidden");
        await ProjectEndpointTests.Problem(
            await agent.PatchAsJsonAsync("/spaces/handbuch", new { title = "Umbenannt" }, Ct),
            HttpStatusCode.Forbidden, "forbidden");
        await ProjectEndpointTests.Problem(
            await agent.DeleteAsync("/spaces/handbuch", Ct), HttpStatusCode.Forbidden, "forbidden");
        await ProjectEndpointTests.Problem(
            await agent.GetAsync("/spaces/handbuch/users", Ct), HttpStatusCode.Forbidden, "forbidden");
    }

    /// <summary>
    /// A closed space is absent for an agent, not refused: the list omits it,
    /// the direct read denies it exists, and the answer is the one a space
    /// that never existed would give.
    /// </summary>
    [Fact]
    public async Task A_closed_space_is_not_there_for_an_agent()
    {
        await using var instance = await AnInstance.BootstrappedAsync(postgres);
        using var admin = instance.ClientWith(AnInstance.BootstrapToken);
        await admin.PostAsJsonAsync("/spaces", new { name = "handbuch", title = "Handbuch" }, Ct);
        await admin.PostAsJsonAsync("/spaces", new { name = "personal", title = "Personal", closed_to_agents = true }, Ct);
        using var agent = await Agent(instance, admin, "one");

        // The owner sees both; the agent sees one.
        Assert.Equal(2, (await admin.GetFromJsonAsync<JsonElement>("/spaces", Ct)).GetArrayLength());
        Assert.Equal(
            ["handbuch"],
            (await agent.GetFromJsonAsync<JsonElement>("/spaces", Ct)).EnumerateArray().Select(s => s.GetProperty("name").GetString()));

        var closed = await ProjectEndpointTests.Problem(
            await agent.GetAsync("/spaces/personal", Ct), HttpStatusCode.NotFound, "not-found");
        var absent = await ProjectEndpointTests.Problem(
            await agent.GetAsync("/spaces/gibtesnicht", Ct), HttpStatusCode.NotFound, "not-found");

        // Same code, and the same sentence apart from the name the caller
        // typed: an answer that told the two apart would hand over the
        // existence the switch is there to keep.
        Assert.Equal(
            absent.GetProperty("detail").GetString()!.Replace("gibtesnicht", "personal", StringComparison.Ordinal),
            closed.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task An_administrator_names_another_user_on_a_space_and_takes_them_off_again()
    {
        await using var instance = await AnInstance.BootstrappedAsync(postgres);
        using var admin = instance.ClientWith(AnInstance.BootstrapToken);
        var colleague = await instance.AddActiveUserAsync("colleague");
        using var theirs = instance.ClientWith(colleague);
        await admin.PostAsJsonAsync("/spaces", new { name = "handbuch", title = "Handbuch" }, Ct);

        Assert.Empty((await theirs.GetFromJsonAsync<JsonElement>("/spaces", Ct)).EnumerateArray());
        await ProjectEndpointTests.Problem(await theirs.GetAsync("/spaces/handbuch", Ct), HttpStatusCode.NotFound, "not-found");

        var id = (await UserId(admin, "colleague")).ToString();
        using var granted = await admin.PutAsync($"/spaces/handbuch/users/{id}", null, Ct);
        Assert.Equal(HttpStatusCode.NoContent, granted.StatusCode);

        Assert.Equal(
            ["handbuch"],
            (await theirs.GetFromJsonAsync<JsonElement>("/spaces", Ct)).EnumerateArray().Select(s => s.GetProperty("name").GetString()));

        // Granting twice is the state asked for, not an error.
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsync($"/spaces/handbuch/users/{id}", null, Ct)).StatusCode);

        // A user named on the space may not hand it to anybody else.
        await ProjectEndpointTests.Problem(
            await theirs.PutAsync($"/spaces/handbuch/users/{id}", null, Ct), HttpStatusCode.Forbidden, "forbidden");

        using var revoked = await admin.DeleteAsync($"/spaces/handbuch/users/{id}", Ct);
        Assert.Equal(HttpStatusCode.NoContent, revoked.StatusCode);
        Assert.Empty((await theirs.GetFromJsonAsync<JsonElement>("/spaces", Ct)).EnumerateArray());
    }

    [Fact]
    public async Task Deleting_keeps_the_name_and_restoring_gives_the_space_back()
    {
        await using var instance = await AnInstance.BootstrappedAsync(postgres);
        using var admin = instance.ClientWith(AnInstance.BootstrapToken);
        await admin.PostAsJsonAsync("/spaces", new { name = "handbuch", title = "Handbuch" }, Ct);

        var colleague = await instance.AddActiveUserAsync("colleague");
        using var theirs = instance.ClientWith(colleague);
        await admin.PutAsync($"/spaces/handbuch/users/{await UserId(admin, "colleague")}", null, Ct);

        // A bracket is not one person's to take away from everybody on it.
        await ProjectEndpointTests.Problem(
            await theirs.DeleteAsync("/spaces/handbuch", Ct), HttpStatusCode.Forbidden, "forbidden");

        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync("/spaces/handbuch", Ct)).StatusCode);

        var gone = await ProjectEndpointTests.Problem(
            await admin.GetAsync("/spaces/handbuch", Ct), HttpStatusCode.NotFound, "deleted");
        Assert.True(gone.TryGetProperty("restorable_until", out _));
        Assert.Empty((await admin.GetFromJsonAsync<JsonElement>("/spaces", Ct)).EnumerateArray());

        // The name is not free while the space can come back.
        await ProjectEndpointTests.Problem(
            await admin.PostAsJsonAsync("/spaces", new { name = "handbuch", title = "Etwas anderes" }, Ct),
            HttpStatusCode.NotFound, "deleted");

        // And the deleted space is findable where a deleted thing is found.
        Assert.Equal(
            ["handbuch"],
            (await admin.GetFromJsonAsync<JsonElement>("/admin/spaces", Ct))
                .EnumerateArray().Select(s => s.GetProperty("name").GetString()));

        using var restored = await admin.PostAsync("/spaces/handbuch/restore", null, Ct);
        Assert.Equal(HttpStatusCode.OK, restored.StatusCode);
        Assert.Equal("Handbuch", (await restored.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("title").GetString());
        await ProjectEndpointTests.Problem(
            await admin.PostAsync("/spaces/handbuch/restore", null, Ct),
            HttpStatusCode.UnprocessableEntity, "transition");

        // The grant survived the deletion, so the space comes back to the
        // people it came back for.
        Assert.Equal(
            ["colleague", AnInstance.Administrator],
            (await admin.GetFromJsonAsync<JsonElement>("/spaces/handbuch/users", Ct))
                .EnumerateArray().Select(u => u.GetProperty("name").GetString()).Order());
    }

    [Fact]
    public async Task The_administration_list_is_the_administrators_alone()
    {
        await using var instance = await AnInstance.BootstrappedAsync(postgres);
        using var admin = instance.ClientWith(AnInstance.BootstrapToken);
        using var theirs = instance.ClientWith(await instance.AddActiveUserAsync("colleague"));

        await ProjectEndpointTests.Problem(await theirs.GetAsync("/admin/spaces", Ct), HttpStatusCode.Forbidden, "forbidden");
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/admin/spaces", Ct)).StatusCode);
    }

    private static async Task<Guid> UserId(HttpClient admin, string name) =>
        (await admin.GetFromJsonAsync<JsonElement>("/users", Ct)).EnumerateArray()
            .Single(u => u.GetProperty("name").GetString() == name)
            .GetProperty("id").GetGuid();

    private static async Task<HttpClient> Agent(AnInstance instance, HttpClient admin, string name)
    {
        using var created = await admin.PostAsJsonAsync("/agents", new { name }, Ct);
        return instance.ClientWith((await created.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("token").GetProperty("secret").GetString());
    }
}
