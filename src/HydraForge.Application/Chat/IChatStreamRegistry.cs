namespace HydraForge.Application.Chat;

public interface IChatStreamRegistry
{
    bool TryRegister(Guid sessionId, CancellationTokenSource cts);
    bool TryCancel(Guid sessionId);
    void Remove(Guid sessionId);
}
