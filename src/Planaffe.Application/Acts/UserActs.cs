using Planaffe.Application.Ports;
using Planaffe.Domain;
using Planaffe.Domain.Identities;

namespace Planaffe.Application.Acts;

/// <summary>
/// The invitation of cut one: an administrator creates a user and hands over
/// their first user token (<c>docs/api.md</c>, Users, agents and tokens). Cut
/// three replaces the secret in the answer with a one-time link (VISION 12).
/// </summary>
public sealed class CreateUser(ICallerIdentity callerIdentity, IIdentities identities, TimeProvider clock)
{
    public async Task<CreatedUser> ExecuteAsync(string? name, bool administrator, CancellationToken cancellationToken)
    {
        callerIdentity.Caller.RequireAdministrator("create a user");

        var normalized = Validated.Field("name", () => Identity.NormalizeName(name!));
        if (await identities.NameTakenAsync(normalized, cancellationToken))
        {
            throw Refusal.Validation("name", $"The name {normalized} is taken; names are unique across users and agents, whatever the case.");
        }

        var now = clock.GetUtcNow();
        var user = User.Create(normalized, administrator, now);
        var secret = TokenSecret.Generate();
        var token = Token.Issue(user, secret, now);

        await identities.AddAsync(user, token, cancellationToken);

        return new CreatedUser(
            user.Id, user.Kind, user.Name, user.Administrator, user.CreatedAt,
            new IssuedToken(token.Id, token.Prefix, secret, token.CreatedAt));
    }
}

/// <summary>Every user. The list is people; it is not paginated.</summary>
public sealed class ListUsers(ICallerIdentity callerIdentity, IIdentities identities)
{
    public async Task<IReadOnlyList<UserSummary>> ExecuteAsync(CancellationToken cancellationToken)
    {
        callerIdentity.Caller.RequireUser("list users");

        return [.. (await identities.ListUsersAsync(cancellationToken)).Select(UserSummary.Of)];
    }
}
