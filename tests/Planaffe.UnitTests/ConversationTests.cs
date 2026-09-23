using Planaffe.Domain.Issues;

namespace Planaffe.UnitTests;

/// <summary>The question that is a state and the comment its author may correct (VISION 7, ADR 0022).</summary>
public sealed class ConversationTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_question_is_answered_once_and_trimmed()
    {
        var by = Guid.NewGuid();
        var question = Question.Ask(Guid.NewGuid(), Guid.NewGuid(), "  Which Postgres? ", Guid.NewGuid(), Now.AddHours(-1));
        Assert.Equal("Which Postgres?", question.Text);
        Assert.True(question.Open);

        Assert.Throws<ArgumentException>(() => question.AnswerWith("  ", by, Now));
        Assert.True(question.Open);

        question.AnswerWith(" 18. ", by, Now);
        Assert.False(question.Open);
        Assert.Equal("18.", question.Answer);
        Assert.Equal(by, question.AnsweredBy);
        Assert.Equal(Now, question.AnsweredAt);

        Assert.Throws<InvalidOperationException>(() => question.AnswerWith("17.", Guid.NewGuid(), Now.AddHours(1)));
        Assert.Equal("18.", question.Answer);
    }

    [Fact]
    public void A_question_says_on_what()
    {
        Assert.Throws<ArgumentException>(() => Question.Ask(Guid.NewGuid(), Guid.NewGuid(), " ", Guid.NewGuid(), Now));
    }

    [Fact]
    public void A_comment_is_rewritten_visibly_and_never_to_nothing()
    {
        var comment = Comment.Write(Guid.NewGuid(), Guid.NewGuid(), " First thought. ", Now.AddHours(-1));
        Assert.Equal("First thought.", comment.Body);
        Assert.Null(comment.EditedAt);

        Assert.Throws<ArgumentException>(() => comment.Rewrite("   ", Now));
        Assert.Null(comment.EditedAt);

        comment.Rewrite(" Second thought. ", Now);
        Assert.Equal("Second thought.", comment.Body);
        Assert.Equal(Now, comment.EditedAt);
        Assert.Equal(Now.AddHours(-1), comment.CreatedAt);
    }

    [Fact]
    public void A_comment_says_something()
    {
        Assert.Throws<ArgumentException>(() => Comment.Write(Guid.NewGuid(), Guid.NewGuid(), "", Now));
    }
}
