using Planaffe.Domain.Issues;

namespace Planaffe.Application.Acts;

/// <summary>The claim as a list shows it: who, since when, and when it lapses (<c>null</c> for a user's).</summary>
public sealed record ClaimShape(IdentityRef Holder, DateTimeOffset Since, DateTimeOffset? ExpiresAt);

/// <summary>A blocker in the slim issue: the key and whether it is still open. A hidden one is <c>null</c> key (cut three).</summary>
public sealed record BlockerRefShape(string? Key, bool Open);

/// <summary>A blocker, or a blocked issue, in the complete issue.</summary>
public sealed record BlockerLinkShape(string? Key, string? Title, IssueStatus? Status, bool Open);

/// <summary>The slim issue every list returns (ADR 0012, <c>docs/api.md</c>).</summary>
public sealed record IssueSummaryShape(
    string Key,
    string Project,
    string Title,
    IssueStatus Status,
    bool Ready,
    Priority Priority,
    IReadOnlyList<string> Labels,
    string? Epic,
    string? Parent,
    IdentityRef? Assignee,
    ClaimShape? Claim,
    IReadOnlyList<BlockerRefShape> BlockedBy,
    int OpenQuestions,
    int OpenBlockers,
    int OpenSubIssues,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? ClosedAt,
    DateTimeOffset? DeletedAt,
    IdentityRef? DeletedBy);

public sealed record EpicRefShape(string Key, string Title, string Description, Domain.Epics.EpicStatus Status);

public sealed record CommentShape(Guid Id, IdentityRef Author, string Body, DateTimeOffset CreatedAt);

public sealed record QuestionShape(
    Guid Id,
    string Question,
    IdentityRef AskedBy,
    DateTimeOffset AskedAt,
    string? Answer,
    IdentityRef? AnsweredBy,
    DateTimeOffset? AnsweredAt);

/// <summary>The project as the complete issue carries it: the switches and the labels with their descriptions.</summary>
public sealed record ProjectContextShape(
    string Key, string Name, bool TriageRequired, bool ReviewRequired, IReadOnlyList<LabelShape> Labels);

/// <summary>
/// The complete issue — the context package of cut one (VISION 15.5): the
/// ticket, its comments and questions, the epic's description, and the
/// project's labels with their descriptions, in one read.
/// </summary>
public sealed record IssueShape(
    string Key,
    string Project,
    string Title,
    string Description,
    string? Result,
    IssueStatus Status,
    bool Ready,
    Priority Priority,
    IReadOnlyList<LabelShape> Labels,
    EpicRefShape? Epic,
    IssueRefShape? Parent,
    IReadOnlyList<IssueRefShape> SubIssues,
    IdentityRef? Assignee,
    ClaimShape? Claim,
    IdentityRef Author,
    IReadOnlyList<BlockerLinkShape> BlockedBy,
    IReadOnlyList<BlockerLinkShape> Blocks,
    int OpenQuestions,
    int OpenBlockers,
    int OpenSubIssues,
    IReadOnlyList<CommentShape> Comments,
    IReadOnlyList<QuestionShape> Questions,
    ProjectContextShape ProjectContext,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? ClosedAt);

/// <summary>A page of slim issues (<c>docs/api.md</c>, Pagination).</summary>
public sealed record IssuePage(IReadOnlyList<IssueSummaryShape> Items, int Total, bool HasMore, string? NextCursor);

/// <summary>What <c>POST /issues</c> answers: the complete issues, in the order given.</summary>
public sealed record CreatedIssues(IReadOnlyList<IssueShape> Items);
