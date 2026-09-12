using Planaffe.Application.Ports;
using Planaffe.Domain;
using Planaffe.Domain.Spaces;

namespace Planaffe.Application.Acts;

/// <summary>
/// A space as every route returns it. There is no slim variant: a space is a
/// name, a title and a switch, so the list and the single read are the same
/// shape — what ADR 0012 makes slim is a body, and a space has none.
/// </summary>
public sealed record SpaceShape(
    string Name,
    string Title,
    bool ClosedToAgents,
    IdentityRef Author,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? DeletedAt)
{
    public static SpaceShape Of(Space space, IdentityRef author)
    {
        ArgumentNullException.ThrowIfNull(space);

        return new SpaceShape(
            space.Name, space.Title, space.ClosedToAgents, author, space.CreatedAt, space.UpdatedAt, space.DeletedAt);
    }
}

public sealed record CreateSpaceRequest(string? Name, string? Title, bool ClosedToAgents = false);

public sealed record SpaceChanges(string? Name, string? Title, bool? ClosedToAgents);

/// <summary>Turns space rows into their shape, resolving the authors once for the whole list.</summary>
public sealed class SpaceAssembler(IIdentities identities)
{
    public async Task<IReadOnlyList<SpaceShape>> ShapesAsync(
        IReadOnlyList<Space> rows, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(rows);

        if (rows.Count == 0)
        {
            return [];
        }

        var people = new Dictionary<Guid, IdentityRef>();
        foreach (var id in rows.Select(s => s.CreatedBy).Distinct())
        {
            people[id] = IdentityRef.Of(
                await identities.FindAsync(id, cancellationToken)
                ?? throw new InvalidOperationException($"Identity {id} has no row."));
        }

        return [.. rows.Select(s => SpaceShape.Of(s, people[s.CreatedBy]))];
    }

    public async Task<SpaceShape> ShapeAsync(Space space, CancellationToken cancellationToken) =>
        (await ShapesAsync([space], cancellationToken))[0];
}

/// <summary>
/// The one authorization decision every space act uses — the mirror of
/// <see cref="ProjectScope"/>, with the one thing a project has no equivalent
/// of: an agent's set is its owner's minus every space closed to agents
/// (ADR 0027).
/// </summary>
/// <remarks>
/// The subtraction lives here and nowhere else. A caller that asked the access
/// rows directly would have the owner's spaces, closed ones included, which is
/// exactly the mistake this type exists to make impossible.
/// </remarks>
public sealed class SpaceScope(ICallerIdentity callerIdentity, ISpaceAccess access, ISpaces spaces)
{
    /// <summary>An agent resolves to its owner, as everywhere else (VISION 12).</summary>
    public Guid UserId => callerIdentity.Caller.OwnerId ?? callerIdentity.Caller.Id;

    public async Task<IReadOnlySet<Guid>> SpaceIdsAsync(CancellationToken cancellationToken)
    {
        var granted = await access.SpaceIdsAsync(UserId, cancellationToken);

        return callerIdentity.Caller.IsAgent
            ? await spaces.OpenToAgentsAsync([.. granted], cancellationToken)
            : granted;
    }

    /// <exception cref="Refusal"><c>not-found</c> — never <c>forbidden</c>, which would confirm the space.</exception>
    public async Task RequireAsync(Guid spaceId, CancellationToken cancellationToken)
    {
        if (!(await SpaceIdsAsync(cancellationToken)).Contains(spaceId))
        {
            throw new Refusal(RefusalCode.NotFound, "No such space or space content.");
        }
    }
}

/// <summary>The lookup every space act starts with: the name, then the scope.</summary>
public static class SpaceLookup
{
    /// <summary>
    /// By name, deleted or not, and only if the caller may see it. A space
    /// nobody may see and a space that does not exist answer the same, for the
    /// agent with a closed space as much as for the stranger (ADR 0027).
    /// </summary>
    /// <exception cref="Refusal"><c>not-found</c>.</exception>
    public static async Task<Space> AnyAsync(
        this ISpaces spaces, SpaceScope scope, string name, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(spaces);
        ArgumentNullException.ThrowIfNull(scope);

        // An address that is not a slug names nothing, and says so as
        // `not-found` rather than as `validation`: it arrived in the path.
        var normalized = name?.Trim() ?? string.Empty;
        var space = Slug.IsValid(normalized)
            ? await spaces.FindAnyAsync(normalized, cancellationToken)
            : null;

        if (space is null)
        {
            throw new Refusal(RefusalCode.NotFound, $"No space {normalized}.");
        }

        await scope.RequireAsync(space.Id, cancellationToken);
        return space;
    }

    /// <exception cref="Refusal"><c>not-found</c>, or <c>deleted</c> with <c>restorable_until</c>.</exception>
    public static async Task<Space> LiveAsync(
        this ISpaces spaces, SpaceScope scope, string name, InstanceSettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var space = await spaces.AnyAsync(scope, name, cancellationToken);

        return space.Deleted
            ? throw new Refusal(
                RefusalCode.Deleted,
                $"Space {space.Name} is deleted and can be restored until at least {space.DeletedAt!.Value + settings.DeletionGrace:u}.",
                new Dictionary<string, object?> { ["restorable_until"] = space.DeletedAt.Value + settings.DeletionGrace })
            : space;
    }

    /// <exception cref="Refusal"><c>validation</c> on <c>name</c>, or <c>deleted</c> when one is waiting out its grace period.</exception>
    public static async Task TakenAsync(
        this ISpaces spaces, string name, InstanceSettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(spaces);
        ArgumentNullException.ThrowIfNull(settings);

        // Deliberately outside the scope: whether a name is free is not a
        // question about a space the caller may see. The answer says a space
        // is deleted and never which, so nothing leaks but the name itself —
        // and the name is what the caller just proposed.
        var standing = await spaces.FindAnyAsync(name, cancellationToken);
        if (standing is null)
        {
            return;
        }

        throw standing.Deleted
            ? new Refusal(
                RefusalCode.Deleted,
                $"The name {name} belongs to a deleted space and is spent until at least {standing.DeletedAt!.Value + settings.DeletionGrace:u}.",
                new Dictionary<string, object?> { ["restorable_until"] = standing.DeletedAt.Value + settings.DeletionGrace })
            : Refusal.Validation("name", $"The name {name} is taken.");
    }
}

/// <summary>The spaces the caller may see, by name. An agent's list has the closed ones subtracted.</summary>
public sealed class ListSpaces(ISpaces spaces, SpaceScope scope, SpaceAssembler assembler)
{
    public async Task<IReadOnlyList<SpaceShape>> ExecuteAsync(CancellationToken cancellationToken) =>
        await assembler.ShapesAsync(
            await spaces.ListAsync([.. await scope.SpaceIdsAsync(cancellationToken)], cancellationToken),
            cancellationToken);
}

/// <summary>
/// Every space on the instance, deleted ones included — the administration's
/// list, and the only way to find a deleted space in order to restore it. It
/// shows no content: administering is not access (VISION 12).
/// </summary>
public sealed class ListAdminSpaces(ICallerIdentity callerIdentity, ISpaces spaces, SpaceAssembler assembler)
{
    public async Task<IReadOnlyList<SpaceShape>> ExecuteAsync(CancellationToken cancellationToken)
    {
        callerIdentity.Caller.RequireAdministrator("list all spaces");

        return await assembler.ShapesAsync(await spaces.ListAllAsync(cancellationToken), cancellationToken);
    }
}

public sealed class ReadSpace(ISpaces spaces, SpaceScope scope, SpaceAssembler assembler, InstanceSettings settings)
{
    public async Task<SpaceShape> ExecuteAsync(string name, CancellationToken cancellationToken) =>
        await assembler.ShapeAsync(
            await spaces.LiveAsync(scope, name, settings, cancellationToken), cancellationToken);
}

/// <summary>
/// A space and its creator's access in one transaction. A user's act and never
/// an agent's: whoever may open brackets decides what it may read next
/// (ADR 0015, ADR 0027).
/// </summary>
public sealed class CreateSpace(
    ICallerIdentity callerIdentity,
    ISpaces spaces,
    ITransactions transactions,
    SpaceAssembler assembler,
    InstanceSettings settings,
    TimeProvider clock)
{
    public async Task<SpaceShape> ExecuteAsync(CreateSpaceRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var caller = callerIdentity.Caller.RequireUser("create a space");

        var name = Validated.Field("name", () => Slug.Normalize(request.Name ?? string.Empty, "name"));
        var title = Validated.Field("title", () => Space.NormalizeTitle(request.Title!));

        await spaces.TakenAsync(name, settings, cancellationToken);

        var space = await transactions.RunAsync(async () =>
        {
            var now = clock.GetUtcNow();
            var created = Space.Create(name, title, caller.Id, now);

            if (request.ClosedToAgents)
            {
                created.CloseToAgents(true, now);
            }

            // The creator is named on it, as with a project: whoever opens a
            // bracket is in it without anybody granting them anything.
            await spaces.AddAsync(created, SpaceAccess.Grant(created.Id, caller.Id, caller.Id, now), cancellationToken);
            return created;
        }, cancellationToken);

        return await assembler.ShapeAsync(space, cancellationToken);
    }
}

/// <summary>
/// The name, the title and the switch. A user with access, never an agent —
/// an agent that could open a closed space would be granting itself what the
/// switch took away.
/// </summary>
public sealed class ChangeSpace(
    ICallerIdentity callerIdentity,
    ISpaces spaces,
    SpaceScope scope,
    ITransactions transactions,
    SpaceAssembler assembler,
    InstanceSettings settings,
    TimeProvider clock)
{
    public async Task<SpaceShape> ExecuteAsync(string name, SpaceChanges changes, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(changes);
        callerIdentity.Caller.RequireUser("change a space");

        var space = await spaces.LiveAsync(scope, name, settings, cancellationToken);
        var renamed = changes.Name is null ? null : Validated.Field("name", () => Slug.Normalize(changes.Name, "name"));

        if (renamed is not null && renamed != space.Name)
        {
            await spaces.TakenAsync(renamed, settings, cancellationToken);
        }

        await transactions.RunAsync(async () =>
        {
            var now = clock.GetUtcNow();

            if (renamed is not null && renamed != space.Name)
            {
                space.Rename(renamed, now);
            }

            if (changes.Title is not null && changes.Title != space.Title)
            {
                Validated.Field("title", () => { space.Retitle(changes.Title, now); return true; });
            }

            if (changes.ClosedToAgents is { } closed && closed != space.ClosedToAgents)
            {
                space.CloseToAgents(closed, now);
            }

            await spaces.SaveAsync(cancellationToken);
            return true;
        }, cancellationToken);

        return await assembler.ShapeAsync(space, cancellationToken);
    }
}

/// <summary>
/// Delete and restore: soft, with the grace period of everything else
/// (ADR 0013), and an administrator's act as with a project — a bracket and
/// what hangs in it do not go on one person's word.
/// </summary>
public sealed class MoveSpace(
    ICallerIdentity callerIdentity,
    ISpaces spaces,
    ITransactions transactions,
    SpaceAssembler assembler,
    TimeProvider clock)
{
    public async Task DeleteAsync(string name, CancellationToken cancellationToken)
    {
        var caller = callerIdentity.Caller.RequireAdministrator("delete a space");
        var space = await Administered(spaces, name, cancellationToken);

        if (space.Deleted)
        {
            return;
        }

        await transactions.RunAsync(async () =>
        {
            space.Delete(caller.Id, clock.GetUtcNow());
            await spaces.SaveAsync(cancellationToken);
            return true;
        }, cancellationToken);
    }

    public async Task<SpaceShape> RestoreAsync(string name, CancellationToken cancellationToken)
    {
        callerIdentity.Caller.RequireAdministrator("restore a space");
        var space = await Administered(spaces, name, cancellationToken);

        if (!space.Deleted)
        {
            throw new Refusal(RefusalCode.Transition, $"Space {space.Name} is not deleted.");
        }

        await transactions.RunAsync(async () =>
        {
            space.Restore();
            await spaces.SaveAsync(cancellationToken);
            return true;
        }, cancellationToken);

        return await assembler.ShapeAsync(space, cancellationToken);
    }

    /// <summary>
    /// An administrator reaches a space by name without being named on it —
    /// as with a project, where administering and access are separate things
    /// (VISION 12). It is the one route past <see cref="SpaceScope"/>, and it
    /// hands out no content: the caller gets the row it just deleted or
    /// restored, and nothing that is in it.
    /// </summary>
    private static async Task<Space> Administered(ISpaces spaces, string name, CancellationToken cancellationToken)
    {
        var normalized = name?.Trim() ?? string.Empty;

        return (Slug.IsValid(normalized) ? await spaces.FindAnyAsync(normalized, cancellationToken) : null)
            ?? throw new Refusal(RefusalCode.NotFound, $"No space {normalized}.");
    }
}
