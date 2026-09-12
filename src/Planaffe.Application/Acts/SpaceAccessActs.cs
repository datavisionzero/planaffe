using Planaffe.Application.Ports;
using Planaffe.Domain;
using Planaffe.Domain.Spaces;

namespace Planaffe.Application.Acts;

/// <summary>
/// Who sees a space. The mirror of <see cref="ListProjectUsers"/> and its two
/// siblings, down to the order of the checks: an administrator grants, the
/// creator was granted when the space was created, and an agent neither grants
/// nor is granted — it inherits its owner (VISION 12, ADR 0015, ADR 0027).
/// </summary>
public sealed class ListSpaceUsers(ICallerIdentity callerIdentity, ISpaces spaces, ISpaceAccess access, InstanceSettings settings)
{
    public async Task<IReadOnlyList<UserSummary>> ExecuteAsync(string name, CancellationToken cancellationToken)
    {
        var caller = callerIdentity.Caller.RequireUser("list the users of a space");
        var space = await SpaceAdministration.LiveAsync(spaces, name, settings, cancellationToken);

        // An administrator reaches the list without being named on the space —
        // administering is not access — and everybody else has to be on it.
        if (!caller.Administrator && !await access.HasAsync(caller.Id, space.Id, cancellationToken))
        {
            throw new Refusal(RefusalCode.NotFound, $"No space {name}.");
        }

        return [.. (await access.UsersAsync(space.Id, cancellationToken)).Select(UserSummary.Of)];
    }
}

public sealed class GrantSpaceAccess(
    ICallerIdentity callerIdentity,
    ISpaces spaces,
    ISpaceAccess access,
    IIdentities identities,
    InstanceSettings settings,
    TimeProvider clock)
{
    public async Task ExecuteAsync(string name, Guid userId, CancellationToken cancellationToken)
    {
        var administrator = callerIdentity.Caller.RequireAdministrator("grant access to a space");
        var space = await SpaceAdministration.LiveAsync(spaces, name, settings, cancellationToken);
        var user = await identities.FindUserAsync(userId, cancellationToken)
            ?? throw new Refusal(RefusalCode.NotFound, $"No user {userId}.");

        await access.GrantAsync(
            SpaceAccess.Grant(space.Id, user.Id, administrator.Id, clock.GetUtcNow()), cancellationToken);
    }
}

public sealed class RevokeSpaceAccess(
    ICallerIdentity callerIdentity, ISpaces spaces, ISpaceAccess access, IIdentities identities, InstanceSettings settings)
{
    public async Task ExecuteAsync(string name, Guid userId, CancellationToken cancellationToken)
    {
        callerIdentity.Caller.RequireAdministrator("revoke access to a space");
        var space = await SpaceAdministration.LiveAsync(spaces, name, settings, cancellationToken);
        _ = await identities.FindUserAsync(userId, cancellationToken)
            ?? throw new Refusal(RefusalCode.NotFound, $"No user {userId}.");

        // Revoking what was never granted is not an error: the state asked for
        // is the state afterwards, as with the project.
        await access.RevokeAsync(space.Id, userId, cancellationToken);
    }
}

/// <summary>
/// The space these three act on, by name and live. It is deliberately not the
/// scope's lookup: an administrator manages a space they are not named on, and
/// the space itself — a name and a title — is not the content the scope
/// guards.
/// </summary>
file static class SpaceAdministration
{
    public static async Task<Space> LiveAsync(
        ISpaces spaces, string name, InstanceSettings settings, CancellationToken cancellationToken)
    {
        var normalized = name?.Trim() ?? string.Empty;
        var space = (Slug.IsValid(normalized) ? await spaces.FindAnyAsync(normalized, cancellationToken) : null)
            ?? throw new Refusal(RefusalCode.NotFound, $"No space {normalized}.");

        return space.Deleted
            ? throw new Refusal(
                RefusalCode.Deleted,
                $"Space {space.Name} is deleted and can be restored until at least {space.DeletedAt!.Value + settings.DeletionGrace:u}.",
                new Dictionary<string, object?> { ["restorable_until"] = space.DeletedAt.Value + settings.DeletionGrace })
            : space;
    }
}
