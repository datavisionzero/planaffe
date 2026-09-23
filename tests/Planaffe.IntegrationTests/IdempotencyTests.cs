using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Planaffe.Infrastructure.Persistence;

namespace Planaffe.IntegrationTests;

/// <summary>
/// <c>Idempotency-Key</c> on every write (<c>docs/api.md</c>): a replay is
/// answered from the store and creates nothing, a reuse for another request is
/// refused, and keys of different identities never meet.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class IdempotencyTests(PostgresFixture postgres)
{
    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_bulk_create_replayed_with_the_same_key_returns_the_same_issues_and_creates_nothing()
    {
        await using var instance = await AnInstance.BootstrappedAsync(postgres);
        using var admin = await Project(instance);
        var body = new { project = "PLAN", issues = Enumerable.Range(1, 7).Select(i => new { title = $"Issue {i}" }).ToArray() };

        using var first = await Send(admin, HttpMethod.Post, "/issues", body, "create-7");
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var firstBody = await first.Content.ReadAsStringAsync(Ct);

        using var replay = await Send(admin, HttpMethod.Post, "/issues", body, "create-7");
        Assert.Equal(HttpStatusCode.Created, replay.StatusCode);
        // The same answer — structurally: the store is jsonb, which spells its JSON its own way.
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(firstBody), JsonNode.Parse(await replay.Content.ReadAsStringAsync(Ct))));
        Assert.Equal("true", Assert.Single(replay.Headers.GetValues("Idempotent-Replayed")));

        var listed = await admin.GetFromJsonAsync<JsonElement>("/issues?project=PLAN", Ct);
        Assert.Equal(7, listed.GetProperty("total").GetInt32());

        // The same key with a different body is a reuse, not a replay.
        var mismatch = await ProjectEndpointTests.Problem(
            await Send(admin, HttpMethod.Post, "/issues", new { project = "PLAN", issues = new[] { new { title = "Other" } } }, "create-7"),
            HttpStatusCode.Conflict, "idempotency-mismatch");
        Assert.Contains("create-7", mismatch.GetProperty("detail").GetString(), StringComparison.Ordinal);

        // A fresh key creates again.
        using var fresh = await Send(admin, HttpMethod.Post, "/issues", body, "create-7-again");
        Assert.Equal(HttpStatusCode.Created, fresh.StatusCode);
        Assert.Equal(14, (await admin.GetFromJsonAsync<JsonElement>("/issues?project=PLAN", Ct)).GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Next_replayed_with_the_same_key_hands_out_the_same_issue_and_claims_no_second()
    {
        await using var instance = await AnInstance.BootstrappedAsync(postgres);
        using var admin = await Project(instance);
        await admin.PostAsJsonAsync("/issues", new { project = "PLAN", issues = new[] { new { title = "A" }, new { title = "B" } } }, Ct);
        using var createdAgent = await admin.PostAsJsonAsync("/agents", new { name = "one" }, Ct);
        using var agent = instance.ClientWith((await createdAgent.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("token").GetProperty("secret").GetString());

        using var first = await Send(agent, HttpMethod.Post, "/projects/PLAN/next", new { }, "run-1");
        var issue = (await first.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("issue").GetProperty("key").GetString();
        using var replay = await Send(agent, HttpMethod.Post, "/projects/PLAN/next", new { }, "run-1");
        Assert.Equal(issue, (await replay.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("issue").GetProperty("key").GetString());

        var held = await admin.GetFromJsonAsync<JsonElement>("/issues?project=PLAN&claimed=true", Ct);
        Assert.Equal(1, held.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Keys_of_different_identities_never_meet_and_a_refusal_is_replayed_too()
    {
        await using var instance = await AnInstance.BootstrappedAsync(postgres);
        using var admin = await Project(instance);
        using var other = instance.ClientWith(await instance.AddActiveUserAsync("other"));
        var users = await admin.GetFromJsonAsync<JsonElement>("/users", Ct);
        var otherId = users.EnumerateArray().Single(user => user.GetProperty("name").GetString() == "other").GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsync($"/projects/PLAN/users/{otherId}", null, Ct)).StatusCode);

        using var mine = await Send(admin, HttpMethod.Post, "/issues", new { project = "PLAN", issues = new[] { new { title = "Mine" } } }, "shared-key");
        using var theirs = await Send(other, HttpMethod.Post, "/issues", new { project = "PLAN", issues = new[] { new { title = "Theirs" } } }, "shared-key");
        Assert.Equal(HttpStatusCode.Created, mine.StatusCode);
        Assert.Equal(HttpStatusCode.Created, theirs.StatusCode);
        Assert.Equal(2, (await admin.GetFromJsonAsync<JsonElement>("/issues?project=PLAN", Ct)).GetProperty("total").GetInt32());

        // A refused write is kept and replayed as the same refusal.
        var bad = new { project = "PLAN", issues = new[] { new { title = "" } } };
        await ProjectEndpointTests.Problem(await Send(admin, HttpMethod.Post, "/issues", bad, "bad-key"), HttpStatusCode.BadRequest, "validation");
        using var replayed = await Send(admin, HttpMethod.Post, "/issues", bad, "bad-key");
        Assert.Equal(HttpStatusCode.BadRequest, replayed.StatusCode);
        Assert.Equal("application/problem+json", replayed.Content.Headers.ContentType?.MediaType);
        Assert.Equal("true", Assert.Single(replayed.Headers.GetValues("Idempotent-Replayed")));

        // A key on a read is ignored; an overlong key is refused; a key without a caller changes nothing about the 401.
        using var read = await Send(admin, HttpMethod.Get, "/issues?project=PLAN", null, "shared-key");
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        await ProjectEndpointTests.Problem(await Send(admin, HttpMethod.Post, "/issues", bad, new string('k', 201)), HttpStatusCode.BadRequest, "validation");
        using var anonymous = instance.ClientWith(null);
        using var refused = await Send(anonymous, HttpMethod.Post, "/issues", bad, "no-caller");
        Assert.Equal(HttpStatusCode.Unauthorized, refused.StatusCode);
    }

    /// <summary>
    /// Two <c>next</c> with one key at the same moment — a client that retried
    /// while the first was still being answered — are one request: one runs,
    /// the other waits for its answer and replays it, and one issue is claimed.
    /// </summary>
    [Fact]
    public async Task Two_concurrent_nexts_with_the_same_key_claim_exactly_one_issue()
    {
        await using var instance = await AnInstance.BootstrappedAsync(postgres);
        using var admin = await Project(instance);
        await admin.PostAsJsonAsync("/issues", new { project = "PLAN", issues = Enumerable.Range(1, 5).Select(i => new { title = $"Issue {i}" }).ToArray() }, Ct);
        using var createdAgent = await admin.PostAsJsonAsync("/agents", new { name = "one" }, Ct);
        using var agent = instance.ClientWith((await createdAgent.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("token").GetProperty("secret").GetString());

        for (var round = 0; round < 3; round++)
        {
            var answers = await Task.WhenAll(Enumerable.Range(0, 4).Select(async _ =>
            {
                using var response = await Send(agent, HttpMethod.Post, "/projects/PLAN/next", new { }, $"twins-{round}");
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                return (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("issue").GetProperty("key").GetString();
            }));

            Assert.Single(answers.Distinct());
            var held = await admin.GetFromJsonAsync<JsonElement>("/issues?project=PLAN&claimed=true", Ct);
            Assert.Equal(round + 1, held.GetProperty("total").GetInt32());
        }
    }

    [Fact]
    public async Task A_replay_carries_the_location_of_the_first_answer()
    {
        await using var instance = await AnInstance.BootstrappedAsync(postgres);
        using var admin = await Project(instance);

        using var first = await Send(admin, HttpMethod.Post, "/epics", new { project = "PLAN", title = "Theme" }, "epic-1");
        using var replay = await Send(admin, HttpMethod.Post, "/epics", new { project = "PLAN", title = "Theme" }, "epic-1");
        Assert.Equal(HttpStatusCode.Created, replay.StatusCode);
        Assert.Equal("true", Assert.Single(replay.Headers.GetValues("Idempotent-Replayed")));
        Assert.NotNull(first.Headers.Location);
        Assert.Equal(first.Headers.Location, replay.Headers.Location);
    }

    /// <summary>
    /// A pending row is a request still running, and its twin is told so once
    /// it has waited long enough; the row of a request that can no longer be
    /// running — its process went away — gives its key back.
    /// </summary>
    [Fact]
    public async Task A_pending_twin_is_told_so_and_an_abandoned_pending_row_gives_its_key_back()
    {
        await using var instance = await AnInstance.BootstrappedAsync(postgres);
        using var admin = await Project(instance);
        const string body = """{"project":"PLAN","title":"Theme"}""";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes("POST\n/epics\n" + body));

        await using (var context = Migrated.ContextFor(instance.ConnectionString))
        {
            var bootstrap = (await context.Users.SingleAsync(Ct)).Id;
            context.Idempotency.Add(IdempotencyRecord.Of(bootstrap, "running", hash, null, null, DateTimeOffset.UtcNow));
            context.Idempotency.Add(IdempotencyRecord.Of(bootstrap, "gone", hash, null, null, DateTimeOffset.UtcNow.AddHours(-2)));
            await context.SaveChangesAsync(Ct);
        }

        await ProjectEndpointTests.Problem(await SendRaw(admin, "/epics", body, "running"), HttpStatusCode.Conflict, "idempotency-pending");

        using var taken = await SendRaw(admin, "/epics", body, "gone");
        Assert.Equal(HttpStatusCode.Created, taken.StatusCode);
        using var replay = await SendRaw(admin, "/epics", body, "gone");
        Assert.Equal("true", Assert.Single(replay.Headers.GetValues("Idempotent-Replayed")));
    }

    private static Task<HttpResponseMessage> SendRaw(HttpClient client, string url, string body, string key)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        request.Headers.TryAddWithoutValidation("Idempotency-Key", key);
        return client.SendAsync(request, Ct);
    }

    /// <summary>
    /// A secret shown once is shown once: the store keeps the status of the
    /// answer that carried it and not the answer, so that nothing a copy of the
    /// database holds signs anybody in, and a replay is told it is too late
    /// rather than handed a second copy.
    /// </summary>
    [Fact]
    public async Task A_secret_shown_once_is_not_kept_and_its_replay_is_refused()
    {
        await using var instance = await AnInstance.ConfiguredAsync(postgres, BrowserIdentityEndpointTests.WithoutSmtp);
        using var admin = instance.ClientWith(AnInstance.BootstrapToken);
        using var invited = await admin.PostAsJsonAsync("/users", new { name = "invited", email = "invited@example.test" }, Ct);
        Assert.Equal(HttpStatusCode.Created, invited.StatusCode);
        await instance.AddActiveUserAsync("active");

        (string Path, object? Body)[] writes =
        [
            ("/tokens", null),
            ("/agents", new { name = "one" }),
            ("/agents/one/token", null),
            ("/users/invited/invitation-link", null),
            ("/users/active/recovery-link", null),
            ("/device-logins", null),
        ];

        var shown = new List<string>();
        foreach (var (path, body) in writes)
        {
            using var first = await Send(admin, HttpMethod.Post, path, body ?? new { }, $"once{path}");
            Assert.True(first.IsSuccessStatusCode, $"{path}: {first.StatusCode} {await first.Content.ReadAsStringAsync(Ct)}");
            shown.AddRange(Strings(JsonNode.Parse(await first.Content.ReadAsStringAsync(Ct))).Where(value => value.Length >= 20));

            var refused = await ProjectEndpointTests.Problem(
                await Send(admin, HttpMethod.Post, path, body ?? new { }, $"once{path}"),
                HttpStatusCode.Conflict, "already-shown");
            Assert.Contains($"once{path}", refused.GetProperty("detail").GetString(), StringComparison.Ordinal);
        }

        await using var context = Migrated.ContextFor(instance.ConnectionString);
        var rows = await context.Idempotency.Where(r => r.Key.StartsWith("once/")).ToListAsync(Ct);
        Assert.Equal(writes.Length, rows.Count);
        Assert.All(rows, row => Assert.True(row.Withheld && row.Body is null, row.Key));

        var stored = await context.Database.SqlQueryRaw<string>("select coalesce(body::text, '') as \"Value\" from idempotency").ToListAsync(Ct);
        Assert.NotEmpty(shown);
        Assert.DoesNotContain(shown, secret => stored.Any(body => body.Contains(secret, StringComparison.Ordinal)));
    }

    [Fact]
    public async Task A_refusal_of_a_write_that_would_show_a_secret_is_kept_and_replayed()
    {
        await using var instance = await AnInstance.BootstrappedAsync(postgres);
        using var admin = instance.ClientWith(AnInstance.BootstrapToken);

        await ProjectEndpointTests.Problem(await Send(admin, HttpMethod.Post, "/agents/nobody/token", new { }, "rotate-nobody"), HttpStatusCode.NotFound, "not-found");
        using var replayed = await Send(admin, HttpMethod.Post, "/agents/nobody/token", new { }, "rotate-nobody");
        Assert.Equal(HttpStatusCode.NotFound, replayed.StatusCode);
        Assert.Equal("true", Assert.Single(replayed.Headers.GetValues("Idempotent-Replayed")));
    }

    private static IEnumerable<string> Strings(JsonNode? node) => node switch
    {
        JsonObject o => o.SelectMany(p => Strings(p.Value)),
        JsonArray a => a.SelectMany(Strings),
        JsonValue v when v.TryGetValue<string>(out var text) => [text],
        _ => [],
    };

    private static Task<HttpResponseMessage> Send(HttpClient client, HttpMethod method, string url, object? body, string key)
    {
        var request = new HttpRequestMessage(method, url) { Content = body is null ? null : JsonContent.Create(body) };
        request.Headers.TryAddWithoutValidation("Idempotency-Key", key);
        return client.SendAsync(request, Ct);
    }

    private static async Task<HttpClient> Project(AnInstance instance)
    {
        var admin = instance.ClientWith(AnInstance.BootstrapToken);
        using var project = await admin.PostAsJsonAsync("/projects", new { key = "PLAN", name = "planaffe" }, Ct);
        Assert.Equal(HttpStatusCode.Created, project.StatusCode);
        return admin;
    }
}
