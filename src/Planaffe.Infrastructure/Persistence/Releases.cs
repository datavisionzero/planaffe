using Microsoft.EntityFrameworkCore;
using Npgsql;
using Planaffe.Application.Ports;
using Planaffe.Domain;
using Planaffe.Domain.Issues;
using Planaffe.Domain.Releases;

namespace Planaffe.Infrastructure.Persistence;

/// <summary>
/// The release rows and what they record (<c>docs/storage.md</c>, Releases).
/// </summary>
/// <remarks>
/// Every write of a project's releases — closing into the open one, reopening
/// out of it, publishing, retracting, renaming, and the two hand edits — takes
/// one lock per project first and holds it to the end of the transaction, and
/// only then loads what it writes, in a statement of its own that sees what
/// the writer it waited for wrote. A publication replaces the open release row
/// with a new one, so a lock on that row was waited for and then found
/// nothing: the close behind a publish failed as if the project had no open
/// release at all. The lock is a transaction-level advisory lock rather than
/// the project row, because allocating an issue number takes the project row
/// and then locks a parent, while a close holds its issue and then asks for
/// this — two orders that would deadlock.
/// </remarks>
public sealed class Releases(PlanaffeDbContext context) : IReleases
{
    // The first of the two advisory keys says what the lock is for, the second
    // whose it is. Two projects whose ids hash alike share a lock and merely
    // wait for each other.
    private const int ReleaseWrites = 1;

    public async Task<IReadOnlyList<Release>> ListAsync(Guid projectId, CancellationToken ct) =>
        await context.Releases.AsNoTracking().Where(r => r.ProjectId == projectId)
            .OrderBy(r => r.Status == ReleaseStatus.Open ? 0 : 1).ThenByDescending(r => r.PublishedAt).ToListAsync(ct);

    public Task<Release?> FindAsync(Guid projectId, string name, CancellationToken ct) =>
        name.Equals("unreleased", StringComparison.OrdinalIgnoreCase)
            ? context.Releases.AsNoTracking().SingleOrDefaultAsync(r => r.ProjectId == projectId && r.Status == ReleaseStatus.Open, ct)
            : context.Releases.AsNoTracking().SingleOrDefaultAsync(r => r.ProjectId == projectId && r.Name != null && r.Name.ToLower() == name.ToLower(), ct);

    public async Task<Release?> LoadForWriteAsync(Guid projectId, string name, CancellationToken ct)
    {
        await LockAsync(projectId, ct);
        return name.Equals("unreleased", StringComparison.OrdinalIgnoreCase)
            ? await context.Releases.SingleOrDefaultAsync(r => r.ProjectId == projectId && r.Status == ReleaseStatus.Open, ct)
            : await context.Releases.SingleOrDefaultAsync(r => r.ProjectId == projectId && r.Name != null && r.Name.ToLower() == name.ToLower(), ct);
    }

    public async Task<Release?> LoadOpenForWriteAsync(Guid projectId, CancellationToken ct)
    {
        await LockAsync(projectId, ct);
        return await context.Releases.SingleOrDefaultAsync(r => r.ProjectId == projectId && r.Status == ReleaseStatus.Open, ct);
    }

    public async Task<IReadOnlyList<IssueRow>> IssuesAsync(Guid releaseId, CancellationToken ct) =>
        await (from ri in context.ReleaseIssues.AsNoTracking()
               join i in context.IssueReads on ri.IssueId equals i.Id
               join p in context.Projects on i.ProjectId equals p.Id
               where ri.ReleaseId == releaseId && p.DeletedAt == null
               orderby i.ParentId ?? i.Id, i.ParentId == null ? 0 : 1, i.Number
               select new IssueRow
               {
                   Id = i.Id,
                   ProjectId = i.ProjectId,
                   ProjectKey = p.Key,
                   Number = i.Number,
                   Title = i.Title,
                   Description = i.Description,
                   Result = i.Result,
                   Status = i.Status,
                   Ready = i.Ready,
                   Priority = i.Priority,
                   AssigneeId = i.AssigneeId,
                   EpicId = i.EpicId,
                   ParentId = i.ParentId,
                   ClaimedBy = i.ClaimedBy,
                   ClaimedAt = i.ClaimedAt,
                   ClaimExpiresAt = i.ClaimExpiresAt,
                   AuthorId = i.AuthorId,
                   CreatedAt = i.CreatedAt,
                   UpdatedAt = i.UpdatedAt,
                   ClosedAt = i.ClosedAt
               }).ToListAsync(ct);

    public async Task<IReadOnlyDictionary<Guid, string>> CurrentNamesAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct)
    {
        if (ids.Count == 0) return new Dictionary<Guid, string>();
        var rows = await (from ri in context.ReleaseIssues.AsNoTracking()
                          join r in context.Releases.AsNoTracking() on ri.ReleaseId equals r.Id
                          where ids.Contains(ri.IssueId)
                          orderby r.Status == ReleaseStatus.Open ? 0 : 1, r.PublishedAt descending
                          select new { ri.IssueId, Name = r.Name ?? "unreleased" }).ToListAsync(ct);
        return rows.GroupBy(x => x.IssueId).ToDictionary(g => g.Key, g => g.First().Name);
    }

    public Task<bool> NameTakenAsync(Guid projectId, string name, CancellationToken ct) =>
        context.Releases.AnyAsync(r => r.ProjectId == projectId && r.Name != null && r.Name.ToLower() == name.ToLower(), ct);

    public Task<Release?> LatestPublishedAsync(Guid projectId, CancellationToken ct) =>
        context.Releases.Where(r => r.ProjectId == projectId && r.Status == ReleaseStatus.Published)
            .OrderByDescending(r => r.PublishedAt).FirstOrDefaultAsync(ct);

    public Task<int> IssueCountAsync(Guid releaseId, CancellationToken ct) =>
        context.ReleaseIssues.CountAsync(x => x.ReleaseId == releaseId, ct);

    public async Task<bool> AttachAsync(Guid releaseId, Guid issueId, CancellationToken ct)
    {
        if (await context.ReleaseIssues.AnyAsync(x => x.ReleaseId == releaseId && x.IssueId == issueId, ct)) return false;
        context.ReleaseIssues.Add(ReleaseIssue.Attach(releaseId, issueId));
        return true;
    }

    public async Task<bool> DetachAsync(Guid releaseId, Guid issueId, CancellationToken ct) =>
        await context.ReleaseIssues.Where(x => x.ReleaseId == releaseId && x.IssueId == issueId).ExecuteDeleteAsync(ct) > 0;

    public void Remove(Release release) => context.Releases.Remove(release);

    public Task<bool> InPublishedAsync(Guid issueId, CancellationToken ct) =>
        (from ri in context.ReleaseIssues
         join r in context.Releases on ri.ReleaseId equals r.Id
         where ri.IssueId == issueId && r.Status == ReleaseStatus.Published
         select ri).AnyAsync(ct);

    public async Task AddDoneAsync(Issue issue, CancellationToken ct)
    {
        if (issue.Status != IssueStatus.Done) return;
        var open = await LoadOpenForWriteAsync(issue.ProjectId, ct) ?? throw new InvalidOperationException("Project has no open release.");
        List<Guid> ids;
        if (issue.ParentId is { } parentId)
        {
            // Read after the lock and untracked: the parent is not this act's
            // to write, and a reopen of it waits for the same lock.
            var parentStatus = await context.Issues.AsNoTracking().Where(i => i.Id == parentId).Select(i => i.Status).SingleAsync(ct);
            if (parentStatus != IssueStatus.Done) return;

            // A parent that shipped already stays where it shipped; the
            // sub-issue closed after it enters the open release alone.
            ids = await InPublishedAsync(parentId, ct) ? [issue.Id] : [parentId, issue.Id];
        }
        else
        {
            ids = await context.Issues.Where(i => i.Id == issue.Id || i.ParentId == issue.Id && i.Status == IssueStatus.Done && i.DeletedAt == null).Select(i => i.Id).ToListAsync(ct);
        }

        foreach (var id in ids.Distinct())
            if (!await context.ReleaseIssues.AnyAsync(x => x.ReleaseId == open.Id && x.IssueId == id, ct)) context.ReleaseIssues.Add(ReleaseIssue.Attach(open.Id, id));
    }

    public async Task RemoveFromOpenAsync(Guid issueId, CancellationToken ct)
    {
        var projectId = await context.Issues.AsNoTracking().Where(i => i.Id == issueId).Select(i => i.ProjectId).SingleAsync(ct);
        await LockAsync(projectId, ct);
        await (from ri in context.ReleaseIssues
               join r in context.Releases on ri.ReleaseId equals r.Id
               join i in context.Issues on ri.IssueId equals i.Id
               where (i.Id == issueId || i.ParentId == issueId) && r.Status == ReleaseStatus.Open
               select ri).ExecuteDeleteAsync(ct);
    }

    public void Add(Release release) => context.Releases.Add(release);

    // The lock keeps release writes from racing each other, so neither of
    // these is expected; where one happens anyway, it is the refusal it stands
    // for and not a 500.
    public async Task SaveAsync(CancellationToken ct)
    {
        try
        {
            await context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException collision) when (collision.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "release_name" or "release_open",
        } unique)
        {
            throw unique.ConstraintName == "release_name"
                ? new Refusal(RefusalCode.ReleaseExists, "A release of that name already exists.")
                : new Refusal(RefusalCode.Transition, "The open release changed while this was written; ask again.");
        }
    }

    private Task LockAsync(Guid projectId, CancellationToken ct) =>
        context.Database.CurrentTransaction is null
            ? throw new InvalidOperationException("A release is written inside a transaction, or the lock is worth nothing.")
            : context.Database.ExecuteSqlRawAsync(
                "select pg_advisory_xact_lock({0}, hashtext({1}::text))", [ReleaseWrites, projectId], ct);
}
