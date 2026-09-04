namespace Planaffe.Application.Ports;

/// <summary>
/// The wake-up channel for a project's changing work. A notification carries
/// no state: after this returns, the caller asks its original question again.
/// </summary>
public interface IChanges
{
    Task WaitAsync(Guid projectId, CancellationToken cancellationToken);
}
