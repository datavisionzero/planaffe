using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Planaffe.IntegrationTests;

/// <summary>
/// Two writers racing for one row, where the second has to see what the first
/// did. Each act reads the row before its transaction and again under the
/// lock; the second read is the one every guard runs against, and these fail
/// when it answers with the copy from before the lock. A race is not
/// scheduled, so each case runs several rounds, and a single round that let
/// both writers through is enough.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class LostUpdateTests(PostgresFixture postgres)
{
    private const int Rounds = 8;

    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Two_guarded_edits_of_one_epic_let_exactly_one_through()
    {
        await using var instance = await AnInstance.BootstrappedAsync(postgres);
        using var admin = await Project(instance);
        using var agent = await Agent(instance, admin);
        await admin.PostAsJsonAsync("/epics", new { project = "PLAN", title = "Backend", description = "v0" }, Ct);

        for (var round = 1; round <= Rounds; round++)
        {
            var version = (await admin.GetFromJsonAsync<JsonElement>("/epics/PLAN-E1", Ct)).GetProperty("updated_at").GetString()!;

            var answers = await Task.WhenAll(
                Guarded(admin, "/epics/PLAN-E1", version, new { description = $"admin {round}" }),
                Guarded(agent, "/epics/PLAN-E1", version, new { description = $"agent {round}" }));

            await OneWinsOneIsStale(answers);
        }
    }

    [Fact]
    public async Task Two_guarded_edits_of_one_page_let_exactly_one_through()
    {
        await using var instance = await AnInstance.BootstrappedAsync(postgres);
        using var admin = instance.ClientWith(AnInstance.BootstrapToken);
        await admin.PostAsJsonAsync("/spaces", new { name = "handbuch", title = "Handbuch" }, Ct);
        await admin.PostAsJsonAsync("/spaces/handbuch/pages", new { slug = "company", title = "Company", body = "v0" }, Ct);

        for (var round = 1; round <= Rounds; round++)
        {
            var version = (await admin.GetFromJsonAsync<JsonElement>("/spaces/handbuch/pages/company", Ct)).GetProperty("updated_at").GetString()!;

            var answers = await Task.WhenAll(
                Guarded(admin, "/spaces/handbuch/pages/company", version, new { body = $"first {round}" }),
                Guarded(admin, "/spaces/handbuch/pages/company", version, new { body = $"second {round}" }));

            await OneWinsOneIsStale(answers);
        }
    }

    [Fact]
    public async Task Two_answers_to_one_question_leave_the_first_standing()
    {
        await using var instance = await AnInstance.BootstrappedAsync(postgres);
        using var admin = await Project(instance);
        using var agent = await Agent(instance, admin);
        await Issues(admin, new { title = "A" });

        for (var round = 1; round <= Rounds; round++)
        {
            using var asked = await agent.PostAsJsonAsync("/issues/PLAN-1/questions", new { question = $"Which one, {round}?" }, Ct);
            var id = (await asked.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid();

            var answers = await Task.WhenAll(
                admin.PostAsJsonAsync($"/questions/{id}/answer", new { answer = "left" }, Ct),
                admin.PostAsJsonAsync($"/questions/{id}/answer", new { answer = "right" }, Ct));

            var won = Assert.Single(answers, a => a.StatusCode == HttpStatusCode.OK);
            var lost = Assert.Single(answers, a => a.StatusCode != HttpStatusCode.OK);
            await ProjectEndpointTests.Problem(lost, HttpStatusCode.UnprocessableEntity, "transition");

            var stands = (await won.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("answer").GetString();
            var read = await admin.GetFromJsonAsync<JsonElement>($"/questions/{id}", Ct);
            Assert.Equal(stands, read.GetProperty("answer").GetString());
            Dispose(answers);
        }
    }

    [Fact]
    public async Task Two_withdrawals_of_one_comment_are_one_withdrawal_and_one_not_found()
    {
        await using var instance = await AnInstance.BootstrappedAsync(postgres);
        using var admin = await Project(instance);
        await Issues(admin, new { title = "A" });

        for (var round = 1; round <= Rounds; round++)
        {
            using var written = await admin.PostAsJsonAsync("/issues/PLAN-1/comments", new { body = $"Noise {round}." }, Ct);
            var id = (await written.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid();

            var answers = await Task.WhenAll(
                admin.DeleteAsync($"/comments/{id}", Ct),
                admin.DeleteAsync($"/comments/{id}", Ct));

            Assert.Single(answers, a => a.StatusCode == HttpStatusCode.NoContent);
            await ProjectEndpointTests.Problem(
                Assert.Single(answers, a => a.StatusCode != HttpStatusCode.NoContent), HttpStatusCode.NotFound, "not-found");
            Dispose(answers);
        }
    }

    private static Task<HttpResponseMessage> Guarded(HttpClient client, string url, string version, object body)
    {
        var request = new HttpRequestMessage(HttpMethod.Patch, url) { Content = JsonContent.Create(body) };
        request.Headers.TryAddWithoutValidation("If-Match", $"\"{version}\"");
        return client.SendAsync(request, Ct);
    }

    private static async Task OneWinsOneIsStale(HttpResponseMessage[] answers)
    {
        Assert.Single(answers, a => a.StatusCode == HttpStatusCode.OK);
        await ProjectEndpointTests.Problem(
            Assert.Single(answers, a => a.StatusCode != HttpStatusCode.OK), HttpStatusCode.PreconditionFailed, "stale");
        Dispose(answers);
    }

    private static void Dispose(HttpResponseMessage[] answers)
    {
        foreach (var answer in answers)
        {
            answer.Dispose();
        }
    }

    private static async Task<HttpClient> Project(AnInstance instance)
    {
        var admin = instance.ClientWith(AnInstance.BootstrapToken);
        using var project = await admin.PostAsJsonAsync("/projects", new { key = "PLAN", name = "planaffe" }, Ct);
        Assert.Equal(HttpStatusCode.Created, project.StatusCode);
        return admin;
    }

    private static async Task Issues(HttpClient admin, params object[] issues)
    {
        using var created = await admin.PostAsJsonAsync("/issues", new { project = "PLAN", issues }, Ct);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
    }

    private static async Task<HttpClient> Agent(AnInstance instance, HttpClient admin)
    {
        using var created = await admin.PostAsJsonAsync("/agents", new { name = "one" }, Ct);
        return instance.ClientWith((await created.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("token").GetProperty("secret").GetString());
    }
}
