using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Planaffe.Domain.Issues;
using Planaffe.Domain.Projects;
using Planaffe.Infrastructure;

namespace Planaffe.IntegrationTests;

[Collection(nameof(PostgresCollection))]
public sealed class WakeUpTests(PostgresFixture postgres)
{
    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Issue_changes_wake_only_their_project_and_a_deadline_remains_the_fallback()
    {
        await using var db = await Migrated.SeededAsync(postgres);
        await using var changes = new PostgresChanges(db.ConnectionString, NullLogger<PostgresChanges>.Instance);

        await EstablishListenerAsync(changes, db);

        var other = Project.Create("OTHER", "Another project", db.User.Id, Migrated.Now);
        var otherIssue = Issue.Create(other.Id, 1, "Elsewhere", db.User.Id, Migrated.Now);
        db.Context.AddRange(other, otherIssue);
        await db.Context.SaveChangesAsync(Ct);

        using (var deadline = new CancellationTokenSource(TimeSpan.FromMilliseconds(300)))
        {
            await Assert.ThrowsAsync<TaskCanceledException>(
                () => changes.WaitAsync(db.Project.Id, deadline.Token));
        }

        var firstWaiter = changes.WaitAsync(db.Project.Id, Ct);
        var secondWaiter = changes.WaitAsync(db.Project.Id, Ct);
        await db.Context.Database.ExecuteSqlInterpolatedAsync(
            $"update issue set updated_at = now() where id = {db.Issue.Id}", Ct);
        await Task.WhenAll(firstWaiter, secondWaiter).WaitAsync(TimeSpan.FromSeconds(5), Ct);
    }

    [Fact]
    public async Task Inserting_and_answering_a_question_wake_the_issue_project()
    {
        await using var db = await Migrated.SeededAsync(postgres);
        await using var changes = new PostgresChanges(db.ConnectionString, NullLogger<PostgresChanges>.Instance);
        await EstablishListenerAsync(changes, db);

        var inserted = changes.WaitAsync(db.Project.Id, Ct);
        var question = Question.Ask(db.Project.Id, db.Issue.Id, "Which way?", db.Agent.Id, Migrated.Now);
        db.Context.Questions.Add(question);
        await db.Context.SaveChangesAsync(Ct);
        await inserted.WaitAsync(TimeSpan.FromSeconds(5), Ct);

        var answered = changes.WaitAsync(db.Project.Id, Ct);
        question.AnswerWith("This way.", db.User.Id, Migrated.Now.AddMinutes(1));
        await db.Context.SaveChangesAsync(Ct);
        await answered.WaitAsync(TimeSpan.FromSeconds(5), Ct);
    }

    [Fact]
    public async Task A_disconnected_listener_wakes_waiters_and_reconnects()
    {
        await using var db = await Migrated.SeededAsync(postgres);
        await using var changes = new PostgresChanges(db.ConnectionString, NullLogger<PostgresChanges>.Instance);
        await EstablishListenerAsync(changes, db);

        var disconnected = changes.WaitAsync(db.Project.Id, Ct);
        await using (var connection = new NpgsqlConnection(db.ConnectionString))
        {
            await connection.OpenAsync(Ct);
            await using var command = new NpgsqlCommand("""
                select coalesce(bool_or(pg_terminate_backend(pid)), false)
                  from pg_stat_activity
                 where application_name = 'planaffe-change-listener'
                   and datname = current_database()
                """, connection);
            Assert.True((bool?)await command.ExecuteScalarAsync(Ct));
        }
        await disconnected.WaitAsync(TimeSpan.FromSeconds(5), Ct);

        await EstablishListenerAsync(changes, db);
    }

    /// <summary>
    /// A blocker may sit in another project. Closing or deleting it makes the
    /// issue it blocks workable, so it wakes the blocked issue's project too;
    /// so does deleting the blocker's whole project.
    /// </summary>
    [Fact]
    public async Task A_blocker_in_another_project_wakes_the_project_it_blocks()
    {
        await using var db = await Migrated.SeededAsync(postgres);
        await using var changes = new PostgresChanges(db.ConnectionString, NullLogger<PostgresChanges>.Instance);
        await EstablishListenerAsync(changes, db);

        var other = Project.Create("OTHER", "Another project", db.User.Id, Migrated.Now);
        var first = Issue.Create(other.Id, 1, "Elsewhere", db.User.Id, Migrated.Now);
        var second = Issue.Create(other.Id, 2, "Elsewhere too", db.User.Id, Migrated.Now);
        db.Context.AddRange(other, first, second);
        await db.Context.SaveChangesAsync(Ct);
        await db.Context.Database.ExecuteSqlInterpolatedAsync(
            $"insert into blocker (blocker_id, blocked_id, created_by, created_at) values ({first.Id}, {db.Issue.Id}, {db.User.Id}, now()), ({second.Id}, {db.Issue.Id}, {db.User.Id}, now())", Ct);

        var closed = changes.WaitAsync(db.Project.Id, Ct);
        await db.Context.Database.ExecuteSqlInterpolatedAsync(
            $"update issue set status = 'done', closed_at = now() where id = {first.Id}", Ct);
        await closed.WaitAsync(TimeSpan.FromSeconds(5), Ct);

        var deleted = changes.WaitAsync(db.Project.Id, Ct);
        await db.Context.Database.ExecuteSqlInterpolatedAsync(
            $"update project set deleted_at = now(), deleted_by = {db.User.Id} where id = {other.Id}", Ct);
        await deleted.WaitAsync(TimeSpan.FromSeconds(5), Ct);
    }

    private static async Task EstablishListenerAsync(PostgresChanges changes, Migrated db)
    {
        var waiting = changes.WaitAsync(db.Project.Id, Ct);
        while (!waiting.IsCompleted)
        {
            await db.Context.Database.ExecuteSqlInterpolatedAsync(
                $"update issue set updated_at = now() where id = {db.Issue.Id}", Ct);
            await Task.WhenAny(waiting, Task.Delay(25, Ct));
        }

        await waiting;
    }
}
