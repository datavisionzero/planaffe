namespace Planaffe.Domain.Projects;

/// <summary>
/// The bracket every piece of content belongs to, carrying the project key that
/// prefixes everything in it (<c>CONTEXT.md</c>, Project). It is not a
/// repository.
/// </summary>
/// <remarks>
/// <para>
/// The two switches are the two places the product asks what an agent's word is
/// worth: <see cref="TriageRequired"/> guards the entrance (VISION 10),
/// <see cref="ReviewRequired"/> the exit (VISION 9, ADR 0014). Both are off by
/// default, because a solo developer who trusts their agents should click
/// nothing.
/// </para>
/// <para>
/// <see cref="LastIssueNumber"/> and <see cref="LastEpicNumber"/> are the two
/// counters every key in the project is drawn from, allocated under the row's
/// lock by the store (<c>docs/storage.md</c>, Keys are allocated from the project
/// row). They only go up: a key is never reused, not after a deletion either
/// (ADR 0013). Nothing here increments them — that is one statement in the
/// transaction that inserts the row, and it lives with the SQL.
/// </para>
/// </remarks>
public sealed class Project
{
    public const int NameMaxLength = 100;

    private Project()
    {
        // EF Core materializes through this; every other route goes through Create.
    }

    private Project(Guid id, string key, string name, Guid createdBy, DateTimeOffset createdAt)
    {
        Id = id;
        Key = key;
        Name = name;
        CreatedBy = createdBy;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public Guid Id { get; private init; }

    /// <summary>Never changed after creation (ADR 0015).</summary>
    public string Key { get; private init; } = null!;

    public string Name { get; private set; } = null!;

    public bool TriageRequired { get; private set; }

    public bool ReviewRequired { get; private set; }

    /// <summary>
    /// The text every agent is handed with every ticket
    /// (<c>CONTEXT.md</c>, Instructions; VISION 15.3), or <c>null</c> where the
    /// project carries none. Markdown, like a description, and deliberately a
    /// field rather than a document somewhere else: it travels inside the
    /// context package (VISION 15.5), so it has to follow project access and no
    /// second rule. In a space it could be closed to the agent that needs it or
    /// invisible to somebody who has the project, and a text that is sometimes
    /// delivered is worse than one that is always there (VISION 18).
    /// </summary>
    /// <remarks>
    /// One text and not a mark on any number of documents: the context budget
    /// is the resource the whole idea is about (VISION 6.1), and whoever needs
    /// more instructions writes them in here and notices it growing.
    /// </remarks>
    public string? Instructions { get; private set; }

    public int LastIssueNumber { get; private init; }

    public int LastEpicNumber { get; private init; }

    public Guid CreatedBy { get; private init; }

    public DateTimeOffset CreatedAt { get; private init; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public Guid? DeletedBy { get; private set; }

    public bool Deleted => DeletedAt is not null;

    public static Project Create(string key, string name, Guid createdBy, DateTimeOffset createdAt) =>
        new(Guid.CreateVersion7(), ProjectKey.Normalize(key), NormalizeName(name), createdBy, createdAt);

    /// <summary>
    /// Write the text every agent is handed with its ticket, or take it away.
    /// Blank and <c>null</c> are the same thing, as everywhere a body is
    /// written: a project carrying an empty instruction carries none.
    /// </summary>
    public void Instruct(string? instructions, DateTimeOffset at)
    {
        var trimmed = instructions?.Trim();
        Instructions = string.IsNullOrEmpty(trimmed) ? null : trimmed;
        UpdatedAt = at;
    }

    public void Rename(string name, DateTimeOffset at)
    {
        Name = NormalizeName(name);
        UpdatedAt = at;
    }

    /// <summary>The entrance guard (VISION 10): on, `ready` is a user's word and binding for `next`.</summary>
    public void RequireTriage(bool required, DateTimeOffset at)
    {
        TriageRequired = required;
        UpdatedAt = at;
    }

    /// <summary>The exit guard (VISION 9, ADR 0014): on, an agent's close lands in `review`.</summary>
    public void RequireReview(bool required, DateTimeOffset at)
    {
        ReviewRequired = required;
        UpdatedAt = at;
    }

    /// <summary>
    /// The soft delete of ADR 0013, with everything in the project: invisible,
    /// restorable for the grace period, gone after the purge. The key stays
    /// taken meanwhile.
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

    /// <summary>Back, with everything in it, into whatever state it was.</summary>
    public void Restore()
    {
        DeletedAt = null;
        DeletedBy = null;
    }

    /// <exception cref="ArgumentException">
    /// <paramref name="name"/> is blank or longer than <see cref="NameMaxLength"/>.
    /// </exception>
    public static string NormalizeName(string name)
    {
        var trimmed = name?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            throw new ArgumentException("A project has a name.", nameof(name));
        }

        return trimmed.Length > NameMaxLength
            ? throw new ArgumentException(
                $"A project name is at most {NameMaxLength} characters.", nameof(name))
            : trimmed;
    }
}
