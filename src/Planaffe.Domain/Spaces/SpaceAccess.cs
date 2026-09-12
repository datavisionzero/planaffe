namespace Planaffe.Domain.Spaces;

/// <summary>
/// The assignment of a space to a user — the same coarse model the project has,
/// a second time and not a second kind (VISION 12, ADR 0027). Agents inherit
/// their owner's assignments and therefore never have rows of their own, minus
/// every space that is closed to them.
/// </summary>
public sealed class SpaceAccess
{
    private SpaceAccess() { }

    private SpaceAccess(Guid spaceId, Guid userId, Guid grantedBy, DateTimeOffset grantedAt)
    {
        SpaceId = spaceId;
        UserId = userId;
        GrantedBy = grantedBy;
        GrantedAt = grantedAt;
    }

    public Guid SpaceId { get; private init; }

    public Guid UserId { get; private init; }

    public Guid GrantedBy { get; private init; }

    public DateTimeOffset GrantedAt { get; private init; }

    public static SpaceAccess Grant(Guid spaceId, Guid userId, Guid grantedBy, DateTimeOffset grantedAt) =>
        new(spaceId, userId, grantedBy, grantedAt);
}
