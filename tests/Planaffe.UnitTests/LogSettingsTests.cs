using Planaffe.Application.Ports;

namespace Planaffe.UnitTests;

/// <summary>The three variables of ADR 0029, and what they refuse.</summary>
public sealed class LogSettingsTests
{
    [Fact]
    public void Nothing_set_is_the_console_at_information()
    {
        var settings = LogSettings.FromVariables(null, null, null);

        Assert.False(settings.ShipsToLogaffe);
        Assert.Equal("Information", settings.Level);
    }

    [Fact]
    public void Endpoint_and_token_together_ship_to_logaffe()
    {
        var settings = LogSettings.FromVariables("https://logs.example.org/", " pa-ingest-token ", "warning");

        Assert.True(settings.ShipsToLogaffe);
        Assert.Equal(new Uri("https://logs.example.org/"), settings.Endpoint);
        Assert.Equal("pa-ingest-token", settings.Token);
        Assert.Equal("Warning", settings.Level);
    }

    [Theory]
    [InlineData("Verbose", "Trace")]
    [InlineData("Fatal", "Critical")]
    [InlineData("trace", "Trace")]
    [InlineData("critical", "Critical")]
    public void Existing_and_framework_level_names_are_accepted(string input, string expected)
    {
        var settings = LogSettings.FromVariables(null, null, input);

        Assert.Equal(expected, settings.Level);
    }

    [Theory]
    [InlineData("https://logs.example.org", null, "PLANAFFE_LOG_TOKEN")]
    [InlineData(null, "token", "PLANAFFE_LOG_ENDPOINT")]
    [InlineData("logs.example.org", "token", "PLANAFFE_LOG_ENDPOINT")]
    [InlineData("ftp://logs.example.org", "token", "PLANAFFE_LOG_ENDPOINT")]
    [InlineData(null, null, "PLANAFFE_LOG_LEVEL")]
    public void One_without_the_other_a_bad_address_or_an_unknown_level_is_refused(string? endpoint, string? token, string variable)
    {
        var refusal = Assert.Throws<ArgumentException>(() =>
            LogSettings.FromVariables(endpoint, token, variable == "PLANAFFE_LOG_LEVEL" ? "loud" : null));

        Assert.Equal(variable, refusal.ParamName);
    }
}
