using Planaffe.Domain.Identities;
using Planaffe.Domain.Spaces;

namespace Planaffe.Application.Ports;

/// <summary>
/// Who sees which space — the mirror of <see cref="IProjectAccess"/>, and
/// deliberately nothing more than that (ADR 0027).
/// </summary>
public interface ISpaceAccess
{
    Task<bool> HasAsync(Guid userId, Guid spaceId, CancellationToken cancellationToken);

    Task<IReadOnlySet<Guid>> SpaceIdsAsync(Guid userId, CancellationToken cancellationToken);

    Task<IReadOnlyList<User>> UsersAsync(Guid spaceId, CancellationToken cancellationToken);

    Task GrantAsync(SpaceAccess access, CancellationToken cancellationToken);

    Task RevokeAsync(Guid spaceId, Guid userId, CancellationToken cancellationToken);
}
