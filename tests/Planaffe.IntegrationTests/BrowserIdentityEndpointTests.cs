using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Planaffe.IntegrationTests;

[Collection(nameof(PostgresCollection))]
public sealed class BrowserIdentityEndpointTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Bootstrap_token_becomes_a_cookie_session_once_and_password_sign_in_works()
    {
        await using var instance = await AnInstance.BootstrappedAsync(postgres);
        using var client = instance.ClientWith(null);

        using var exchange = await client.PostAsJsonAsync("/session/bootstrap", new
        {
            token = AnInstance.BootstrapToken,
            password = "a long first password",
        }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, exchange.StatusCode);
        var cookie = exchange.Headers.GetValues("Set-Cookie").Single().Split(';')[0];

        using var meRequest = new HttpRequestMessage(HttpMethod.Get, "/me");
        meRequest.Headers.Add("Cookie", cookie);
        using var me = await client.SendAsync(meRequest, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        Assert.Null((await me.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken)).GetProperty("token").GetString());

        using var repeated = await client.PostAsJsonAsync("/session/bootstrap", new
        {
            token = AnInstance.BootstrapToken,
            password = "another long password",
        }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, repeated.StatusCode);

        using var login = await client.PostAsJsonAsync("/session", new
        {
            email = "maintainer@example.test",
            password = "a long first password",
        }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
    }

    [Fact]
    public async Task Cookie_writes_need_the_csrf_header_and_origin_but_bearer_writes_do_not()
    {
        await using var instance = await AnInstance.BootstrappedAsync(postgres);
        using var client = instance.ClientWith(null);
        using var exchange = await client.PostAsJsonAsync("/session/bootstrap", new { token = AnInstance.BootstrapToken, password = "a long first password" }, TestContext.Current.CancellationToken);
        var cookie = exchange.Headers.GetValues("Set-Cookie").Single().Split(';')[0];

        using var refused = new HttpRequestMessage(HttpMethod.Delete, "/session");
        refused.Headers.Add("Cookie", cookie);
        using var refusedResponse = await client.SendAsync(refused, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, refusedResponse.StatusCode);

        using var accepted = new HttpRequestMessage(HttpMethod.Delete, "/session");
        accepted.Headers.Add("Cookie", cookie); accepted.Headers.Add("Origin", "http://localhost:5173"); accepted.Headers.Add("X-Planaffe-CSRF", "1");
        using var acceptedResponse = await client.SendAsync(accepted, TestContext.Current.CancellationToken);
        Assert.True(acceptedResponse.StatusCode == HttpStatusCode.NoContent,
            $"{acceptedResponse.StatusCode}: {await acceptedResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)}");
    }

    /// <summary>
    /// The instance without SMTP, which ADR 0018 says is an ordinary one: a
    /// second person is added and gets in, and then gets back in after
    /// forgetting their password. Both used to be impossible — `POST /users`
    /// refused outright, and `POST /password-recovery` is the only way to a
    /// new password and needs an email to carry it. Whoever forgot theirs was
    /// locked out for good, and an administrator could do nothing about it.
    /// </summary>
    [Fact]
    public async Task Without_smtp_an_administrator_hands_over_the_links_themselves()
    {
        await using var instance = await AnInstance.ConfiguredAsync(postgres, WithoutSmtp);
        using var admin = instance.ClientWith(AnInstance.BootstrapToken);
        using var browser = instance.ClientWith(null);

        using var invited = await admin.PostAsJsonAsync("/users",
            new { name = "other", email = "other@example.test" }, Ct);
        Assert.Equal(HttpStatusCode.Created, invited.StatusCode);

        // No public URL is set either, so the instance says what it knows: the
        // path, for whoever asked to resolve against the address they reached
        // it at. It never invents a host out of a request header.
        var invitation = await Link(admin, "/users/other/invitation-link");
        Assert.StartsWith("/activate?secret=", invitation);

        using var accepted = await browser.PostAsJsonAsync("/invitations/accept",
            new { secret = SecretIn(invitation), password = "the first long password" }, Ct);
        Assert.Equal(HttpStatusCode.NoContent, accepted.StatusCode);

        using var signedIn = await browser.PostAsJsonAsync("/session",
            new { email = "other@example.test", password = "the first long password" }, Ct);
        Assert.Equal(HttpStatusCode.NoContent, signedIn.StatusCode);

        // And the half this ticket is named after: the password is forgotten,
        // there is no email to recover it with, and the administrator issues
        // the same secret an email would have carried rather than setting a
        // password nobody but its owner should ever know.
        using var selfService = await browser.PostAsJsonAsync("/password-recovery",
            new { email = "other@example.test" }, Ct);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, selfService.StatusCode);

        var recovery = await Link(admin, "/users/other/recovery-link");
        Assert.StartsWith("/recover?secret=", recovery);

        using var completed = await browser.PostAsJsonAsync("/password-recovery/complete",
            new { secret = SecretIn(recovery), password = "the replacement password" }, Ct);
        Assert.Equal(HttpStatusCode.NoContent, completed.StatusCode);

        using var again = await browser.PostAsJsonAsync("/session",
            new { email = "other@example.test", password = "the replacement password" }, Ct);
        Assert.Equal(HttpStatusCode.NoContent, again.StatusCode);
    }

    /// <summary>
    /// The link is a state and not a favour: an invited user has an invitation
    /// and no password to recover, and an active one is the other way round.
    /// A link for the wrong one would be issued, replace a live secret, and
    /// then be refused by the screen it leads to.
    /// </summary>
    [Fact]
    public async Task A_link_is_refused_for_a_user_the_purpose_does_not_fit()
    {
        await using var instance = await AnInstance.ConfiguredAsync(postgres, WithoutSmtp);
        using var admin = instance.ClientWith(AnInstance.BootstrapToken);
        await admin.PostAsJsonAsync("/users", new { name = "other", email = "other@example.test" }, Ct);

        using var recovery = await admin.PostAsync("/users/other/recovery-link", null, Ct);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, recovery.StatusCode);

        using var invitation = await admin.PostAsync($"/users/{AnInstance.Administrator}/invitation-link", null, Ct);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, invitation.StatusCode);

        using var absent = await admin.PostAsync("/users/nobody/recovery-link", null, Ct);
        Assert.Equal(HttpStatusCode.NotFound, absent.StatusCode);
    }

    /// <summary>
    /// With a public URL the instance does know its address, and says the whole
    /// link — an operator pasting it into a chat window wants the link and not
    /// a path.
    /// </summary>
    [Fact]
    public async Task With_a_public_url_the_link_is_absolute()
    {
        await using var instance = await AnInstance.ConfiguredAsync(postgres,
            new Dictionary<string, string?>(WithoutSmtp) { ["PLANAFFE_PUBLIC_URL"] = "https://plan.example.org" });
        using var admin = instance.ClientWith(AnInstance.BootstrapToken);
        await admin.PostAsJsonAsync("/users", new { name = "other", email = "other@example.test" }, Ct);

        var invitation = await Link(admin, "/users/other/invitation-link");
        Assert.StartsWith("https://plan.example.org/activate?secret=", invitation);
    }

    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;

    /// <summary>An instance with transactional email switched off entirely (ADR 0018).</summary>
    private static readonly Dictionary<string, string?> WithoutSmtp = new()
    {
        ["PLANAFFE_PUBLIC_URL"] = string.Empty,
        ["PLANAFFE_SMTP_HOST"] = string.Empty,
        ["PLANAFFE_SMTP_PORT"] = string.Empty,
        ["PLANAFFE_SMTP_USERNAME"] = string.Empty,
        ["PLANAFFE_SMTP_PASSWORD"] = string.Empty,
        ["PLANAFFE_SMTP_SECURITY"] = string.Empty,
        ["PLANAFFE_SMTP_FROM_ADDRESS"] = string.Empty,
        ["PLANAFFE_SMTP_FROM_NAME"] = string.Empty,
    };

    private static async Task<string> Link(HttpClient client, string path)
    {
        using var response = await client.PostAsync(path, null, Ct);
        Assert.True(response.StatusCode == HttpStatusCode.OK,
            $"{response.StatusCode}: {await response.Content.ReadAsStringAsync(Ct)}");
        var issued = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.True(issued.GetProperty("expires_at").GetDateTimeOffset() > DateTimeOffset.UtcNow);
        return issued.GetProperty("link").GetString()!;
    }

    private static string SecretIn(string link) =>
        Uri.UnescapeDataString(link[(link.IndexOf("secret=", StringComparison.Ordinal) + "secret=".Length)..]);
}
