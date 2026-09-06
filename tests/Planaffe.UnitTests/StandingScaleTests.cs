using Planaffe.Domain.Projects;

namespace Planaffe.UnitTests;

/// <summary>The scale of ADR 0024 on the Domain type, without a database.</summary>
public sealed class StandingScaleTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 6, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Nothing_open_at_all_is_clear()
    {
        var facts = Facts();
        Assert.Equal(Standing.Clear, StandingScale.Of(facts, Now));
        Assert.Equal(StandingBecause.Nothing, StandingScale.Why(facts, Standing.Clear));
    }

    [Fact]
    public void A_full_backlog_that_is_moving_is_running()
    {
        // The whole point of ADR 0024: forty issues and three claims is the
        // state the product exists to produce, and it is not a warning.
        var facts = Facts(inProgress: 3, ready: 12, open: 41);
        Assert.Equal(Standing.Running, StandingScale.Of(facts, Now));
        Assert.Equal(StandingBecause.Working, StandingScale.Why(facts, Standing.Running));
    }

    [Fact]
    public void Work_that_nobody_can_take_is_idle_and_says_which_of_the_three_it_is()
    {
        var nobody = Facts(ready: 4, open: 9, agents: 0);
        Assert.Equal(Standing.Idle, StandingScale.Of(nobody, Now));
        Assert.Equal(StandingBecause.NoAgent, StandingScale.Why(nobody, Standing.Idle));

        var walled = Facts(blocked: 7, open: 7);
        Assert.Equal(Standing.Idle, StandingScale.Of(walled, Now));
        Assert.Equal(StandingBecause.Blocked, StandingScale.Why(walled, Standing.Idle));

        // One of six behind a blocker and the rest simply not workable: still
        // the blockers, because a chain is the one thing here a reader can pull
        // on, and the "Ready" screen names the rest issue by issue.
        var partly = Facts(blocked: 1, open: 6);
        Assert.Equal(StandingBecause.Blocked, StandingScale.Why(partly, Standing.Idle));

        var unflagged = Facts(open: 6);
        Assert.Equal(Standing.Idle, StandingScale.Of(unflagged, Now));
        Assert.Equal(StandingBecause.NothingReady, StandingScale.Why(unflagged, Standing.Idle));
    }

    [Fact]
    public void One_fresh_entry_waits_and_three_neglect()
    {
        var one = Facts(questions: 1, oldest: Now.AddHours(-4), open: 3);
        Assert.Equal(Standing.Waiting, StandingScale.Of(one, Now));
        Assert.Equal(StandingBecause.Question, StandingScale.Why(one, Standing.Waiting));

        var two = Facts(inReview: 2, oldest: Now.AddHours(-4), open: 3);
        Assert.Equal(Standing.Waiting, StandingScale.Of(two, Now));
        Assert.Equal(StandingBecause.Review, StandingScale.Why(two, Standing.Waiting));

        var three = Facts(unready: 2, stuck: 1, oldest: Now.AddHours(-4), open: 3);
        Assert.Equal(Standing.Neglected, StandingScale.Of(three, Now));
        Assert.Equal(StandingBecause.Unready, StandingScale.Why(three, Standing.Neglected));
    }

    [Fact]
    public void Age_outranks_quantity_at_three_days()
    {
        var almost = Facts(questions: 1, oldest: Now.AddDays(-3).AddMinutes(1), open: 2);
        Assert.Equal(Standing.Waiting, StandingScale.Of(almost, Now));

        var over = Facts(questions: 1, oldest: Now.AddDays(-3), open: 2);
        Assert.Equal(Standing.Neglected, StandingScale.Of(over, Now));
    }

    [Fact]
    public void What_waits_for_a_human_outranks_everything_underneath_it()
    {
        // No agent and a wall of blockers, and still `waiting`: the question is
        // the thing a human can act on, and the tile sends them there.
        var facts = Facts(questions: 1, oldest: Now.AddHours(-1), blocked: 9, open: 9, agents: 0);
        Assert.Equal(Standing.Waiting, StandingScale.Of(facts, Now));
        Assert.Equal(StandingBecause.Question, StandingScale.Why(facts, Standing.Waiting));
    }

    [Fact]
    public void The_size_of_the_backlog_never_enters_the_step()
    {
        var small = Facts(inProgress: 1, ready: 1, open: 2);
        var huge = Facts(inProgress: 1, ready: 500, open: 4000);
        Assert.Equal(StandingScale.Of(small, Now), StandingScale.Of(huge, Now));
    }

    private static StandingFacts Facts(
        int questions = 0,
        int inReview = 0,
        int unready = 0,
        int stuck = 0,
        DateTimeOffset? oldest = null,
        int inProgress = 0,
        int ready = 0,
        int blocked = 0,
        int open = 0,
        int agents = 1) =>
        new(questions, inReview, unready, stuck, oldest, inProgress, ready, blocked, open, agents);
}
