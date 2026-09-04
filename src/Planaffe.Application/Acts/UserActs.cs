using Planaffe.Application.Ports;
using Planaffe.Domain;
using Planaffe.Domain.Identities;

namespace Planaffe.Application.Acts;

/// <summary>
/// The invitation of cut one: an administrator creates a user and hands over
/// their first user token (<c>docs/api.md</c>, Users, agents and tokens). Cut
/// three replaces the secret in the answer with a one-time link (VISION 12).
/// </summary>
public sealed class CreateUser(ICallerIdentity callerIdentity, IIdentities identities, IOneTimeSecrets secrets,
    IEmailSender emailSender, SmtpSettings smtp, TimeProvider clock)
{
    public async Task<UserSummary> ExecuteAsync(string? name, string? email, bool administrator, CancellationToken cancellationToken)
    {
        callerIdentity.Caller.RequireAdministrator("create a user");

        var normalized = Validated.Field("name", () => Identity.NormalizeName(name!));
        if (await identities.NameTakenAsync(normalized, cancellationToken))
        {
            throw Refusal.Validation("name", $"The name {normalized} is taken; names are unique across users and agents, whatever the case.");
        }

        if (!smtp.Configured)
            throw new Refusal(RefusalCode.SmtpNotConfigured, "Transactional email is not configured for this instance.");

        var normalizedEmail = Validated.Field("email", () => User.NormalizeEmail(email!));
        if (await identities.FindUserByNormalizedEmailAsync(User.NormalizeEmailForComparison(normalizedEmail), cancellationToken) is not null)
            throw new Refusal(RefusalCode.EmailExists, "That email address already belongs to a user.");

        var now = clock.GetUtcNow();
        var user = User.Invite(normalized, normalizedEmail, administrator, now);
        await identities.AddUserAsync(user, cancellationToken);
        var invitation = OneTimeSecret.Issue(user.Id, OneTimeSecretPurpose.Invitation, now);
        await secrets.AddReplacingLiveAsync(invitation.Record, now, cancellationToken);
        var link = new Uri(smtp.PublicUrl!, $"/activate?secret={Uri.EscapeDataString(invitation.Secret)}");
        await emailSender.SendAsync(TransactionalEmailTemplates.Invitation(user.Email, user.Name, link), cancellationToken);
        return UserSummary.Of(user);
    }
}

/// <summary>Every user. The list is people; it is not paginated.</summary>
public sealed class ListUsers(ICallerIdentity callerIdentity, IIdentities identities)
{
    public async Task<IReadOnlyList<UserSummary>> ExecuteAsync(CancellationToken cancellationToken)
    {
        callerIdentity.Caller.RequireAdministrator("list users");

        return [.. (await identities.ListUsersAsync(cancellationToken)).Select(UserSummary.Of)];
    }
}
