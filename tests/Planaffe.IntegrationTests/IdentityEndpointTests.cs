using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Npgsql;

namespace Planaffe.IntegrationTests;

/// <summary>
/// Users, agents and tokens over HTTP (<c>docs/api.md</c>): the permission line
/// between a user and an agent, the secret shown once, and revocation that
/// keeps the identity (ADR 0013, ADR 0015).
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class IdentityEndpointTests(PostgresFixture postgres)
{
    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task An_agent_reports_partial_metadata_and_every_report_is_kept()
    {
        await using var instance = await AnInstance.BootstrappedAsync(postgres);
        using var admin = instance.ClientWith(AnInstance.BootstrapToken);
        using var created = await admin.PostAsJsonAsync("/agents", new { name = "quiet-otter-42" }, Ct);
        var agent = await created.Content.ReadFromJsonAsync<JsonElement>(Ct);
        var id = agent.GetProperty("id").GetGuid();
        using var asAgent = instance.ClientWith(agent.GetProperty("token").GetProperty("secret").GetString()!);

        using var first = await asAgent.PatchAsJsonAsync("/me/metadata", new
        {
            kind = "codex", harness = "cli", environment = "container", version = "1.2.3",
        }, Ct);
        Assert.True(first.StatusCode == HttpStatusCode.OK,
            $"{await first.Content.ReadAsStringAsync(Ct)}\n{string.Join('\n', instance.Errors)}");
        var reported = await first.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal("codex", reported.GetProperty("metadata").GetProperty("kind").GetString());
        Assert.NotEqual(JsonValueKind.Null, reported.GetProperty("metadata_reported_at").ValueKind);

        // Absent keeps the old value; null clears it.
        using var second = await asAgent.PatchAsJsonAsync("/me/metadata", new { harness = (string?)null, version = "1.2.4" }, Ct);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        reported = await second.Content.ReadFromJsonAsync<JsonElement>(Ct);
        var metadata = reported.GetProperty("metadata");
        Assert.Equal("codex", metadata.GetProperty("kind").GetString());
        Assert.Equal(JsonValueKind.Null, metadata.GetProperty("harness").ValueKind);
        Assert.Equal("container", metadata.GetProperty("environment").GetString());
        Assert.Equal("1.2.4", metadata.GetProperty("version").GetString());

        // A user sees the last report in the management list.
        var agents = await admin.GetFromJsonAsync<JsonElement>("/agents", Ct);
        var listed = Assert.Single(agents.EnumerateArray());
        Assert.Equal("1.2.4", listed.GetProperty("metadata").GetProperty("version").GetString());
        Assert.NotEqual(JsonValueKind.Null, listed.GetProperty("metadata_reported_at").ValueKind);

        // The write keeps both complete snapshots; no read endpoint exposes this history yet.
        await using var connection = new NpgsqlConnection(instance.ConnectionString);
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand(
            "select metadata::text from identity_metadata where identity_id = @id order by reported_at", connection);
        command.Parameters.AddWithValue("id", id);
        await using var reader = await command.ExecuteReaderAsync(Ct);
        var snapshots = new List<string>();
        while (await reader.ReadAsync(Ct)) snapshots.Add(reader.GetString(0));
        Assert.Equal(2, snapshots.Count);
        Assert.Contains("\"harness\": \"cli\"", snapshots[0], StringComparison.Ordinal);
        Assert.Contains("\"harness\": null", snapshots[1], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Only_an_agent_reports_metadata_and_the_shape_is_closed()
    {
        await using var instance = await AnInstance.BootstrappedAsync(postgres);
        using var admin = instance.ClientWith(AnInstance.BootstrapToken);

        using var forbidden = await admin.PatchAsJsonAsync("/me/metadata", new { kind = "user" }, Ct);
        await Problem(forbidden, HttpStatusCode.Forbidden, "forbidden", string.Join('\n', instance.Errors));

        using var asAgent = instance.ClientWith(await AgentSecretAsync(admin));
        using var unknown = await asAgent.PatchAsJsonAsync("/me/metadata", new { model = "not-stable" }, Ct);
        var unknownProblem = await Problem(unknown, HttpStatusCode.BadRequest, "unknown-field");
        Assert.Equal("model", unknownProblem.GetProperty("field").GetString());

        using var tooLong = await asAgent.PatchAsJsonAsync("/me/metadata", new { environment = new string('x', 101) }, Ct);
        var validation = await Problem(tooLong, HttpStatusCode.BadRequest, "validation");
        Assert.True(validation.GetProperty("errors").TryGetProperty("environment", out _));
    }

    [Fact]
    public async Task A_user_creates_an_agent_the_agent_works_and_a_revoked_agent_still_has_its_name()
    {
        await using var instance = await AnInstance.BootstrappedAsync(postgres);
        using var admin = instance.ClientWith(AnInstance.BootstrapToken);

        // The user creates an agent and gets the secret once.
        using var created = await admin.PostAsJsonAsync("/agents", new { name = "quiet-otter-42" }, Ct);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var agent = await created.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal("agent", agent.GetProperty("kind").GetString());
        Assert.Equal(AnInstance.Administrator, agent.GetProperty("owner").GetProperty("name").GetString());
        var secret = agent.GetProperty("token").GetProperty("secret").GetString()!;
        Assert.StartsWith("pa_", secret, StringComparison.Ordinal);
        Assert.Equal(secret[..8], agent.GetProperty("token").GetProperty("prefix").GetString());

        // The agent's token authenticates as an agent.
        using var asAgent = instance.ClientWith(secret);
        var me = await asAgent.GetFromJsonAsync<JsonElement>("/me", Ct);
        Assert.Equal("agent", me.GetProperty("kind").GetString());
        Assert.Equal("quiet-otter-42", me.GetProperty("name").GetString());

        // The user revokes it; the next call fails.
        var id = agent.GetProperty("id").GetGuid();
        using var revoked = await admin.DeleteAsync($"/agents/{id}", Ct);
        Assert.Equal(HttpStatusCode.NoContent, revoked.StatusCode);

        using var refused = await asAgent.GetAsync("/me", Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, refused.StatusCode);

        // And the agent still appears by name, with its revocation.
        var agents = await admin.GetFromJsonAsync<JsonElement>("/agents", Ct);
        var listed = Assert.Single(agents.EnumerateArray());
        Assert.Equal("quiet-otter-42", listed.GetProperty("name").GetString());
        Assert.NotEqual(JsonValueKind.Null, listed.GetProperty("token").GetProperty("revoked_at").ValueKind);
        Assert.False(listed.GetProperty("token").TryGetProperty("secret", out _));

        // Revoking twice is uneventful.
        using var again = await admin.DeleteAsync($"/agents/{id}", Ct);
        Assert.Equal(HttpStatusCode.NoContent, again.StatusCode);
    }

    [Fact]
    public async Task An_agent_without_a_name_is_given_one()
    {
        await using var instance = await AnInstance.BootstrappedAsync(postgres);
        using var admin = instance.ClientWith(AnInstance.BootstrapToken);

        using var created = await admin.PostAsJsonAsync("/agents", new { }, Ct);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var agent = await created.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Matches("^[a-z]+-[a-z]+-[0-9]{1,2}$", agent.GetProperty("name").GetString());
    }

    [Fact]
    public async Task An_agent_may_call_none_of_these_and_is_told_why()
    {
        await using var instance = await AnInstance.BootstrappedAsync(postgres);
        using var admin = instance.ClientWith(AnInstance.BootstrapToken);
        using var asAgent = instance.ClientWith(await AgentSecretAsync(admin));

        foreach (var (method, path) in new[]
        {
            (HttpMethod.Post, "/users"), (HttpMethod.Get, "/users"),
            (HttpMethod.Post, "/agents"), (HttpMethod.Get, "/agents"),
            (HttpMethod.Get, "/tokens"), (HttpMethod.Post, "/tokens"),
        })
        {
            using var request = new HttpRequestMessage(method, path);
            if (method == HttpMethod.Post)
            {
                request.Content = JsonContent.Create(new { name = "somebody" });
            }

            using var response = await asAgent.SendAsync(request, Ct);
            await Problem(response, HttpStatusCode.Forbidden, "forbidden", $"{method} {path}");
        }
    }

    [Fact]
    public async Task Only_an_administrator_invites_users()
    {
        await using var instance = await AnInstance.BootstrappedAsync(postgres);
        using var admin = instance.ClientWith(AnInstance.BootstrapToken);

        using var asUser = instance.ClientWith(await instance.AddActiveUserAsync("Second Person"));

        // A user who is not an administrator may not invite.
        using var refused = await asUser.PostAsJsonAsync("/users", new { name = "third", email = "third@example.test" }, Ct);
        await Problem(refused, HttpStatusCode.Forbidden, "forbidden");

        // Listing all users is instance administration too.
        await Problem(await asUser.GetAsync("/users", Ct), HttpStatusCode.Forbidden, "forbidden");
    }

    [Fact]
    public async Task A_name_is_unique_across_both_kinds_regardless_of_case()
    {
        await using var instance = await AnInstance.BootstrappedAsync(postgres);
        using var admin = instance.ClientWith(AnInstance.BootstrapToken);

        using var taken = await admin.PostAsJsonAsync("/agents", new { name = "MAINTAINER" }, Ct);
        var problem = await Problem(taken, HttpStatusCode.BadRequest, "validation");
        Assert.True(problem.GetProperty("errors").TryGetProperty("name", out _));

        using var blank = await admin.PostAsJsonAsync("/users", new { name = "   " }, Ct);
        await Problem(blank, HttpStatusCode.BadRequest, "validation");
    }

    [Fact]
    public async Task The_owner_renames_and_another_user_may_not()
    {
        await using var instance = await AnInstance.BootstrappedAsync(postgres);
        using var admin = instance.ClientWith(AnInstance.BootstrapToken);

        using var other = instance.ClientWith(await instance.AddActiveUserAsync("other"));

        // `other` owns the agent; the administrator does not.
        using var created = await other.PostAsJsonAsync("/agents", new { name = "quiet-otter-42" }, Ct);
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid();

        using var renamed = await other.PatchAsJsonAsync($"/agents/{id}", new { name = "brisk-heron-7" }, Ct);
        Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);
        Assert.Equal("brisk-heron-7", (await renamed.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("name").GetString());

        // An administrator may too; a third user may not; an unknown id is not found.
        using var byAdmin = await admin.PatchAsJsonAsync($"/agents/{id}", new { name = "calm-badger-3" }, Ct);
        Assert.Equal(HttpStatusCode.OK, byAdmin.StatusCode);

        using var third = instance.ClientWith(await instance.AddActiveUserAsync("third"));
        using var refused = await third.PatchAsJsonAsync($"/agents/{id}", new { name = "stolen" }, Ct);
        await Problem(refused, HttpStatusCode.Forbidden, "forbidden");
        using var revokeRefused = await third.DeleteAsync($"/agents/{id}", Ct);
        await Problem(revokeRefused, HttpStatusCode.Forbidden, "forbidden");

        using var unknown = await admin.PatchAsJsonAsync($"/agents/{Guid.NewGuid()}", new { name = "nobody" }, Ct);
        await Problem(unknown, HttpStatusCode.NotFound, "not-found");
    }

    [Fact]
    public async Task A_user_has_as_many_tokens_as_they_create_and_revokes_only_their_own()
    {
        await using var instance = await AnInstance.BootstrappedAsync(postgres);
        using var admin = instance.ClientWith(AnInstance.BootstrapToken);

        using var created = await admin.PostAsync("/tokens", null, Ct);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var issued = await created.Content.ReadFromJsonAsync<JsonElement>(Ct);
        var secret = issued.GetProperty("secret").GetString()!;
        var id = issued.GetProperty("id").GetGuid();

        using var withNew = instance.ClientWith(secret);
        Assert.Equal(HttpStatusCode.OK, (await withNew.GetAsync("/me", Ct)).StatusCode);

        var tokens = await admin.GetFromJsonAsync<JsonElement>("/tokens", Ct);
        Assert.Equal(2, tokens.GetArrayLength());
        Assert.All(tokens.EnumerateArray(), t => Assert.False(t.TryGetProperty("secret", out _)));

        // Another user cannot see, and so cannot revoke, this token.
        using var other = instance.ClientWith(await instance.AddActiveUserAsync("other"));
        using var notTheirs = await other.DeleteAsync($"/tokens/{id}", Ct);
        await Problem(notTheirs, HttpStatusCode.NotFound, "not-found");

        // The owner revokes it, and it stops working.
        using var revoked = await admin.DeleteAsync($"/tokens/{id}", Ct);
        Assert.Equal(HttpStatusCode.NoContent, revoked.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await withNew.GetAsync("/me", Ct)).StatusCode);

        tokens = await admin.GetFromJsonAsync<JsonElement>("/tokens", Ct);
        Assert.Single(tokens.EnumerateArray(), t => t.GetProperty("revoked_at").ValueKind != JsonValueKind.Null);
    }

    private static async Task<string> AgentSecretAsync(HttpClient user)
    {
        using var created = await user.PostAsJsonAsync("/agents", new { }, Ct);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        return (await created.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("token").GetProperty("secret").GetString()!;
    }

    private static async Task<JsonElement> Problem(HttpResponseMessage response, HttpStatusCode status, string code, string? on = null)
    {
        Assert.True(status == response.StatusCode, $"{on}: expected {status}, got {response.StatusCode}");
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal($"/problems/{code}", problem.GetProperty("type").GetString());
        return problem;
    }
}
