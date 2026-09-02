namespace Planaffe.Domain.Identities;

/// <summary>
/// An AI identity that reaches the instance through the API under exactly one
/// agent token, and is owned by the user who created it (<c>CONTEXT.md</c>,
/// Agent).
/// </summary>
/// <remarks>
/// Never an administrator, whoever owns it (ADR 0015): there is no act here that
/// could make it one, and the table's check constraint refuses the row if
/// anything else tries. What an agent reports about itself — the metadata back
/// channel of VISION 12 — is a cut-two column.
/// </remarks>
public sealed class Agent : Identity
{
    private Agent()
    {
    }

    private Agent(Guid id, string name, Guid ownerId, DateTimeOffset createdAt)
        : base(id, name, administrator: false, createdAt)
    {
        OwnerId = ownerId;
    }

    /// <summary>
    /// The user who created this agent. Ownership answers <em>which</em> human
    /// is behind a token; it does not make the agent's acts that human's acts
    /// (ADR 0015).
    /// </summary>
    public Guid OwnerId { get; private init; }

    public override IdentityKind Kind => IdentityKind.Agent;

    public static Agent Create(string name, Guid ownerId, DateTimeOffset createdAt) =>
        new(Guid.CreateVersion7(), NormalizeName(name), ownerId, createdAt);
}
