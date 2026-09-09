using Microsoft.EntityFrameworkCore;
using Planaffe.Application.Ports;
using Planaffe.Domain.Identities;

namespace Planaffe.Infrastructure.Persistence;

public sealed class OneTimeSecrets(PlanaffeDbContext context) : IOneTimeSecrets
{
    public async Task AddReplacingLiveAsync(OneTimeSecret secret, DateTimeOffset now, CancellationToken ct)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(ct);
        await context.OneTimeSecrets.Where(x => x.UserId == secret.UserId && x.Purpose == secret.Purpose && x.UsedAt == null)
            .ExecuteUpdateAsync(set => set.SetProperty(x => x.UsedAt, now), ct);
        context.OneTimeSecrets.Add(secret); await context.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
    }
    public async Task<OneTimeSecret?> ConsumeAsync(byte[] secretHash, OneTimeSecretPurpose purpose, DateTimeOffset now, CancellationToken ct)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(ct);
        var purposeName = purpose switch
        {
            OneTimeSecretPurpose.Invitation => "invitation",
            OneTimeSecretPurpose.PasswordRecovery => "password_recovery",
            OneTimeSecretPurpose.EmailChange => "email_change",
            _ => throw new ArgumentOutOfRangeException(nameof(purpose)),
        };
        var secret = await context.OneTimeSecrets.FromSqlInterpolated($"select * from one_time_secret where secret_hash = {secretHash} and purpose = {purposeName} for update").SingleOrDefaultAsync(ct);
        if (secret is null || !secret.IsLive(now)) return null;
        secret.Consume(now); await context.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return secret;
    }
}

public sealed class BrowserSessions(PlanaffeDbContext context) : IBrowserSessions
{
    public async Task AddAsync(BrowserSession session, CancellationToken ct) { context.BrowserSessions.Add(session); await context.SaveChangesAsync(ct); }
    public async Task<SessionUser?> AuthenticateAsync(byte[] hash, DateTimeOffset now, CancellationToken ct)
    {
        var row = await (from session in context.BrowserSessions join user in context.Users on session.UserId equals user.Id where session.SecretHash == hash select new { session, user }).SingleOrDefaultAsync(ct);
        if (row is null || row.user.State != UserState.Active || !row.session.IsValid(now)) return null;
        if (row.session.Touch(now)) await context.SaveChangesAsync(ct);
        return new(row.session, row.user);
    }
    public Task<IReadOnlyList<BrowserSession>> ListAsync(Guid userId, CancellationToken ct) => ListCore(userId, ct);
    private async Task<IReadOnlyList<BrowserSession>> ListCore(Guid userId, CancellationToken ct) => await context.BrowserSessions.Where(x => x.UserId == userId).OrderByDescending(x => x.CreatedAt).ToListAsync(ct);
    public Task RevokeAsync(Guid id, Guid userId, DateTimeOffset now, CancellationToken ct) => context.BrowserSessions.Where(x => x.Id == id && x.UserId == userId && x.RevokedAt == null).ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAt, now), ct);
    public Task RevokeAllAsync(Guid userId, Guid? except, DateTimeOffset now, CancellationToken ct) => context.BrowserSessions.Where(x => x.UserId == userId && x.Id != except && x.RevokedAt == null).ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAt, now), ct);
}

/// <summary>
/// The device-login rows (ADR 0025). Two of the four reads and writes here are
/// conditional updates rather than a load-and-save, for the reason claiming is:
/// the row is what two callers race for.
/// </summary>
public sealed class DeviceLogins(PlanaffeDbContext context) : IDeviceLogins
{
    /// <summary>How long an expired row is kept so the CLI is told why, not just "no".</summary>
    private static readonly TimeSpan SweepGrace = TimeSpan.FromHours(1);

    public async Task AddAsync(DeviceLogin login, DateTimeOffset now, CancellationToken ct)
    {
        await context.DeviceLogins.Where(x => x.ExpiresAt < now - SweepGrace).ExecuteDeleteAsync(ct);
        context.DeviceLogins.Add(login);
        await context.SaveChangesAsync(ct);
    }

    // The newest row wearing that code: `one_live_login_per_code` keeps two
    // undecided ones apart, and a decided one may share a code with the login a
    // person has just been shown.
    public Task<DeviceLogin?> FindByUserCodeAsync(string userCode, CancellationToken ct) =>
        context.DeviceLogins.Where(x => x.UserCode == userCode)
            .OrderByDescending(x => x.CreatedAt).FirstOrDefaultAsync(ct);

    public async Task<ConfirmedDeviceLogin?> FindByDeviceCodeHashAsync(byte[] hash, CancellationToken ct)
    {
        var row = await (from login in context.DeviceLogins
                         where login.DeviceCodeHash == hash
                         join user in context.Users on login.ApprovedByUserId equals user.Id into approvers
                         from approver in approvers.DefaultIfEmpty()
                         select new { login, approver }).SingleOrDefaultAsync(ct);

        return row is null ? null : new ConfirmedDeviceLogin(row.login, row.approver);
    }

    // Guarded rather than saved, for the reason redeeming is: two browsers can
    // hold the same short code, and the second one must be told rather than
    // quietly overwrite whose login this became.
    public async Task<bool> TryRecordDecisionAsync(DeviceLogin login, CancellationToken ct) =>
        await context.DeviceLogins
            .Where(x => x.Id == login.Id && x.ApprovedAt == null && x.DeniedAt == null && x.RedeemedAt == null)
            .ExecuteUpdateAsync(set => set
                .SetProperty(x => x.ApprovedAt, login.ApprovedAt)
                .SetProperty(x => x.ApprovedByUserId, login.ApprovedByUserId)
                .SetProperty(x => x.DeniedAt, login.DeniedAt), ct) == 1;

    public async Task<bool> TryRedeemAsync(DeviceLogin login, Token token, CancellationToken ct)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(ct);

        // The guard is the whole act: whoever moves `redeemed_at` from null
        // writes the token, and the other poll writes nothing.
        var redeemed = await context.DeviceLogins
            .Where(x => x.Id == login.Id && x.RedeemedAt == null)
            .ExecuteUpdateAsync(set => set
                .SetProperty(x => x.RedeemedAt, login.RedeemedAt)
                .SetProperty(x => x.IssuedTokenId, login.IssuedTokenId), ct);

        if (redeemed == 0)
        {
            await transaction.RollbackAsync(ct);
            return false;
        }

        context.Tokens.Add(token);
        await context.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return true;
    }
}
