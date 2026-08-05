using System.Collections.Concurrent;
using HydraForge.Application.Chat;

namespace HydraForge.Infrastructure.Realtime;

public sealed class ChatStreamRegistry : IChatStreamRegistry
{
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _activeStreams = new();

    public bool TryRegister(Guid sessionId, CancellationTokenSource cts) =>
        _activeStreams.TryAdd(sessionId, cts);

    public bool TryCancel(Guid sessionId)
    {
        if (!_activeStreams.TryGetValue(sessionId, out var cts))
            return false;

        cts.Cancel();
        return true;
    }

    public void Remove(Guid sessionId) => _activeStreams.TryRemove(sessionId, out _);
}
