using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using System.Text.RegularExpressions;

namespace Planaffe.IntegrationTests;

[Collection(nameof(PostgresCollection))]
public sealed class SmtpEndpointTests(PostgresFixture postgres) : IAsyncLifetime
{
    private readonly IContainer _mailpit = new ContainerBuilder("axllent/mailpit:v1.31.0")
        .WithPortBinding(1025, assignRandomHostPort: true)
        .WithPortBinding(8025, assignRandomHostPort: true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(request => request.ForPort(8025)))
        .Build();

    public async ValueTask InitializeAsync() => await _mailpit.StartAsync();

    public async ValueTask DisposeAsync() => await _mailpit.DisposeAsync();

    [Fact]
    public async Task An_administrator_sees_non_secret_status_and_sends_text_and_html_through_mailpit()
    {
        var configuration = ConfiguredMailpit();
        configuration["PLANAFFE_SMTP_USERNAME"] = string.Empty;
        configuration["PLANAFFE_SMTP_PASSWORD"] = string.Empty;
        await using var instance = await AnInstance.ConfiguredAsync(postgres, configuration);
        using var client = instance.ClientWith(AnInstance.BootstrapToken);

        var status = await client.GetFromJsonAsync<JsonElement>("/admin/smtp", TestContext.Current.CancellationToken);
        Assert.True(status.GetProperty("configured").GetBoolean());
        Assert.Equal("127.0.0.1", status.GetProperty("host").GetString());
        Assert.Equal("none", status.GetProperty("security").GetString());
        Assert.False(status.TryGetProperty("username", out _));
        Assert.False(status.TryGetProperty("password", out _));

        using var sent = await client.PostAsJsonAsync("/admin/smtp/test",
            new { email = "recipient@example.test" }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Accepted, sent.StatusCode);

        using var mailpit = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{_mailpit.GetMappedPublicPort(8025)}") };
        var messages = await mailpit.GetFromJsonAsync<JsonElement>("/api/v1/messages", TestContext.Current.CancellationToken);
        Assert.Equal(1, messages.GetProperty("total").GetInt32());
        var item = messages.GetProperty("messages")[0];
        Assert.Equal("planaffe test email", item.GetProperty("Subject").GetString());

        var message = await mailpit.GetFromJsonAsync<JsonElement>(
            $"/api/v1/message/{item.GetProperty("ID").GetString()}", TestContext.Current.CancellationToken);
        Assert.Contains("Transactional email is configured correctly.", message.GetProperty("Text").GetString());
        Assert.Contains("<strong>planaffe</strong>", message.GetProperty("HTML").GetString());
    }

    [Fact]
    public async Task An_unconfigured_instance_reports_status_and_refuses_delivery()
    {
        await using var instance = await AnInstance.ConfiguredAsync(postgres, DisabledSmtp());
        using var client = instance.ClientWith(AnInstance.BootstrapToken);

        var status = await client.GetFromJsonAsync<JsonElement>("/admin/smtp", TestContext.Current.CancellationToken);
        Assert.False(status.GetProperty("configured").GetBoolean());

        using var sent = await client.PostAsJsonAsync("/admin/smtp/test",
            new { email = "recipient@example.test" }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, sent.StatusCode);
        var problem = await sent.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("/problems/smtp-not-configured", problem.GetProperty("type").GetString());
    }

    [Fact]
    public async Task An_invited_user_activates_and_recovers_their_password_from_emailed_links()
    {
        await using var instance = await AnInstance.ConfiguredAsync(postgres, ConfiguredMailpit());
        using var admin = instance.ClientWith(AnInstance.BootstrapToken);
        using var invited = await admin.PostAsJsonAsync("/users",
            new { name = "other", email = "other@example.test" }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, invited.StatusCode);

        var invitationSecret = await SecretFromLatestMessage("activate");
        using var browser = instance.ClientWith(null);
        using var accepted = await browser.PostAsJsonAsync("/invitations/accept",
            new { secret = invitationSecret, password = "the first long password" }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, accepted.StatusCode);

        using var login = await browser.PostAsJsonAsync("/session",
            new { email = "OTHER@example.test", password = "the first long password" }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);

        using var requested = await browser.PostAsJsonAsync("/password-recovery",
            new { email = "other@example.test" }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Accepted, requested.StatusCode);
        var recoverySecret = await SecretFromLatestMessage("recover");
        using var completed = await browser.PostAsJsonAsync("/password-recovery/complete",
            new { secret = recoverySecret, password = "the replacement password" }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, completed.StatusCode);

        using var replacement = await browser.PostAsJsonAsync("/session",
            new { email = "other@example.test", password = "the replacement password" }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, replacement.StatusCode);
    }

    private async Task<string> SecretFromLatestMessage(string path)
    {
        using var mailpit = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{_mailpit.GetMappedPublicPort(8025)}") };
        var messages = await mailpit.GetFromJsonAsync<JsonElement>("/api/v1/messages", TestContext.Current.CancellationToken);
        var id = messages.GetProperty("messages")[0].GetProperty("ID").GetString();
        var message = await mailpit.GetFromJsonAsync<JsonElement>($"/api/v1/message/{id}", TestContext.Current.CancellationToken);
        var match = Regex.Match(message.GetProperty("Text").GetString()!, $@"/{path}\?secret=([^\s]+)");
        Assert.True(match.Success, message.GetProperty("Text").GetString());
        return Uri.UnescapeDataString(match.Groups[1].Value);
    }

    private Dictionary<string, string?> ConfiguredMailpit() => new()
    {
        ["PLANAFFE_PUBLIC_URL"] = "http://localhost:5173",
        ["PLANAFFE_SMTP_HOST"] = "127.0.0.1",
        ["PLANAFFE_SMTP_PORT"] = _mailpit.GetMappedPublicPort(1025).ToString(),
        ["PLANAFFE_SMTP_SECURITY"] = "none",
        ["PLANAFFE_SMTP_FROM_ADDRESS"] = "planaffe@example.test",
        ["PLANAFFE_SMTP_FROM_NAME"] = "planaffe",
    };

    private static Dictionary<string, string?> DisabledSmtp() => new()
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
}
