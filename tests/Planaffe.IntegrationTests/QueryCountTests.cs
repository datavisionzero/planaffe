using System.Collections.Concurrent;
using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Planaffe.Infrastructure.Persistence;

namespace Planaffe.IntegrationTests;

/// <summary>
/// How many statements a request costs, where that number is what used to
/// grow with the size of the answer: one per release in the list, and one
/// read of the project's labels per issue of a bulk create.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class QueryCountTests(PostgresFixture postgres)
{
    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_release_list_costs_the_same_for_one_release_and_for_six()
    {
        await using var instance = await AnInstance.BootstrappedAsync(postgres);
        var counter = new Counter();
        await using var counted = Counted(instance, counter);
        using var admin = counted.CreateClient();
        admin.DefaultRequestHeaders.Authorization = new("Bearer", AnInstance.BootstrapToken);
        await admin.PostAsJsonAsync("/projects", new { key = "PLAN", name = "planaffe" }, Ct);
        Assert.Equal(HttpStatusCode.Created, (await admin.PostAsJsonAsync("/projects/PLAN/releases/publish", new { name = "v1.0.0" }, Ct)).StatusCode);

        var few = await counter.CountAsync(() => admin.GetAsync("/projects/PLAN/releases", Ct));

        for (var minor = 1; minor <= 5; minor++)
        {
            Assert.Equal(HttpStatusCode.Created, (await admin.PostAsJsonAsync("/projects/PLAN/releases/publish", new { name = $"v1.{minor}.0" }, Ct)).StatusCode);
        }

        var many = await counter.CountAsync(() => admin.GetAsync("/projects/PLAN/releases", Ct));

        Assert.Equal(few.Count, many.Count);
    }

    [Fact]
    public async Task A_bulk_create_reads_the_project_labels_once_to_resolve_them()
    {
        await using var instance = await AnInstance.BootstrappedAsync(postgres);
        var counter = new Counter();
        await using var counted = Counted(instance, counter);
        using var admin = counted.CreateClient();
        admin.DefaultRequestHeaders.Authorization = new("Bearer", AnInstance.BootstrapToken);
        await admin.PostAsJsonAsync("/projects", new { key = "PLAN", name = "planaffe" }, Ct);
        Assert.Equal(HttpStatusCode.Created, (await admin.PostAsJsonAsync("/projects/PLAN/labels", new { name = "wanted" }, Ct)).StatusCode);

        const int Items = 20;
        var statements = await counter.CountAsync(() => admin.PostAsJsonAsync("/issues", new
        {
            project = "PLAN",
            issues = Enumerable.Range(1, Items).Select(n => new { title = $"Issue {n}", labels = new[] { "wanted" } }).ToArray(),
        }, Ct));

        // The label list of the project: once for the whole request to resolve
        // the names, and once per issue in the answer, which is the complete
        // shape and carries the project's labels. It used to be read a second
        // time per issue before the transaction.
        var labelLists = statements.Count(text =>
            text.Contains("FROM label", StringComparison.Ordinal)
            && !text.Contains("issue_label", StringComparison.Ordinal)
            && text.Contains("ORDER BY", StringComparison.Ordinal));
        Assert.Equal(Items + 1, labelLists);
    }

    private static WebApplicationFactory<Program> Counted(AnInstance instance, Counter counter) =>
        instance.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.ConfigureDbContext<PlanaffeDbContext>(options => options.AddInterceptors(counter))));

    /// <summary>Every statement EF Core sends, while a request is being counted.</summary>
    private sealed class Counter : DbCommandInterceptor
    {
        private ConcurrentQueue<string>? _texts;

        public async Task<IReadOnlyList<string>> CountAsync(Func<Task<HttpResponseMessage>> request)
        {
            var texts = new ConcurrentQueue<string>();
            _texts = texts;
            using var response = await request();
            _texts = null;
            Assert.True(response.IsSuccessStatusCode, $"{response.StatusCode}: {await response.Content.ReadAsStringAsync(Ct)}");
            return [.. texts];
        }

        public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result) =>
            Record(command, result);

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(Record(command, result));

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(Record(command, result));

        public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<object> result, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(Record(command, result));

        private T Record<T>(DbCommand command, T result)
        {
            _texts?.Enqueue(command.CommandText);
            return result;
        }
    }
}
