using Microsoft.EntityFrameworkCore;
using Planaffe.Application.Ports;
using Planaffe.Domain.History;

namespace Planaffe.Infrastructure.Persistence;

/// <summary>The history rows, appended with the write they record and saved with it.</summary>
public sealed class History(PlanaffeDbContext context) : IHistory
{
    public void Add(HistoryEntry entry) => context.History.Add(entry);

    public async Task<IReadOnlyList<HistoryEntry>> ListAsync(Guid issueId, CancellationToken cancellationToken) =>
        await context.History.Where(h => h.IssueId == issueId).OrderBy(h => h.Id).ToListAsync(cancellationToken);

    public Task<HistoryEntry?> LastAsync(Guid issueId, string field, CancellationToken cancellationToken) =>
        context.History
            .Where(h => h.IssueId == issueId && h.Field == field)
            .OrderByDescending(h => h.Id)
            .FirstOrDefaultAsync(cancellationToken);
}
