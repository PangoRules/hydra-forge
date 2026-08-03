namespace HydraForge.Infrastructure.Chat;

using HydraForge.Application.Chat;
using Hangfire;
using System.Linq.Expressions;

public sealed class HangfireBackgroundTaskQueue : IBackgroundTaskQueue
{
    private readonly IBackgroundJobClient _jobClient;

    public HangfireBackgroundTaskQueue(IBackgroundJobClient jobClient) => _jobClient = jobClient;

    public Task EnqueueAsync(Func<CancellationToken, Task> workItem, CancellationToken ct = default)
    {
        _jobClient.Enqueue(() => InvokeWorkItem(workItem, CancellationToken.None));
        return Task.CompletedTask;
    }

    public Task EnqueueJobAsync<TJob>(Expression<Func<TJob, Task>> methodCall)
    {
        _jobClient.Enqueue(methodCall);
        return Task.CompletedTask;
    }

    public static async Task InvokeWorkItem(Func<CancellationToken, Task> workItem, CancellationToken ct)
    {
        await workItem(ct);
    }
}
