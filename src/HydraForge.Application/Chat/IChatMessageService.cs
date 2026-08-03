using HydraForge.Application.Llm;
using HydraForge.Domain.Common;

namespace HydraForge.Application.Chat;

public interface IChatMessageService
{
    Task<Result<ChatMessageDto>> SendUserMessageAsync(
        Guid sessionId,
        Guid userId,
        string content,
        IReadOnlyList<ImageBlock>? images = null,
        CancellationToken ct = default
    );

    Task<Result<ChatMessagePageDto>> GetHistoryAsync(
        Guid sessionId,
        Guid actorId,
        DateTime? before = null,
        Guid? beforeId = null,
        int limit = 50,
        CancellationToken ct = default
    );
}
