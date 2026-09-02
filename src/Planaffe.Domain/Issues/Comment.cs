namespace Planaffe.Domain.Issues;

/// <summary>
/// A note on an issue that forces nobody to act (<c>CONTEXT.md</c>, Comment).
/// Written once and kept; cut one has no edit and no delete for it.
/// </summary>
public sealed class Comment
{
    private Comment()
    {
        // EF Core materializes through this; every other route goes through Write.
    }

    private Comment(Guid id, Guid issueId, Guid authorId, string body, DateTimeOffset createdAt)
    {
        Id = id;
        IssueId = issueId;
        AuthorId = authorId;
        Body = body;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private init; }

    public Guid IssueId { get; private init; }

    public Guid AuthorId { get; private init; }

    public string Body { get; private init; } = null!;

    public DateTimeOffset CreatedAt { get; private init; }

    /// <exception cref="ArgumentException"><paramref name="body"/> is blank.</exception>
    public static Comment Write(Guid issueId, Guid authorId, string body, DateTimeOffset createdAt) =>
        string.IsNullOrWhiteSpace(body)
            ? throw new ArgumentException("A comment says something.", nameof(body))
            : new(Guid.CreateVersion7(), issueId, authorId, body.Trim(), createdAt);
}
