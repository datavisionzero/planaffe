using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace Planaffe.IntegrationTests;

/// <summary>
/// The device-code login over HTTP (ADR 0025): the CLI's two anonymous calls,
/// the browser's two confirmed ones, and the five states a poll is answered
/// with.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class DeviceLoginEndpointTests(PostgresFixture postgres)
{
    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_confirmed_login_hands_over_a_user_token_once_and_it_authenticates()
    {
        await using var instance = await AnInstance.BootstrappedAsync(postgres);
        using var cli = instance.ClientWith(null);
        using var human = instance.ClientWith(AnInstance.BootstrapToken);

        var begun = await BeginAsync(cli);
        var code = begun.GetProperty("user_code").GetString()!;
        var deviceCode = begun.GetProperty("device_code").GetString()!;

        Assert.Matches("^[BCDFGHJKLMNPQRSTVWXZ]{4}-[BCDFGHJKLMNPQRSTVWXZ]{4}$", code);
        Assert.Equal("/device", begun.GetProperty("verification_uri").GetString());
        Assert.Equal($"/device?code={code}", begun.GetProperty("verification_uri_complete").GetString());
        Assert.Equal(600, begun.GetProperty("expires_in_seconds").GetInt32());
        Assert.Equal(5, begun.GetProperty("interval_seconds").GetInt32());
        Assert.DoesNotContain("pa_", deviceCode, StringComparison.Ordinal);

        // Until a human presses something, the poll is told the one code that
        // means keep asking.
        Assert.Equal("device-pending", await RefusalOf(cli, deviceCode, HttpStatusCode.Conflict));

        var waiting = await human.GetFromJsonAsync<JsonElement>($"/device-logins/{code}", Ct);
        Assert.Equal(code, waiting.GetProperty("user_code").GetString());

        using var approved = await human.PostAsJsonAsync($"/device-logins/{code}/decide", new { approve = true }, Ct);
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);

        using var redeemed = await cli.PostAsJsonAsync("/device-logins/redeem", new { device_code = deviceCode }, Ct);
        Assert.Equal(HttpStatusCode.OK, redeemed.StatusCode);
        var collected = await redeemed.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal(AnInstance.Administrator, collected.GetProperty("user").GetProperty("name").GetString());
        Assert.Equal("maintainer@example.test", collected.GetProperty("email").GetString());
        Assert.True(collected.GetProperty("administrator").GetBoolean());

        var secret = collected.GetProperty("token").GetProperty("secret").GetString()!;
        Assert.StartsWith("pa_", secret, StringComparison.Ordinal);

        // What it collected is an ordinary user token: it authenticates, it is
        // in the caller's own list, and nothing about it is a session.
        using var signedIn = instance.ClientWith(secret);
        var me = await signedIn.GetFromJsonAsync<JsonElement>("/me", Ct);
        Assert.Equal("user", me.GetProperty("kind").GetString());
        Assert.Equal(secret[..8], me.GetProperty("token").GetProperty("prefix").GetString());

        var tokens = await signedIn.GetFromJsonAsync<JsonElement>("/tokens", Ct);
        Assert.Contains(tokens.EnumerateArray(), token => token.GetProperty("prefix").GetString() == secret[..8]);

        // A device code works once.
        Assert.Equal("device-expired", await RefusalOf(cli, deviceCode, HttpStatusCode.Gone));
    }

    [Fact]
    public async Task A_refused_login_says_so_and_stays_refused()
    {
        await using var instance = await AnInstance.BootstrappedAsync(postgres);
        using var cli = instance.ClientWith(null);
        using var human = instance.ClientWith(AnInstance.BootstrapToken);

        var begun = await BeginAsync(cli);
        var code = begun.GetProperty("user_code").GetString()!;
        var deviceCode = begun.GetProperty("device_code").GetString()!;

        using var denied = await human.PostAsJsonAsync($"/device-logins/{code}/decide", new { approve = false }, Ct);
        Assert.Equal(HttpStatusCode.OK, denied.StatusCode);

        Assert.Equal("device-denied", await RefusalOf(cli, deviceCode, HttpStatusCode.Forbidden));

        // And nobody can change their mind afterwards.
        using var again = await human.PostAsJsonAsync($"/device-logins/{code}/decide", new { approve = true }, Ct);
        Assert.Equal(HttpStatusCode.Forbidden, again.StatusCode);
    }

    [Fact]
    public async Task A_login_nobody_confirmed_in_time_expires_and_an_expired_approval_hands_over_nothing()
    {
        await using var instance = await AnInstance.BootstrappedAsync(postgres);
        using var cli = instance.ClientWith(null);
        using var human = instance.ClientWith(AnInstance.BootstrapToken);

        var begun = await BeginAsync(cli);
        var code = begun.GetProperty("user_code").GetString()!;
        var deviceCode = begun.GetProperty("device_code").GetString()!;

        using var approved = await human.PostAsJsonAsync($"/device-logins/{code}/decide", new { approve = true }, Ct);
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);

        // The clock is the instance's, so the row is what moves: an approval
        // nobody collected in time is expired rather than approved, or a device
        // code left in a log would still be worth something an hour later.
        await using (var context = Migrated.ContextFor(instance.ConnectionString))
        {
            await context.DeviceLogins.ExecuteUpdateAsync(
                set => set.SetProperty(x => x.ExpiresAt, DateTimeOffset.UtcNow.AddMinutes(-1)), Ct);
        }

        Assert.Equal("device-expired", await RefusalOf(cli, deviceCode, HttpStatusCode.Gone));

        using var late = await human.GetAsync($"/device-logins/{code}", Ct);
        Assert.Equal(HttpStatusCode.Gone, late.StatusCode);
    }

    [Fact]
    public async Task A_code_nobody_issued_is_the_same_answer_as_one_that_never_existed()
    {
        await using var instance = await AnInstance.BootstrappedAsync(postgres);
        using var cli = instance.ClientWith(null);
        using var human = instance.ClientWith(AnInstance.BootstrapToken);

        using var unknown = await human.GetAsync("/device-logins/BCDF-GHJK", Ct);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);

        // Not a code at all takes the same road as one that is: nothing found.
        using var nonsense = await human.GetAsync("/device-logins/hello", Ct);
        Assert.Equal(HttpStatusCode.NotFound, nonsense.StatusCode);

        using var noSuchDevice = await cli.PostAsJsonAsync("/device-logins/redeem", new { device_code = "nothing" }, Ct);
        Assert.Equal(HttpStatusCode.NotFound, noSuchDevice.StatusCode);
    }

    [Fact]
    public async Task An_agent_may_neither_read_nor_confirm_a_login()
    {
        await using var instance = await AnInstance.BootstrappedAsync(postgres);
        using var cli = instance.ClientWith(null);
        using var human = instance.ClientWith(AnInstance.BootstrapToken);

        using var createdAgent = await human.PostAsJsonAsync("/agents", new { name = "quiet-otter-42" }, Ct);
        var agentSecret = (await createdAgent.Content.ReadFromJsonAsync<JsonElement>(Ct))
            .GetProperty("token").GetProperty("secret").GetString()!;
        using var agent = instance.ClientWith(agentSecret);

        var code = (await BeginAsync(cli)).GetProperty("user_code").GetString()!;

        using var read = await agent.GetAsync($"/device-logins/{code}", Ct);
        Assert.Equal(HttpStatusCode.Forbidden, read.StatusCode);

        using var decided = await agent.PostAsJsonAsync($"/device-logins/{code}/decide", new { approve = true }, Ct);
        Assert.Equal(HttpStatusCode.Forbidden, decided.StatusCode);
    }

    [Fact]
    public async Task The_typed_code_forgives_case_spaces_and_dashes()
    {
        await using var instance = await AnInstance.BootstrappedAsync(postgres);
        using var cli = instance.ClientWith(null);
        using var human = instance.ClientWith(AnInstance.BootstrapToken);

        var begun = await BeginAsync(cli);
        var code = begun.GetProperty("user_code").GetString()!;
        var typed = code.Replace("-", string.Empty, StringComparison.Ordinal).ToLowerInvariant();

        using var approved = await human.PostAsJsonAsync($"/device-logins/{typed}/decide", new { approve = true }, Ct);
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        Assert.Equal(code, (await approved.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("user_code").GetString());
    }

    [Fact]
    public async Task Deciding_needs_the_field_that_says_which_way()
    {
        await using var instance = await AnInstance.BootstrappedAsync(postgres);
        using var cli = instance.ClientWith(null);
        using var human = instance.ClientWith(AnInstance.BootstrapToken);

        var code = (await BeginAsync(cli)).GetProperty("user_code").GetString()!;

        using var empty = await human.PostAsJsonAsync($"/device-logins/{code}/decide", new { }, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
    }

    [Fact]
    public async Task A_token_revokes_itself_and_a_browser_session_is_told_where_its_own_exit_is()
    {
        await using var instance = await AnInstance.BootstrappedAsync(postgres);
        using var admin = instance.ClientWith(AnInstance.BootstrapToken);
        var secret = await instance.AddActiveUserAsync("second");
        using var user = instance.ClientWith(secret);

        using var revoked = await user.DeleteAsync("/me/token", Ct);
        Assert.Equal(HttpStatusCode.NoContent, revoked.StatusCode);

        using var afterwards = await user.GetAsync("/me", Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, afterwards.StatusCode);

        // Revoking a revoked token changes nothing — but the token no longer
        // authenticates, so it is the administrator who asks again.
        using var stillThere = await admin.GetAsync("/tokens", Ct);
        Assert.Equal(HttpStatusCode.OK, stillThere.StatusCode);

        using var exchange = await instance.ClientWith(null).PostAsJsonAsync("/session/bootstrap",
            new { token = AnInstance.BootstrapToken, password = "a long first password" }, Ct);
        var cookie = exchange.Headers.GetValues("Set-Cookie").Single().Split(';')[0];
        using var browser = new HttpRequestMessage(HttpMethod.Delete, "/me/token");
        browser.Headers.Add("Cookie", cookie);
        browser.Headers.Add("Origin", "http://localhost");
        browser.Headers.Add("X-Planaffe-CSRF", "1");
        using var refused = await instance.ClientWith(null).SendAsync(browser, Ct);
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
    }

    private static async Task<JsonElement> BeginAsync(HttpClient cli)
    {
        using var begun = await cli.PostAsJsonAsync("/device-logins", new { }, Ct);
        Assert.Equal(HttpStatusCode.OK, begun.StatusCode);
        return await begun.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }

    private static async Task<string?> RefusalOf(HttpClient cli, string deviceCode, HttpStatusCode expected)
    {
        using var polled = await cli.PostAsJsonAsync("/device-logins/redeem", new { device_code = deviceCode }, Ct);
        Assert.Equal(expected, polled.StatusCode);
        var problem = await polled.Content.ReadFromJsonAsync<JsonElement>(Ct);
        return problem.GetProperty("type").GetString()?.Split('/')[^1];
    }
}
