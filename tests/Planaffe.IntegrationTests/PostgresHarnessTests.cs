using Npgsql;
using Testcontainers.PostgreSql;

namespace Planaffe.IntegrationTests;

/// <summary>
/// docs/codebase.md splits the tests by what they need to run, and what this
/// project needs is a real Postgres — because the acts worth testing here are
/// the concurrent ones, and no substitute can vouch for those.
///
/// Nothing has a schema yet. This asserts that the harness every one of those
/// tests will be written against actually starts, which is worth finding out
/// before anything depends on it and while a red trunk is still cheap.
/// </summary>
public sealed class PostgresHarnessTests : IAsyncLifetime
{
    // The image is named rather than defaulted, and the version is the one the
    // Compose file will run: a test that passes against a different major than
    // the installation uses has proved something about neither.
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18").Build();

    public ValueTask InitializeAsync() => new(_postgres.StartAsync());

    public ValueTask DisposeAsync() => _postgres.DisposeAsync();

    [Fact]
    public async Task A_real_postgres_starts_and_answers()
    {
        await using var connection = new NpgsqlConnection(_postgres.GetConnectionString());
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        await using var command = new NpgsqlCommand("select 1", connection);

        Assert.Equal(1, await command.ExecuteScalarAsync(TestContext.Current.CancellationToken));
    }
}
