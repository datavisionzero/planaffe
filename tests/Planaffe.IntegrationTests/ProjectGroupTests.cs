using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
namespace Planaffe.IntegrationTests;

[Collection(nameof(PostgresCollection))]
public sealed class ProjectGroupTests(PostgresFixture postgres)
{
    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;
    [Fact]
    public async Task Groups_round_trip_and_dissolving_preserves_projects_and_access()
    {
        await using var instance = await AnInstance.BootstrappedAsync(postgres);
        using var admin = instance.ClientWith(AnInstance.BootstrapToken);
        using var user = instance.ClientWith(await instance.AddActiveUserAsync("reader"));
        var response = await admin.PostAsJsonAsync("/project-groups", new { name = "Products", description = "Our products" }, Ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var group = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        var id = group.GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/project-groups", new { name = "Products" }, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await user.PostAsJsonAsync("/project-groups", new { name = "Other" }, Ct)).StatusCode);
        Assert.Empty((await user.GetFromJsonAsync<JsonElement>("/project-groups", Ct)).EnumerateArray());
        await admin.PostAsJsonAsync("/projects", new { key = "APP", name = "Application" }, Ct);
        Assert.Equal(HttpStatusCode.OK, (await admin.PatchAsJsonAsync("/projects/APP", new { group_id = id }, Ct)).StatusCode);
        Assert.Equal(id, (await admin.GetFromJsonAsync<JsonElement>("/projects/APP", Ct)).GetProperty("group_id").GetGuid());
        Assert.Equal(HttpStatusCode.NotFound, (await user.GetAsync("/projects/APP", Ct)).StatusCode);
        var users = await admin.GetFromJsonAsync<JsonElement>("/users", Ct);
        var reader = users.EnumerateArray().Single(u => u.GetProperty("name").GetString() == "reader").GetProperty("id").GetGuid();
        await admin.PutAsync($"/projects/APP/users/{reader}", null, Ct);
        Assert.Single((await user.GetFromJsonAsync<JsonElement>("/project-groups", Ct)).EnumerateArray());
        Assert.Equal(HttpStatusCode.Forbidden, (await user.PatchAsJsonAsync("/projects/APP", new { group_id = (Guid?)null }, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PatchAsJsonAsync("/projects/APP", new { group_id = Guid.NewGuid() }, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PatchAsJsonAsync("/projects/APP", new { group_id = 42 }, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync($"/project-groups/{id}", new { name = "Renamed", description = "New description" }, Ct)).StatusCode);
        await admin.PatchAsJsonAsync("/projects/APP", new { name = "Still grouped" }, Ct);
        Assert.Equal(id, (await user.GetFromJsonAsync<JsonElement>("/projects/APP", Ct)).GetProperty("group_id").GetGuid());
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/project-groups/{id}", Ct)).StatusCode);
        var after = await user.GetFromJsonAsync<JsonElement>("/projects/APP", Ct);
        Assert.Equal("Still grouped", after.GetProperty("name").GetString());
        Assert.Equal(JsonValueKind.Null, after.GetProperty("group_id").ValueKind);
    }
}
