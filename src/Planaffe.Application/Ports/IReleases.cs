using Planaffe.Domain.Issues;
using Planaffe.Domain.Releases;

namespace Planaffe.Application.Ports;

public interface IReleases
{
    Task<IReadOnlyList<Release>> ListAsync(Guid projectId, CancellationToken cancellationToken);
    Task<Release?> FindAsync(Guid projectId, string name, CancellationToken cancellationToken);
    Task<Release?> LoadForWriteAsync(Guid projectId, string name, CancellationToken cancellationToken);
    Task<Release?> LoadOpenForWriteAsync(Guid projectId, CancellationToken cancellationToken);
    Task<IReadOnlyList<IssueRow>> IssuesAsync(Guid releaseId, CancellationToken cancellationToken);
    Task<IReadOnlyDictionary<Guid, string>> CurrentNamesAsync(IReadOnlyCollection<Guid> issueIds, CancellationToken cancellationToken);
    Task<bool> NameTakenAsync(Guid projectId, string name, CancellationToken cancellationToken);
    Task<bool> InPublishedAsync(Guid issueId, CancellationToken cancellationToken);
    Task AddDoneAsync(Issue issue, CancellationToken cancellationToken);
    Task RemoveFromOpenAsync(Guid issueId, CancellationToken cancellationToken);
    void Add(Release release);
    Task SaveAsync(CancellationToken cancellationToken);
}
