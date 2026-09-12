namespace Planaffe.Domain.Spaces;

/// <summary>
/// A Markdown document in a space, with a place in a tree at most three levels
/// deep (<c>CONTEXT.md</c>, Space page; VISION 18, ADR 0028). It is the page of
/// VISION 7 with the one thing the bracket asked for: children.
/// </summary>
/// <remarks>
/// <para>
/// The prefix in the name says which of two pages the product means. The
/// project's flat wiki is <see cref="Pages.Page"/> and is being withdrawn
/// (VISION 18); until it is, the two live in two tables, so that nothing about
/// the older one has to become nullable for a transition. When the project's
/// wiki goes, the prefix goes with it and what is left is the page.
/// </para>
/// <para>
/// It carries no labels. A label is defined per project and a space has none,
/// so this is a consequence of the bracket rather than something forgotten.
/// </para>
/// <para>
/// <see cref="UpdatedAt"/> is the version, as at the issue, the epic and the
/// project's page, so that this page inherits the guarded write of
/// <c>docs/api.md</c> ("Concurrency on text fields") rather than carrying a
/// mechanism of its own.
/// </para>
/// <para>
/// Uniqueness of the slug is the store's and the database's, not this type's:
/// it needs the other children of the same parent. A deleted page keeps its
/// slug until the purge takes it, so that restoring one never lands on a name
/// somebody else has taken (ADR 0013).
/// </para>
/// </remarks>
public sealed class SpacePage
{
    public const int TitleMaxLength = 200;

    /// <summary>
    /// The deepest a page may sit: <c>0</c> directly under the space, so three
    /// levels in all. Hard and deliberate, like the sub-issue's one level —
    /// whoever needs a fourth has found a second space (VISION 18).
    /// </summary>
    public const int MaxDepth = 2;

    private SpacePage()
    {
        // EF Core materializes through this; every other route goes through Create.
    }

    private SpacePage(
        Guid id,
        Guid spaceId,
        Guid? parentId,
        int depth,
        string slug,
        string title,
        string body,
        Guid createdBy,
        DateTimeOffset createdAt)
    {
        Id = id;
        SpaceId = spaceId;
        ParentId = parentId;
        Depth = depth;
        Slug = slug;
        Title = title;
        Body = body;
        CreatedBy = createdBy;
        CreatedAt = createdAt;
        UpdatedBy = createdBy;
        UpdatedAt = createdAt;
    }

    public Guid Id { get; private init; }

    /// <summary>The bracket it hangs in, and the only thing carrying access (ADR 0027).</summary>
    public Guid SpaceId { get; private set; }

    /// <summary>Its parent, or nothing where it hangs directly under the space.</summary>
    public Guid? ParentId { get; private set; }

    /// <summary><c>0</c> directly under the space, at most <see cref="MaxDepth"/>.</summary>
    public int Depth { get; private set; }

    /// <summary>One segment of the address, unique under the parent (ADR 0028).</summary>
    public string Slug { get; private set; } = null!;

    public string Title { get; private set; } = null!;

    /// <summary>Markdown, rendered in the browser and never as HTML (ADR 0007).</summary>
    public string Body { get; private set; } = null!;

    public Guid CreatedBy { get; private init; }

    public DateTimeOffset CreatedAt { get; private init; }

    /// <summary>Who made the last change — what a tree shows without reading the history.</summary>
    public Guid UpdatedBy { get; private set; }

    /// <summary>The version a guarded write is compared against.</summary>
    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public Guid? DeletedBy { get; private set; }

    /// <summary>
    /// The page whose deletion took this one along, where it went as part of a
    /// subtree rather than on its own. Restoring that page brings back exactly
    /// the rows carrying its id, which is why this is a column and not a
    /// comparison of timestamps: two deletions can share a moment, and in a
    /// test with a standing clock they reliably do.
    /// </summary>
    public Guid? DeletedWith { get; private set; }

    public bool Deleted => DeletedAt is not null;

    /// <summary>
    /// A page under <paramref name="parent"/>, or directly under the space
    /// where that is <c>null</c>. The depth is derived from the parent and
    /// never passed in, so that no caller can place a page deeper than the
    /// limit allows.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// <paramref name="parent"/> hangs in another space, or is already at
    /// <see cref="MaxDepth"/>.
    /// </exception>
    public static SpacePage Create(
        Guid spaceId,
        SpacePage? parent,
        string slug,
        string title,
        string? body,
        Guid createdBy,
        DateTimeOffset createdAt) =>
        new(
            Guid.CreateVersion7(),
            spaceId,
            parent?.Id,
            DepthUnder(spaceId, parent),
            Domain.Slug.Normalize(slug),
            NormalizeTitle(title),
            body ?? string.Empty,
            createdBy,
            createdAt);

    /// <summary>The address changes and the old one leads nowhere; nothing forwards (ADR 0021, ADR 0028).</summary>
    public void Rename(string slug, Guid by, DateTimeOffset at)
    {
        Slug = Domain.Slug.Normalize(slug);
        Touch(by, at);
    }

    public void Retitle(string title, Guid by, DateTimeOffset at)
    {
        Title = NormalizeTitle(title);
        Touch(by, at);
    }

    /// <summary>The document itself; <c>null</c> empties it.</summary>
    public void Rewrite(string? body, Guid by, DateTimeOffset at)
    {
        Body = body ?? string.Empty;
        Touch(by, at);
    }

    /// <summary>
    /// Under another parent, in this space or another one. This moves the page
    /// alone: its descendants follow in the same transaction, written by the
    /// store, because a subtree is not something to load into memory one row
    /// at a time. What this type answers for is the page's own depth; that the
    /// deepest descendant still fits is the act's question, and
    /// <see cref="Room"/> is what it asks.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// <paramref name="parent"/> hangs in another space, or is already at
    /// <see cref="MaxDepth"/>.
    /// </exception>
    public void MoveUnder(Guid spaceId, SpacePage? parent, Guid by, DateTimeOffset at)
    {
        var depth = DepthUnder(spaceId, parent);

        SpaceId = spaceId;
        ParentId = parent?.Id;
        Depth = depth;
        Touch(by, at);
    }

    /// <summary>
    /// How many levels of descendants a page at <paramref name="depth"/> still
    /// has room for: <c>2</c> directly under the space, <c>0</c> at the bottom.
    /// </summary>
    public static int Room(int depth) => MaxDepth - depth;

    /// <summary>A change that is not to a field of its own moves the version too, so that a guarded write sees it.</summary>
    public void Touch(Guid by, DateTimeOffset at)
    {
        UpdatedBy = by;
        UpdatedAt = at;
    }

    /// <summary>
    /// Soft, with the grace period of everything else; the slug stays spent
    /// until the purge (ADR 0013). <paramref name="with"/> names the page this
    /// one went along with, and is nothing on the page the deletion was asked
    /// for.
    /// </summary>
    public void Delete(Guid by, DateTimeOffset at, Guid? with = null)
    {
        if (Deleted)
        {
            return;
        }

        DeletedAt = at;
        DeletedBy = by;
        DeletedWith = with;
    }

    public void Restore()
    {
        DeletedAt = null;
        DeletedBy = null;
        DeletedWith = null;
    }

    /// <exception cref="ArgumentException">
    /// <paramref name="title"/> is blank, spans lines, or is longer than
    /// <see cref="TitleMaxLength"/>.
    /// </exception>
    public static string NormalizeTitle(string title)
    {
        var trimmed = title?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            throw new ArgumentException("A page has a title.", nameof(title));
        }

        return trimmed.Length > TitleMaxLength || trimmed.Contains('\n')
            ? throw new ArgumentException(
                $"A page title is one line of at most {TitleMaxLength} characters.", nameof(title))
            : trimmed;
    }

    private static int DepthUnder(Guid spaceId, SpacePage? parent)
    {
        if (parent is null)
        {
            return 0;
        }

        if (parent.SpaceId != spaceId)
        {
            throw new ArgumentException("A page hangs under a parent of its own space.", nameof(parent));
        }

        return parent.Depth < MaxDepth
            ? parent.Depth + 1
            : throw new ArgumentException(
                $"A page sits at most {MaxDepth + 1} levels under its space.", nameof(parent));
    }
}
