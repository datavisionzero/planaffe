using Planaffe.Application.Ports;
using Planaffe.Domain;
using Planaffe.Domain.Identities;

namespace Planaffe.Application.Acts;

/// <summary>
/// An administrator creates an invited user and sends the one-time activation
/// link (<c>docs/api.md</c>, Users, agents and tokens).
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

        var normalizedEmail = Validated.Field("email", () => User.NormalizeEmail(email!));
        if (await identities.FindUserByNormalizedEmailAsync(User.NormalizeEmailForComparison(normalizedEmail), cancellationToken) is not null)
            throw new Refusal(RefusalCode.EmailExists, "That email address already belongs to a user.");

        var now = clock.GetUtcNow();
        var user = User.Invite(normalized, normalizedEmail, administrator, now);
        await identities.AddUserAsync(user, cancellationToken);

        // Without SMTP the user is invited all the same and no secret is
        // issued here, because nobody could be shown one: the administrator
        // asks for the invitation link and hands it over themselves. Refusing
        // the whole act was what left an instance without email unable to add
        // a second person at all (ADR 0018).
        if (smtp.Configured)
        {
            var invitation = OneTimeSecret.Issue(user.Id, OneTimeSecretPurpose.Invitation, now);
            await secrets.AddReplacingLiveAsync(invitation.Record, now, cancellationToken);
            var link = new Uri(smtp.PublicUrl!, $"/activate?secret={Uri.EscapeDataString(invitation.Secret)}");
            await emailSender.SendAsync(TransactionalEmailTemplates.Invitation(user.Email, user.Name, link), cancellationToken);
        }

        return UserSummary.Of(user);
    }
}

/// <summary>A link an administrator hands over themselves, and the moment it stops working.</summary>
public sealed record AccessLink(string Link, DateTimeOffset ExpiresAt);

/// <summary>
/// The way back into a browser account that does not go through an email.
/// </summary>
/// <remarks>
/// <para>
/// Transactional email is optional (ADR 0018) and password recovery was the
/// only way to a new password, so an instance without SMTP locked out whoever
/// forgot theirs — an administrator could do nothing about it, and the last
/// active administrator cannot be deactivated, so the instance itself was shut
/// with them. An invitation had the same hole: without email nobody could be
/// added at all.
/// </para>
/// <para>
/// Both are closed the same way and not on two different ones: the same
/// one-time secret that an email would have carried is issued and shown to the
/// administrator, who transports it themselves. An administrator therefore
/// never learns anybody's password — only the person sets it, which is the
/// difference the history could not make visible afterwards.
/// </para>
/// <para>
/// It is not bound to the absence of SMTP. A button that is there or not there
/// depending on an operating setting explains itself to nobody, and the link
/// is worth having beside an email that is slow, filtered or misaddressed.
/// Issuing one replaces whatever live secret of the same purpose that user
/// had, so a link that was mailed a minute ago stops working — which is what
/// makes handing this one over an answer rather than a second door.
/// </para>
/// </remarks>
public sealed class IssueAccessLink(ICallerIdentity callerIdentity, IIdentities identities, IOneTimeSecrets secrets,
    SmtpSettings smtp, TimeProvider clock)
{
    public async Task<AccessLink> ExecuteAsync(string? address, OneTimeSecretPurpose purpose, CancellationToken cancellationToken)
    {
        callerIdentity.Caller.RequireAdministrator("issue an access link");

        var id = await IdentityAddress.ResolveAsync(address, identities, cancellationToken);
        var user = (id is null ? null : await identities.FindUserAsync(id.Value, cancellationToken))
            ?? throw new Refusal(RefusalCode.NotFound, $"No user {address}.");

        var (required, screen, wrongState) = purpose switch
        {
            OneTimeSecretPurpose.Invitation => (UserState.Invited, "/activate",
                "Only an invited user has an invitation to hand over."),
            OneTimeSecretPurpose.PasswordRecovery => (UserState.Active, "/recover",
                "Only an active user can be given a password link."),
            _ => throw new ArgumentOutOfRangeException(nameof(purpose), purpose, "A link is an invitation or a password recovery."),
        };

        if (user.State != required) throw new Refusal(RefusalCode.Transition, wrongState);

        var now = clock.GetUtcNow();
        var issued = OneTimeSecret.Issue(user.Id, purpose, now);
        await secrets.AddReplacingLiveAsync(issued.Record, now, cancellationToken);

        // A URI reference, and absolute only where the instance knows its own
        // address. `PLANAFFE_PUBLIC_URL` is the one place that is said, and an
        // instance that has not been told it would otherwise invent a host from
        // a request header — which is exactly what that variable exists not to
        // do. Whoever asked reached the instance somehow, and resolves it
        // against that.
        var reference = $"{screen}?secret={Uri.EscapeDataString(issued.Secret)}";
        var link = smtp.PublicUrl is null ? reference : new Uri(smtp.PublicUrl, reference).ToString();
        return new AccessLink(link, issued.Record.ExpiresAt);
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

/// <summary>A user changes the name shown on future history entries.</summary>
public sealed class RenameUser(ICallerIdentity callerIdentity, IIdentities identities)
{
    public async Task<UserSummary> ExecuteAsync(string? name, CancellationToken cancellationToken)
    {
        var caller = callerIdentity.Caller.RequireUser("change their name");
        var user = await identities.FindUserAsync(caller.Id, cancellationToken)
            ?? throw new Refusal(RefusalCode.NotFound, "No user has that id.");
        Validated.Field("name", () => { user.Rename(name!); return true; });
        await identities.RecordUserAsync(user, cancellationToken);
        return UserSummary.Of(user);
    }
}

public sealed class ResendInvitation(ICallerIdentity callerIdentity, IIdentities identities, IOneTimeSecrets secrets,
    IEmailSender emailSender, SmtpSettings smtp, TimeProvider clock)
{
    public async Task ExecuteAsync(string? address, CancellationToken cancellationToken)
    {
        callerIdentity.Caller.RequireAdministrator("resend an invitation");
        if (!smtp.Configured) throw new Refusal(RefusalCode.SmtpNotConfigured, "Transactional email is not configured for this instance.");
        var id = await IdentityAddress.ResolveAsync(address, identities, cancellationToken);
        var user = (id is null ? null : await identities.FindUserAsync(id.Value, cancellationToken))
            ?? throw new Refusal(RefusalCode.NotFound, $"No user {address}.");
        if (user.State != UserState.Invited) throw new Refusal(RefusalCode.Transition, "Only an invited user has an invitation to resend.");
        var issued = OneTimeSecret.Issue(user.Id, OneTimeSecretPurpose.Invitation, clock.GetUtcNow());
        await secrets.AddReplacingLiveAsync(issued.Record, clock.GetUtcNow(), cancellationToken);
        var link = new Uri(smtp.PublicUrl!, $"/activate?secret={Uri.EscapeDataString(issued.Secret)}");
        await emailSender.SendAsync(TransactionalEmailTemplates.Invitation(user.Email, user.Name, link), cancellationToken);
    }
}

public sealed class ChangeUserLifecycle(ICallerIdentity callerIdentity, IIdentities identities, TimeProvider clock)
{
    public async Task<UserSummary> ExecuteAsync(string? address, UserLifecycleChange change, CancellationToken cancellationToken)
    {
        callerIdentity.Caller.RequireAdministrator("change a user's lifecycle");
        var id = await IdentityAddress.ResolveAsync(address, identities, cancellationToken)
            ?? throw new Refusal(RefusalCode.NotFound, $"No user {address}.");
        var outcome = await identities.ChangeLifecycleAsync(id, change, clock.GetUtcNow(), cancellationToken);
        if (outcome == UserLifecycleOutcome.NotFound) throw new Refusal(RefusalCode.NotFound, $"No user {address}.");
        if (outcome == UserLifecycleOutcome.LastAdministrator)
            throw new Refusal(RefusalCode.LastAdministrator, "Deactivation or demotion would leave no active administrator.");
        if (outcome == UserLifecycleOutcome.InvalidState)
            throw new Refusal(RefusalCode.Transition, "The user is already in the requested state.");
        return UserSummary.Of((await identities.FindUserAsync(id, cancellationToken))!);
    }
}

public sealed class RequestEmailChange(ICallerIdentity callerIdentity, IIdentities identities, IOneTimeSecrets secrets,
    IEmailSender emailSender, SmtpSettings smtp, TimeProvider clock)
{
    public async Task ExecuteAsync(string? email, CancellationToken cancellationToken)
    {
        var caller = callerIdentity.Caller.RequireUser("change an email address");
        if (!smtp.Configured) throw new Refusal(RefusalCode.SmtpNotConfigured, "Transactional email is not configured for this instance.");
        var normalized = Validated.Field("email", () => User.NormalizeEmail(email!));
        if (await identities.FindUserByNormalizedEmailAsync(User.NormalizeEmailForComparison(normalized), cancellationToken) is not null)
            throw new Refusal(RefusalCode.EmailExists, "That email address already belongs to a user.");
        var user = await identities.FindUserAsync(caller.Id, cancellationToken) ?? throw new Refusal(RefusalCode.NotFound, "No user has that id.");
        var issued = OneTimeSecret.Issue(user.Id, OneTimeSecretPurpose.EmailChange, clock.GetUtcNow(), normalized);
        await secrets.AddReplacingLiveAsync(issued.Record, clock.GetUtcNow(), cancellationToken);
        var link = new Uri(smtp.PublicUrl!, $"/confirm-email?secret={Uri.EscapeDataString(issued.Secret)}");
        await emailSender.SendAsync(TransactionalEmailTemplates.EmailConfirmation(normalized, user.Name, link), cancellationToken);
    }
}

public sealed class ConfirmEmailChange(IOneTimeSecrets secrets, IIdentities identities, TimeProvider clock)
{
    public async Task ExecuteAsync(string? secret, CancellationToken cancellationToken)
    {
        byte[] hash;
        try { hash = OneTimeSecret.Hash(secret!); }
        catch (Exception exception) when (exception is ArgumentException or FormatException)
        { throw Refusal.Validation("secret", "A valid email-change secret is required."); }
        var record = await secrets.ConsumeAsync(hash, OneTimeSecretPurpose.EmailChange, clock.GetUtcNow(), cancellationToken)
            ?? throw new Refusal(RefusalCode.SecretExpired, "The email-change link is expired, replaced, or has already been used.");
        var user = await identities.FindUserAsync(record.UserId, cancellationToken)
            ?? throw new Refusal(RefusalCode.SecretExpired, "The email-change link is no longer usable.");
        user.ChangeEmail(record.PendingEmail!);
        await identities.RecordUserAsync(user, cancellationToken);
    }
}
