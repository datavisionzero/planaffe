using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Planaffe.Domain.Issues;

namespace Planaffe.IntegrationTests;

/// <summary>The overview of ADR 0024: one step per project, worst first.</summary>
[Collection(nameof(PostgresCollection))]
public sealed class StandingEndpointTests(PostgresFixture postgres)
{
    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Every_step_is_reached_and_the_worst_one_stands_first()
    {
        await using var instance = await AnInstance.BootstrappedAsync(postgres);
        using var admin = instance.ClientWith(AnInstance.BootstrapToken);
        await admin.PostAsJsonAsync("/agents", new { name = "worker" }, Ct);

        // CLEAR: a project with nothing in it at all.
        await admin.PostAsJsonAsync("/projects", new { key = "CLEAR", name = "clear" }, Ct);

        // RUN: a full backlog that is moving. Forty issues would say the same
        // thing as these three; the point of ADR 0024 is that the number does
        // not enter the step.
        await admin.PostAsJsonAsync("/projects", new { key = "RUN", name = "running" }, Ct);
        await admin.PostAsJsonAsync("/issues", new
        {
            project = "RUN",
            issues = new object[] { new { title = "Workable", ready = true }, new { title = "Another", ready = true } },
        }, Ct);

        // IDLE: work, all of it parked. Nothing waits for a human — parking is
        // the decision that it is not up yet — and no agent could take a thing.
        // This is the silence that looks like `clear` from outside and is not.
        await admin.PostAsJsonAsync("/projects", new { key = "IDLE", name = "idle" }, Ct);
        await admin.PostAsJsonAsync("/issues", new
        {
            project = "IDLE",
            issues = new object[]
            {
                new { title = "Parked", status = "backlog" },
                new { title = "Parked too", status = "backlog" },
            },
        }, Ct);

        // WAIT: one issue in review, since a moment ago.
        await admin.PostAsJsonAsync("/projects", new { key = "WAIT", name = "waiting" }, Ct);
        await admin.PostAsJsonAsync("/issues", new
        {
            project = "WAIT",
            issues = new object[] { new { title = "Handed in" }, new { title = "Workable", ready = true } },
        }, Ct);

        // NEGL: three things at once, which is enough without any of them being old.
        await admin.PostAsJsonAsync("/projects", new { key = "NEGL", name = "neglected", triage_required = true }, Ct);
        await admin.PostAsJsonAsync("/issues", new
        {
            project = "NEGL",
            issues = new object[]
            {
                new { title = "Asked", ready = true },
                new { title = "Unready one" },
                new { title = "Unready two" },
            },
        }, Ct);

        await using (var context = Migrated.ContextFor(instance.ConnectionString))
        {
            var user = await context.Users.SingleAsync(Ct);
            var asked = await context.Issues.SingleAsync(issue => issue.Title == "Asked", Ct);
            context.Questions.Add(Question.Ask(asked.ProjectId, asked.Id, "Which way?", user.Id, DateTimeOffset.UtcNow));
            await context.Database.ExecuteSqlRawAsync("update issue set status = 'review' where title = 'Handed in'", Ct);
            await context.SaveChangesAsync(Ct);
        }

        var answer = await admin.GetFromJsonAsync<JsonElement>("/standing", Ct);
        Assert.Equal(1, answer.GetProperty("agents").GetInt32());
        Assert.Equal(
            [("NEGL", "neglected", "question"), ("WAIT", "waiting", "review"), ("IDLE", "idle", "nothing_ready"),
             ("RUN", "running", "working"), ("CLEAR", "clear", "nothing")],
            Steps(answer));

        var run = Project(answer, "RUN");
        Assert.Equal(2, run.GetProperty("work").GetProperty("ready").GetInt32());
        Assert.Equal(2, run.GetProperty("work").GetProperty("open").GetInt32());
        Assert.Equal(0, run.GetProperty("work").GetProperty("in_progress").GetInt32());

        var neglected = Project(answer, "NEGL");
        Assert.Equal(1, neglected.GetProperty("needs_you").GetProperty("question").GetInt32());
        Assert.Equal(2, neglected.GetProperty("needs_you").GetProperty("unready").GetInt32());
        Assert.NotEqual(JsonValueKind.Null, neglected.GetProperty("needs_you").GetProperty("oldest").ValueKind);

        // The whole instance losing its last agent moves every project that had
        // work to `idle`, and says so as `no_agent`: with none, nothing anywhere
        // is picked up. What waits for a human outranks it and does not move.
        await using (var context = Migrated.ContextFor(instance.ConnectionString))
        {
            await context.Database.ExecuteSqlRawAsync("update token set revoked_at = now() where kind = 'agent'", Ct);
        }

        var without = await admin.GetFromJsonAsync<JsonElement>("/standing", Ct);
        Assert.Equal(0, without.GetProperty("agents").GetInt32());
        Assert.Equal(("RUN", "idle", "no_agent"), Steps(without).Single(step => step.Key == "RUN"));
        Assert.Equal(("NEGL", "neglected", "question"), Steps(without).Single(step => step.Key == "NEGL"));
    }

    [Fact]
    public async Task An_entry_that_has_waited_three_days_is_enough_on_its_own()
    {
        await using var instance = await AnInstance.BootstrappedAsync(postgres);
        using var admin = instance.ClientWith(AnInstance.BootstrapToken);
        await admin.PostAsJsonAsync("/agents", new { name = "worker" }, Ct);
        await admin.PostAsJsonAsync("/projects", new { key = "PLAN", name = "planaffe" }, Ct);
        await admin.PostAsJsonAsync("/issues", new
        {
            project = "PLAN",
            issues = new object[] { new { title = "Asked", ready = true }, new { title = "Workable", ready = true } },
        }, Ct);

        await using (var context = Migrated.ContextFor(instance.ConnectionString))
        {
            var user = await context.Users.SingleAsync(Ct);
            var asked = await context.Issues.SingleAsync(issue => issue.Number == 1, Ct);
            context.Questions.Add(Question.Ask(asked.ProjectId, asked.Id, "Which way?", user.Id, DateTimeOffset.UtcNow));
            await context.SaveChangesAsync(Ct);
        }

        var fresh = await admin.GetFromJsonAsync<JsonElement>("/standing", Ct);
        Assert.Equal(("PLAN", "waiting", "question"), Steps(fresh).Single());

        // A question is measured from when it was asked, not from when the
        // issue was written: that is the loop VISION 17 leaves open, and the
        // age of it is the whole reason this step exists.
        await using (var context = Migrated.ContextFor(instance.ConnectionString))
        {
            await context.Database.ExecuteSqlRawAsync("update question set asked_at = now() - interval '4 days'", Ct);
        }

        var old = await admin.GetFromJsonAsync<JsonElement>("/standing", Ct);
        Assert.Equal(("PLAN", "neglected", "question"), Steps(old).Single());
    }

    [Fact]
    public async Task It_answers_only_for_the_projects_the_caller_sees()
    {
        await using var instance = await AnInstance.BootstrappedAsync(postgres);
        using var admin = instance.ClientWith(AnInstance.BootstrapToken);
        await admin.PostAsJsonAsync("/projects", new { key = "MINE", name = "mine" }, Ct);
        await admin.PostAsJsonAsync("/projects", new { key = "THEIRS", name = "theirs" }, Ct);

        var otherToken = await instance.AddActiveUserAsync("other");
        using var other = instance.ClientWith(otherToken);
        using var users = await admin.GetAsync("/users", Ct);
        var otherId = (await users.Content.ReadFromJsonAsync<JsonElement>(Ct)).EnumerateArray()
            .Single(value => value.GetProperty("name").GetString() == "other").GetProperty("id").GetGuid();

        var nothing = await other.GetFromJsonAsync<JsonElement>("/standing", Ct);
        Assert.Empty(nothing.GetProperty("projects").EnumerateArray());

        Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsync($"/projects/MINE/users/{otherId}", null, Ct)).StatusCode);
        var one = await other.GetFromJsonAsync<JsonElement>("/standing", Ct);
        Assert.Equal(["MINE"], one.GetProperty("projects").EnumerateArray().Select(p => p.GetProperty("key").GetString()));

        using var anonymous = instance.ClientWith(null);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/standing", Ct)).StatusCode);
    }

    private static JsonElement Project(JsonElement answer, string key) =>
        answer.GetProperty("projects").EnumerateArray().Single(value => value.GetProperty("key").GetString() == key);

    private static (string Key, string? Standing, string? Because)[] Steps(JsonElement answer) =>
        [.. answer.GetProperty("projects").EnumerateArray().Select(value => (
            value.GetProperty("key").GetString()!,
            value.GetProperty("standing").GetString(),
            value.GetProperty("because").GetString()))];
}
