using System.Net.Http.Json;
using System.Text.Json;

namespace Planaffe.IntegrationTests;

/// <summary>
/// Whose address a path is when the application and the instance both stand on
/// it — <c>/spaces</c>, <c>/projects</c>, <c>/admin/projects</c>. The header a
/// browser sends on a navigation is what decides, and nothing else changes
/// (PLAN-10).
/// </summary>
/// <remarks>
/// What a navigation gets is the built application, which is not in this
/// process: a test host has no <c>wwwroot</c> unless somebody built one beside
/// it. The assertions therefore cover what holds either way: a navigation is
/// never answered with the API's JSON, and its response cannot poison the
/// cached JSON representation of the same address.
/// </remarks>
[Collection(nameof(PostgresCollection))]
public sealed class BrowserNavigationTests(PostgresFixture postgres)
{
    [Theory]
    [InlineData("/spaces")]
    [InlineData("/projects")]
    [InlineData("/admin/projects")]
    [InlineData("/PLAN/releases/0.11.0")]
    public async Task A_browser_navigation_is_the_applications_and_not_the_instances(string path)
    {
        await using var instance = await AnInstance.BootstrappedAsync(postgres);
        using var client = instance.ClientWith(AnInstance.BootstrapToken);

        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("Sec-Fetch-Dest", "document");
        request.Headers.Add("Accept", "text/html,application/xhtml+xml");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.NotEqual("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("no-cache", response.Headers.CacheControl?.ToString());
        Assert.Contains("Accept", response.Headers.Vary);
    }

    [Fact]
    public async Task The_same_address_is_the_instances_for_everything_that_is_not_one()
    {
        await using var instance = await AnInstance.BootstrappedAsync(postgres);
        using var client = instance.ClientWith(AnInstance.BootstrapToken);

        var spaces = await client.GetFromJsonAsync<JsonElement>("/spaces", TestContext.Current.CancellationToken);

        Assert.Equal(JsonValueKind.Array, spaces.ValueKind);
    }

    /// <summary>
    /// The contract stays reachable from a browser tab: it has an extension,
    /// and a path with one is an asset rather than a screen.
    /// </summary>
    [Fact]
    public async Task The_contract_is_still_the_instances_under_a_browsers_headers()
    {
        await using var instance = await AnInstance.BootstrappedAsync(postgres);
        using var client = instance.ClientWith(null);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/openapi/v1.json");
        request.Headers.Add("Sec-Fetch-Dest", "document");
        request.Headers.Add("Accept", "text/html,application/xhtml+xml");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        var document = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

        Assert.True(document.TryGetProperty("openapi", out _));
    }
}
