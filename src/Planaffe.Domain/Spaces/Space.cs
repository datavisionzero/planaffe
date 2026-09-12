namespace Planaffe.Domain.Spaces;

/// <summary>
/// The bracket of the knowledge base and the only thing in it that carries
/// access (<c>CONTEXT.md</c>, Space; VISION 18, ADR 0027): a company, a domain,
/// a customer, a handbook. Pages hang in it, nothing hangs beside it.
/// </summary>
/// <remarks>
/// <para>
/// It is addressed by its <see cref="Name"/> and not by a key, for the reason
/// ADR 0021 gives for the page: a space is named in running text and never
/// quoted in a commit message. The name is unique across the instance, because
/// there is no bracket above a space in which it could be unique instead.
/// </para>
/// <para>
/// <see cref="ClosedToAgents"/> is the one switch a space carries. It names the
/// agent rather than the interface, so that it holds on every route an agent
/// ever gets — the MCP server included — and it is off by default, like the
/// project's two switches: whoever trusts their agents should click nothing
/// (VISION 4).
/// </para>
/// <para>
/// Creating a space, renaming it, deleting it and setting that switch are a
/// human's acts. This type does not know who is asking; the acts establish it,
/// as they do everywhere else in the domain.
/// </para>
/// </remarks>
public sealed class Space
{
    public const int TitleMaxLength = 200;

    private Space()
    {
        // EF Core materializes through this; every other route goes through Create.
    }

    private Space(Guid id, string name, string title, Guid createdBy, DateTimeOffset createdAt)
    {
        Id = id;
        Name = name;
        Title = title;
        CreatedBy = createdBy;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public Guid Id { get; private init; }

    /// <summary>The address, unique across the instance and renameable (ADR 0021, ADR 0027).</summary>
    public string Name { get; private set; } = null!;

    public string Title { get; private set; } = null!;

    /// <summary>
    /// Whether an agent may reach this space at all. Closed, it is not a space
    /// an agent may see and not read: it is not there — not in a list, not in a
    /// search result, not under its own address (ADR 0027).
    /// </summary>
    public bool ClosedToAgents { get; private set; }

    public Guid CreatedBy { get; private init; }

    public DateTimeOffset CreatedAt { get; private init; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public Guid? DeletedBy { get; private set; }

    public bool Deleted => DeletedAt is not null;

    public static Space Create(string name, string title, Guid createdBy, DateTimeOffset createdAt) =>
        new(Guid.CreateVersion7(), Slug.Normalize(name, "name"), NormalizeTitle(title), createdBy, createdAt);

    /// <summary>The address changes and the old one leads nowhere; nothing forwards (ADR 0021).</summary>
    public void Rename(string name, DateTimeOffset at)
    {
        Name = Slug.Normalize(name, "name");
        UpdatedAt = at;
    }

    public void Retitle(string title, DateTimeOffset at)
    {
        Title = NormalizeTitle(title);
        UpdatedAt = at;
    }

    /// <summary>Close the space to agents, or open it again (VISION 18).</summary>
    public void CloseToAgents(bool closed, DateTimeOffset at)
    {
        ClosedToAgents = closed;
        UpdatedAt = at;
    }

    /// <summary>
    /// The soft delete of ADR 0013, with the pages in it: invisible, restorable
    /// for the grace period, gone after the purge. The name stays taken
    /// meanwhile, so that a restore never lands on a name somebody else has
    /// taken.
    /// </summary>
    public void Delete(Guid by, DateTimeOffset at)
    {
        if (Deleted)
        {
            return;
        }

        DeletedAt = at;
        DeletedBy = by;
    }

    public void Restore()
    {
        DeletedAt = null;
        DeletedBy = null;
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
            throw new ArgumentException("A space has a title.", nameof(title));
        }

        return trimmed.Length > TitleMaxLength || trimmed.Contains('\n')
            ? throw new ArgumentException(
                $"A space title is one line of at most {TitleMaxLength} characters.", nameof(title))
            : trimmed;
    }
}
