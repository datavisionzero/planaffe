using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Planaffe.IntegrationTests;

/// <summary>
/// A <c>PATCH</c> body nobody can read is the caller's mistake, and the
/// answer says so: 400 <c>validation</c>, never the 500 <c>internal</c> of an
/// exception that escaped the parser — whether the endpoint reads the document
/// by hand or binds it through a converter.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class PatchBodyTests(PostgresFixture postgres)
{
    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task An_empty_a_broken_or_a_wrongly_typed_body_is_validation_on_every_patch()
    {
        await using var instance = await AnInstance.BootstrappedAsync(postgres);
        using var admin = instance.ClientWith(AnInstance.BootstrapToken);
        Assert.Equal(HttpStatusCode.Created, (await admin.PostAsJsonAsync("/projects", new { key = "PLAN", name = "planaffe" }, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await admin.PostAsJsonAsync("/issues", new { project = "PLAN", issues = new[] { new { title = "One" } } }, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await admin.PostAsJsonAsync("/epics", new { project = "PLAN", title = "Theme" }, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await admin.PostAsJsonAsync("/projects/PLAN/labels", new { name = "area:api" }, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await admin.PostAsJsonAsync("/spaces", new { name = "handbuch", title = "Handbuch" }, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await admin.PostAsJsonAsync("/spaces/handbuch/pages", new { slug = "company", title = "Company" }, Ct)).StatusCode);

        string[] paths = ["/issues/PLAN-1", "/issues", "/epics/PLAN-E1", "/projects/PLAN", "/projects/PLAN/labels/area:api", "/spaces/handbuch/pages/company"];
        string[] bodies = ["", "{", "not json", "[1, 2]", "42"];

        foreach (var path in paths)
        {
            foreach (var body in bodies)
            {
                await Refused(admin, path, body);
            }
        }

        await Refused(admin, "/issues/PLAN-1", """{ "priority": 1.5 }""", "priority");
        await Refused(admin, "/issues", """{ "keys": ["PLAN-1"], "changes": { "priority": 1.5 } }""", "priority");
        Assert.DoesNotContain(instance.Errors, error => error.StartsWith("Unhandled exception", StringComparison.Ordinal));

        // And a body that is fine still is.
        using var fine = await Patch(admin, "/issues/PLAN-1", """{ "priority": 3 }""");
        Assert.Equal(HttpStatusCode.OK, fine.StatusCode);
    }

    private static async Task Refused(HttpClient client, string path, string body, string field = "body")
    {
        using var response = await Patch(client, path, body);
        var text = await response.Content.ReadAsStringAsync(Ct);
        Assert.True(response.StatusCode == HttpStatusCode.BadRequest, $"PATCH {path} with '{body}': {response.StatusCode} {text}");
        var problem = JsonDocument.Parse(text).RootElement;
        Assert.Equal("/problems/validation", problem.GetProperty("type").GetString());
        Assert.True(problem.GetProperty("errors").TryGetProperty(field, out _), $"PATCH {path} with '{body}': {text}");
    }

    private static Task<HttpResponseMessage> Patch(HttpClient client, string path, string body) =>
        client.PatchAsync(path, new StringContent(body, Encoding.UTF8, "application/json"), Ct);
}
