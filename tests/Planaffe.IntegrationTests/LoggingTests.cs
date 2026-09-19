using Logaffe.Extensions.Logging;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Planaffe.Api.Hosting;
using Planaffe.Application.Ports;

namespace Planaffe.IntegrationTests;

public sealed class LoggingTests
{
    [Fact]
    public void Console_is_registered_and_the_configured_floor_applies()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Logging:LogLevel:Default"] = "Error",
        });
        builder.AddPlanaffeLogging(LogSettings.FromVariables(null, null, "Debug"));

        using var app = builder.Build();
        var providers = app.Services.GetServices<ILoggerProvider>();
        Assert.Contains(providers, provider => provider is ConsoleLoggerProvider);
        Assert.DoesNotContain(providers, provider => provider is LogaffeLoggerProvider);

        var factory = app.Services.GetRequiredService<ILoggerFactory>();
        Assert.True(factory.CreateLogger("Planaffe.Tests").IsEnabled(LogLevel.Debug));
        Assert.False(factory.CreateLogger("Microsoft.AspNetCore.Tests").IsEnabled(LogLevel.Information));
    }

    [Fact]
    public void Endpoint_and_token_register_logaffe_beside_the_console()
    {
        var builder = WebApplication.CreateBuilder();
        builder.AddPlanaffeLogging(LogSettings.FromVariables("http://127.0.0.1:1", "test-token", null));

        using var app = builder.Build();
        var providers = app.Services.GetServices<ILoggerProvider>();
        Assert.Contains(providers, provider => provider is ConsoleLoggerProvider);
        Assert.Contains(providers, provider => provider is LogaffeLoggerProvider);
    }

    [Fact]
    public async Task Request_line_contains_only_method_path_status_and_duration()
    {
        var logger = new CaptureLogger();
        var middleware = new RequestLoggingMiddleware(
            context =>
            {
                context.Response.StatusCode = StatusCodes.Status201Created;
                return Task.CompletedTask;
            },
            logger);
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = "/issues";
        context.Request.QueryString = new QueryString("?secret=in-query");
        context.Request.Body = new MemoryStream("secret-in-body"u8.ToArray());

        await middleware.InvokeAsync(context);

        var line = Assert.Single(logger.Lines);
        Assert.Contains("POST /issues responded 201 in ", line, StringComparison.Ordinal);
        Assert.DoesNotContain("in-query", line, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-in-body", line, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Unhandled_request_is_logged_with_server_error_status()
    {
        var logger = new CaptureLogger();
        var middleware = new RequestLoggingMiddleware(_ => throw new InvalidOperationException("test failure"), logger);
        var context = new DefaultHttpContext();
        context.Request.Path = "/issues";

        await Assert.ThrowsAsync<InvalidOperationException>(() => middleware.InvokeAsync(context));

        Assert.Contains("responded 500 in ", Assert.Single(logger.Lines), StringComparison.Ordinal);
    }

    private sealed class CaptureLogger : ILogger<RequestLoggingMiddleware>
    {
        public List<string> Lines { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => Lines.Add(formatter(state, exception));
    }
}
