using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Planaffe.Api.Http;

namespace Planaffe.IntegrationTests;

/// <summary>
/// The doors a stranger can knock on, under a burst: sign-in, device login and
/// a password change are each let through as often as their limit says and
/// not as often as requests arrive at once, and what is turned away is told
/// <c>login-throttled</c> with a <c>Retry-After</c>.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class LoginThrottleEndpointTests(PostgresFixture postgres)
{
    private const string Password = "a long first password";
    private const int AccountLimit = 5;

    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Thirty_concurrent_wrong_sign_ins_check_at_most_the_limit_and_the_rest_are_throttled()
    {
        await using var instance = await AnInstance.BootstrappedAsync(postgres);
        using var client = await SignedUpAsync(instance);

        var answers = await Task.WhenAll(Enumerable.Range(0, 30).Select(async _ =>
        {
            using var response = await client.PostAsJsonAsync("/session",
                new { email = "maintainer@example.test", password = "not the password at all" }, Ct);
            return (response.StatusCode, response.Headers.RetryAfter?.Delta,
                Type: (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("type").GetString());
        }));

        // Every 401 is a password that was checked; the gate is taken before
        // the check, so no more of them get through than the limit.
        var checkedOnes = answers.Count(a => a.StatusCode == HttpStatusCode.Unauthorized);
        Assert.InRange(checkedOnes, 1, AccountLimit);
        var throttled = answers.Where(a => a.StatusCode == HttpStatusCode.TooManyRequests).ToList();
        Assert.Equal(30 - checkedOnes, throttled.Count);
        Assert.All(throttled, a =>
        {
            Assert.Equal("/problems/login-throttled", a.Type);
            Assert.True(a.Delta > TimeSpan.Zero);
        });

        // And the right password is throttled too while the account is: the
        // limit is on guessing, and a guess that happens to be right is one.
        using var right = await client.PostAsJsonAsync("/session", new { email = "maintainer@example.test", password = Password }, Ct);
        Assert.Equal(HttpStatusCode.TooManyRequests, right.StatusCode);
    }

    [Fact]
    public async Task A_right_password_gives_its_attempt_back()
    {
        await using var instance = await AnInstance.BootstrappedAsync(postgres);
        using var client = await SignedUpAsync(instance);

        for (var attempt = 0; attempt < AccountLimit * 3; attempt++)
        {
            using var signedIn = await client.PostAsJsonAsync("/session", new { email = "maintainer@example.test", password = Password }, Ct);
            Assert.Equal(HttpStatusCode.NoContent, signedIn.StatusCode);
        }
    }

    [Fact]
    public async Task A_wrong_current_password_is_a_validation_refusal_that_leaves_the_session_signed_in_and_is_limited()
    {
        await using var instance = await AnInstance.BootstrappedAsync(postgres);
        using var client = instance.ClientWith(null);
        using var exchange = await client.PostAsJsonAsync("/session/bootstrap", new { token = AnInstance.BootstrapToken, password = Password }, Ct);
        var cookie = exchange.Headers.GetValues("Set-Cookie").Single().Split(';')[0];

        using (var wrong = await ChangeAsync(client, cookie, "not the current password"))
        {
            Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);
            var problem = await wrong.Content.ReadFromJsonAsync<JsonElement>(Ct);
            Assert.Equal("/problems/validation", problem.GetProperty("type").GetString());
            Assert.True(problem.GetProperty("errors").TryGetProperty("current_password", out _));
        }

        using (var short_ = await ChangeAsync(client, cookie, "short"))
        {
            Assert.Equal(HttpStatusCode.BadRequest, short_.StatusCode);
        }

        using var me = new HttpRequestMessage(HttpMethod.Get, "/me");
        me.Headers.Add("Cookie", cookie);
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(me, Ct)).StatusCode);

        // Two wrong so far; three more fill the window, and then even the
        // right one waits.
        for (var attempt = 2; attempt < AccountLimit; attempt++)
        {
            using var wrong = await ChangeAsync(client, cookie, "not the current password");
            Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);
        }

        using var throttled = await ChangeAsync(client, cookie, Password);
        Assert.Equal(HttpStatusCode.TooManyRequests, throttled.StatusCode);
        Assert.True(throttled.Headers.RetryAfter?.Delta > TimeSpan.Zero);
    }

    [Fact]
    public async Task A_burst_of_device_logins_is_throttled_rather_than_told_to_keep_polling()
    {
        await using var instance = await AnInstance.BootstrappedAsync(postgres);
        using var cli = instance.ClientWith(null);

        var answers = await Task.WhenAll(Enumerable.Range(0, 25).Select(async _ =>
        {
            using var response = await cli.PostAsync("/device-logins", null, Ct);
            return response.StatusCode;
        }));

        Assert.Equal(20, answers.Count(status => status == HttpStatusCode.OK));
        Assert.Equal(5, answers.Count(status => status == HttpStatusCode.TooManyRequests));
    }

    private static async Task<HttpClient> SignedUpAsync(AnInstance instance)
    {
        var client = instance.ClientWith(null);
        using var exchange = await client.PostAsJsonAsync("/session/bootstrap",
            new { token = AnInstance.BootstrapToken, password = Password }, Ct);
        Assert.Equal(HttpStatusCode.NoContent, exchange.StatusCode);
        return client;
    }

    private static async Task<HttpResponseMessage> ChangeAsync(HttpClient client, string cookie, string current)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/me/password")
        {
            Content = JsonContent.Create(new { current_password = current, password = "a long second password" }),
        };
        request.Headers.Add("Cookie", cookie);
        request.Headers.Add("Origin", "http://localhost:5173");
        request.Headers.Add(CsrfProtection.Header, "1");
        return await client.SendAsync(request, Ct);
    }
}

/// <summary>What the throttle counts as one source, and how recovery is let through.</summary>
public sealed class LoginThrottleTests
{
    [Theory]
    [InlineData("203.0.113.7", "203.0.113.7")]
    [InlineData("::ffff:203.0.113.7", "203.0.113.7")]
    [InlineData("2001:db8:1:2:aaaa:bbbb:cccc:dddd", "2001:db8:1:2::/64")]
    [InlineData("2001:db8:1:2::1", "2001:db8:1:2::/64")]
    public void An_ipv6_source_is_its_64_and_an_ipv4_one_is_itself(string address, string source) =>
        Assert.Equal(source, LoginThrottle.SourceOf(IPAddress.Parse(address)));

    [Fact]
    public void Two_ipv6_addresses_in_one_64_share_a_limit()
    {
        var throttle = new LoginThrottle(TimeProvider.System);
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var address = IPAddress.Parse($"2001:db8:1:2::{attempt + 1:x}");
            Assert.True(throttle.TryReserve($"nobody{attempt}@example.test", LoginThrottle.SourceOf(address), out _, out _));
        }

        Assert.False(throttle.TryReserve("somebody@example.test", LoginThrottle.SourceOf(IPAddress.Parse("2001:db8:1:2::ffff")), out _, out var wait));
        Assert.True(wait > TimeSpan.Zero);
        Assert.True(throttle.TryReserve("somebody@example.test", LoginThrottle.SourceOf(IPAddress.Parse("2001:db8:1:3::1")), out _, out _));
    }

    [Fact]
    public void Recovery_sends_once_per_account_in_its_window()
    {
        var throttle = new LoginThrottle(TimeProvider.System);
        Assert.Equal(LoginThrottle.Recovery.Send, throttle.TryRecover("a@example.test", "one", out _));
        Assert.Equal(LoginThrottle.Recovery.Quiet, throttle.TryRecover("a@example.test", "two", out _));
        Assert.Equal(LoginThrottle.Recovery.Send, throttle.TryRecover("b@example.test", "one", out _));
    }
}
