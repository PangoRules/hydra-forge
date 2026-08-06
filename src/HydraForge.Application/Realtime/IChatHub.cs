namespace HydraForge.Application.Realtime;

public interface IChatHub
{
    Task StreamStart(Guid messageId, string modelId, string modelName);
    Task StreamDelta(Guid messageId, string delta);
    Task StreamDone(
        Guid messageId,
        int? inputTokens,
        int? outputTokens,
        int? cachedTokens,
        string? modelName
    );
    Task StreamError(Guid messageId, string code, string message);
    Task Typing(Guid sessionId, Guid userId);

    /// <summary>
    /// Pushed when a session's title (or other server-set fields) changes outside the
    /// caller's own request/response cycle — e.g. <see cref="Chat.ChatTitleGenerationJob"/>,
    /// which runs as its own decoupled Hangfire job specifically so it doesn't block the
    /// chat reply's own completion (see that job's doc comment). Without this push, a
    /// connected client has no way to learn the title changed short of polling or a manual
    /// refresh.
    /// </summary>
    Task SessionUpdated(Guid sessionId, string title, string status);
}
