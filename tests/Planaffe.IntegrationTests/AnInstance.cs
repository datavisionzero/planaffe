using System.Net.Http.Headers;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Planaffe.IntegrationTests;

/// <summary>
/// The whole instance in the test process, on a database of its own, with the
/// environment an operator would have set for the first start.
/// </summary>
/// <remarks>
/// The bootstrap variables are always set — to nothing when the test wants none
/// — so that a variable in the developer's real environment cannot reach the
/// test. Starting the host runs the migrations and the bootstrap, which is why
/// a test about a refused start asks for a client and expects the throw.
/// </remarks>
internal sealed class AnInstance(
    string connectionString,
    string? administrator,
    string? token) : WebApplicationFactory<Program>
{
    public const string Administrator = "maintainer";

    public const string BootstrapToken = "a-bootstrap-secret-of-thirty-two-characters-or-more";

    public static Task<AnInstance> BootstrappedAsync(PostgresFixture postgres) =>
        StartedAsync(postgres, Administrator, BootstrapToken);

    public static async Task<AnInstance> StartedAsync(
        PostgresFixture postgres, string? administrator, string? token) =>
        new(await postgres.CreateDatabaseAsync(), administrator, token);

    /// <summary>The same database, started again with what the environment says now.</summary>
    public AnInstance StartedAgain(string? newAdministrator, string? newToken) =>
        new(connectionString, newAdministrator, newToken);

    public string ConnectionString => connectionString;

    /// <summary>
    /// What the instance logged at error or above — the exception behind a 500,
    /// which the problem document deliberately does not carry.
    /// </summary>
    public IReadOnlyList<string> Errors => _errors;

    private readonly List<string> _errors = [];

    public HttpClient ClientWith(string? bearer)
    {
        var client = CreateClient();
        if (bearer is not null)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        }

        return client;
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.ConfigureHostConfiguration(configuration => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = connectionString,
                ["PLANAFFE_BOOTSTRAP_ADMIN"] = administrator ?? string.Empty,
                ["PLANAFFE_BOOTSTRAP_TOKEN"] = token ?? string.Empty,
            }));

        builder.ConfigureLogging(logging => logging.AddProvider(new Capture(_errors)));

        return base.CreateHost(builder);
    }

    private sealed class Capture(List<string> errors) : ILoggerProvider, ILogger
    {
        public ILogger CreateLogger(string categoryName) => this;

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Error;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (IsEnabled(logLevel))
            {
                lock (errors)
                {
                    errors.Add($"{formatter(state, exception)}\n{exception}");
                }
            }
        }

        public void Dispose()
        {
        }
    }
}
