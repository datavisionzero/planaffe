using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Planaffe.Application.Acts;
using Planaffe.Application.Ports;
using Planaffe.Domain;

namespace Planaffe.IntegrationTests;

/// <summary>
/// The acts ask the project scope themselves (<c>docs/codebase.md</c>), not
/// only the Api's door in front of them: these call an act from the container
/// the way a second adapter would, with no HTTP pipeline and no middleware.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class ActScopeTests(PostgresFixture postgres)
{
    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_edge_acts_refuse_an_issue_outside_the_callers_projects()
    {
        await using var instance = await AnInstance.BootstrappedAsync(postgres);
        using var admin = instance.ClientWith(AnInstance.BootstrapToken);
        await admin.PostAsJsonAsync("/projects", new { key = "PLAN", name = "planaffe" }, Ct);
        await admin.PostAsJsonAsync("/issues", new { project = "PLAN", issues = new[] { new { title = "A" } } }, Ct);
        await instance.AddActiveUserAsync("stranger");

        Caller stranger;
        await using (var context = Migrated.ContextFor(instance.ConnectionString))
        {
            var user = await context.Users.SingleAsync(u => u.Name == "stranger", Ct);
            var token = await context.Tokens.SingleAsync(t => t.IdentityId == user.Id, Ct);
            stranger = new Caller(user.Id, user.Kind, user.Name, false, null, token.Id, token.Prefix, token.CreatedAt);
        }

        await using var scope = instance.Services.CreateAsyncScope();
        var request = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        request.Features.Set(stranger);
        scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext = request;
        var edges = scope.ServiceProvider.GetRequiredService<IssueEdges>();

        var refused = await Assert.ThrowsAsync<Refusal>(() => edges.RemoveLabelAsync("PLAN-1", "bug", Ct));
        Assert.Equal(RefusalCode.NotFound, refused.Code);
        refused = await Assert.ThrowsAsync<Refusal>(() => edges.AddLabelAsync("PLAN-1", "bug", Ct));
        Assert.Equal(RefusalCode.NotFound, refused.Code);
    }
}
