namespace HydraForge.Application.Realtime;

public interface IChatHub
{
    Task StreamStart(Guid messageId, string modelId, string modelName);
    Task StreamDelta(Guid messageId, string delta);
    Task StreamDone(Guid messageId, int? inputTokens, int? outputTokens, int? cachedTokens, string? modelName);
    Task StreamError(Guid messageId, string code, string message);
    Task Typing(Guid sessionId, Guid userId);
}
