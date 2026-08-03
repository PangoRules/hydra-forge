namespace HydraForge.Application.Chat;

using System.Linq.Expressions;

/// <summary>
/// Abstracts background job enqueueing so the Application layer is not coupled to Hangfire.
/// </summary>
public interface IBackgroundTaskQueue
{
    Task EnqueueAsync(Func<CancellationToken, Task> workItem, CancellationToken ct = default);

    Task EnqueueJobAsync<TJob>(Expression<Func<TJob, Task>> methodCall);
}
