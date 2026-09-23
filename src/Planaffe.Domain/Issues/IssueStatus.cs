namespace Planaffe.Domain.Issues;

/// <summary>
/// The one fixed set an issue moves through (<c>CONTEXT.md</c>, Status). Not
/// configurable, no variants: <c>backlog</c> and <c>todo</c> answer
/// <em>when</em>, <c>in_progress</c> is a claim, <c>review</c> waits for a human,
/// and the two closed ones are a decision that stays visible.
/// </summary>
/// <remarks>
/// A status is not written by a client; it changes through the acts of ADR
/// 0016, parking excepted. An issue is born in <see cref="Todo"/> (VISION 9).
/// </remarks>
public enum IssueStatus
{
    Backlog,
    Todo,
    InProgress,
    Review,
    Done,
    Canceled,
}

/// <summary>A status the way the wire, the history and every sentence spell it.</summary>
public static class IssueStatusNames
{
    /// <summary><c>in_progress</c>, not <c>inprogress</c>: the one spelling of <c>docs/api.md</c>.</summary>
    public static string Name(this IssueStatus status) => status switch
    {
        IssueStatus.Backlog => "backlog",
        IssueStatus.Todo => "todo",
        IssueStatus.InProgress => "in_progress",
        IssueStatus.Review => "review",
        IssueStatus.Done => "done",
        IssueStatus.Canceled => "canceled",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Not a status."),
    };
}
