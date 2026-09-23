using Planaffe.Domain;
using Planaffe.Domain.Identities;
using Planaffe.Domain.Issues;

namespace Planaffe.UnitTests;

/// <summary>
/// The moves of the transition table on the Domain type (ADR 0014, ADR 0016,
/// <c>docs/api.md</c>, The acts on an issue), as a matrix: where the issue is,
/// who asks, whether the project requires review, and whose claim it carries.
/// Each case states its expectation from the table rather than from the code.
/// </summary>
public sealed class IssueMoveTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);

    private static readonly TimeSpan FourHours = TimeSpan.FromHours(4);

    private static readonly Guid Agent = Guid.NewGuid();

    private static readonly Guid User = Guid.NewGuid();

    private static readonly Guid SomebodyElse = Guid.NewGuid();

    /// <summary>Whose claim the issue carries, seen from the caller.</summary>
    public enum Holding
    {
        None,
        Own,
        Foreign,
        Lapsed,
    }

    public static TheoryData<IssueStatus, Holding, IdentityKind, bool> Matrix()
    {
        var data = new TheoryData<IssueStatus, Holding, IdentityKind, bool>();
        foreach (var kind in new[] { IdentityKind.Agent, IdentityKind.User })
        {
            foreach (var reviewRequired in new[] { false, true })
            {
                foreach (var status in new[] { IssueStatus.Backlog, IssueStatus.Todo, IssueStatus.Review, IssueStatus.Done, IssueStatus.Canceled })
                {
                    data.Add(status, Holding.None, kind, reviewRequired);
                }

                foreach (var holding in new[] { Holding.Own, Holding.Foreign, Holding.Lapsed })
                {
                    data.Add(IssueStatus.InProgress, holding, kind, reviewRequired);
                }
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Matrix))]
    public void Close_follows_the_table(IssueStatus from, Holding holding, IdentityKind kind, bool reviewRequired)
    {
        foreach (var target in new[] { IssueStatus.Done, IssueStatus.Canceled })
        {
            var issue = Start(from, holding, kind);
            var agentUnderReview = kind is IdentityKind.Agent && reviewRequired;

            var refusal = from is IssueStatus.Done or IssueStatus.Canceled ? RefusalCode.Transition
                : holding is Holding.Foreign && kind is IdentityKind.Agent ? RefusalCode.ClaimHeld
                : from is IssueStatus.Review && agentUnderReview ? RefusalCode.Transition
                : (RefusalCode?)null;

            if (refusal is { } code)
            {
                Assert.Equal(code, Assert.Throws<Refusal>(() => issue.Close(target, "Done.", Caller(kind), kind, reviewRequired, Now)).Code);
                continue;
            }

            var landed = issue.Close(target, "Done.", Caller(kind), kind, reviewRequired, Now);

            Assert.Equal(agentUnderReview ? IssueStatus.Review : target, landed);
            Assert.Equal(landed, issue.Status);
            Assert.Equal(agentUnderReview ? null : Now, issue.ClosedAt);
            Assert.Null(issue.Claim);
            Assert.Equal("Done.", issue.Result);
        }
    }

    [Theory]
    [MemberData(nameof(Matrix))]
    public void Handing_in_follows_the_table(IssueStatus from, Holding holding, IdentityKind kind, bool reviewRequired)
    {
        _ = reviewRequired; // handing in lands in review whatever the switch says
        var issue = Start(from, holding, kind);

        var refusal = from is IssueStatus.Review or IssueStatus.Done or IssueStatus.Canceled ? RefusalCode.Transition
            : holding is Holding.Foreign && kind is IdentityKind.Agent ? RefusalCode.ClaimHeld
            : (RefusalCode?)null;

        if (refusal is { } code)
        {
            Assert.Equal(code, Assert.Throws<Refusal>(() => issue.HandIn("Handed in.", Caller(kind), kind, Now)).Code);
            return;
        }

        issue.HandIn("Handed in.", Caller(kind), kind, Now);

        Assert.Equal(IssueStatus.Review, issue.Status);
        Assert.Null(issue.ClosedAt);
        Assert.Null(issue.Claim);
        Assert.Equal("Handed in.", issue.Result);
    }

    [Theory]
    [MemberData(nameof(Matrix))]
    public void Reopening_follows_the_table(IssueStatus from, Holding holding, IdentityKind kind, bool reviewRequired)
    {
        _ = (kind, reviewRequired); // reopening asks after neither
        var issue = Start(from, holding, kind);
        var result = issue.Result;

        if (from is not (IssueStatus.Review or IssueStatus.Done or IssueStatus.Canceled))
        {
            Assert.Equal(RefusalCode.Transition, Assert.Throws<Refusal>(() => issue.Reopen(Now)).Code);
            return;
        }

        issue.Reopen(Now);

        Assert.Equal(IssueStatus.Todo, issue.Status);
        Assert.Null(issue.ClosedAt);
        Assert.Null(issue.Claim);
        Assert.Equal(result, issue.Result);
    }

    [Theory]
    [MemberData(nameof(Matrix))]
    public void Parking_and_unparking_follow_the_table(IssueStatus from, Holding holding, IdentityKind kind, bool reviewRequired)
    {
        _ = reviewRequired;
        foreach (var target in new[] { IssueStatus.Backlog, IssueStatus.Todo })
        {
            var issue = Start(from, holding, kind);

            // A lapsed claim is no claim, and its in_progress reads todo.
            var reads = holding is Holding.Lapsed ? IssueStatus.Todo : from;
            var refusal = holding is Holding.Foreign && kind is IdentityKind.Agent ? RefusalCode.ClaimHeld
                : (reads, target) is (IssueStatus.Todo, IssueStatus.Backlog) or (IssueStatus.Backlog, IssueStatus.Todo) ? (RefusalCode?)null
                : RefusalCode.Transition;

            if (refusal is { } code)
            {
                Assert.Equal(code, Assert.Throws<Refusal>(() => issue.MoveTo(target, Caller(kind), kind, Now)).Code);
                continue;
            }

            issue.MoveTo(target, Caller(kind), kind, Now);

            Assert.Equal(target, issue.Status);
            Assert.Null(issue.Claim);
        }
    }

    [Fact]
    public void The_acts_are_not_moves_through_parking()
    {
        var issue = Start(IssueStatus.Todo, Holding.None, IdentityKind.User);

        foreach (var target in new[] { IssueStatus.InProgress, IssueStatus.Review, IssueStatus.Done, IssueStatus.Canceled })
        {
            var refused = Assert.Throws<Refusal>(() => issue.MoveTo(target, User, IdentityKind.User, Now));
            Assert.Equal(RefusalCode.Transition, refused.Code);
            Assert.Contains("through the acts", refused.Detail);
        }
    }

    [Fact]
    public void A_close_is_done_or_canceled_and_nothing_else()
    {
        foreach (var target in new[] { IssueStatus.Backlog, IssueStatus.Todo, IssueStatus.InProgress, IssueStatus.Review })
        {
            var issue = Start(IssueStatus.Todo, Holding.None, IdentityKind.User);
            Assert.Equal(RefusalCode.Validation, Assert.Throws<Refusal>(() => issue.Close(target, null, User, IdentityKind.User, false, Now)).Code);
        }
    }

    [Fact]
    public void A_close_without_a_result_keeps_the_one_there()
    {
        var issue = Start(IssueStatus.Todo, Holding.None, IdentityKind.User);
        issue.RecordResult("What was done.", Now.AddHours(-1));

        issue.Close(IssueStatus.Done, null, User, IdentityKind.User, false, Now);

        Assert.Equal("What was done.", issue.Result);
    }

    [Theory]
    [MemberData(nameof(Matrix))]
    public void Deleting_lets_go_of_the_claim_and_restoring_does_not_bring_it_back(
        IssueStatus from, Holding holding, IdentityKind kind, bool reviewRequired)
    {
        _ = reviewRequired;
        var issue = Start(from, holding, kind);

        issue.Delete(User, Now);

        Assert.True(issue.Deleted);
        Assert.Equal(Now, issue.DeletedAt);
        Assert.Equal(User, issue.DeletedBy);
        Assert.Null(issue.Claim);
        Assert.Equal(from is IssueStatus.InProgress ? IssueStatus.Todo : from, issue.Status);

        // A second delete changes nothing.
        issue.Delete(SomebodyElse, Now.AddHours(1));
        Assert.Equal(Now, issue.DeletedAt);
        Assert.Equal(User, issue.DeletedBy);

        issue.Restore(Now.AddHours(2));

        Assert.False(issue.Deleted);
        Assert.Null(issue.DeletedBy);
        Assert.Null(issue.Claim);
        Assert.Equal(Now.AddHours(2), issue.UpdatedAt);
    }

    /// <summary>An agent's own id, a user's own id: the caller of every case.</summary>
    private static Guid Caller(IdentityKind kind) => kind is IdentityKind.Agent ? Agent : User;

    /// <summary>An issue where the case says, carrying the claim it says, reached through the moves themselves.</summary>
    private static Issue Start(IssueStatus status, Holding holding, IdentityKind callerKind)
    {
        var issue = Issue.Create(Guid.NewGuid(), 1, "An issue", User, Now.AddDays(-1), parked: status is IssueStatus.Backlog);

        switch (status)
        {
            case IssueStatus.Review:
                issue.HandIn("Handed in.", User, IdentityKind.User, Now.AddHours(-2));
                break;
            case IssueStatus.Done or IssueStatus.Canceled:
                issue.Close(status, "Closed.", User, IdentityKind.User, false, Now.AddHours(-2));
                break;
            case IssueStatus.InProgress:
                var (holder, kind, at) = holding switch
                {
                    Holding.Own => (Caller(callerKind), callerKind, Now.AddHours(-1)),
                    Holding.Foreign => (SomebodyElse, IdentityKind.Agent, Now.AddHours(-1)),
                    Holding.Lapsed => (SomebodyElse, IdentityKind.Agent, Now.AddHours(-5)),
                    _ => throw new ArgumentOutOfRangeException(nameof(holding), holding, "in_progress is a claim."),
                };
                issue.ClaimFor(holder, kind, false, at, FourHours);
                break;
        }

        Assert.Equal(status, issue.Status);
        return issue;
    }
}
