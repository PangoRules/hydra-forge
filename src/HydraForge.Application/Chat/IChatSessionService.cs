using HydraForge.Domain.Common;

namespace HydraForge.Application.Chat;

public interface IChatSessionService
{
    Task<Result<ChatSessionDto>> CreateAsync(
        CreateChatSessionRequest request,
        Guid actorId,
        CancellationToken ct = default
    );
    Task<Result<ChatSessionDetailDto>> GetAsync(
        Guid sessionId,
        Guid actorId,
        CancellationToken ct = default
    );
    Task<Result<ChatSessionPageDto>> ListAsync(
        Guid actorId,
        Guid? folderId,
        Guid? projectId,
        DateTime? before,
        Guid? beforeId,
        int limit,
        ChatSessionStatusFilter statusFilter = ChatSessionStatusFilter.NonArchived,
        CancellationToken ct = default
    );
    Task<Result<ChatSessionDto>> UpdateAsync(
        Guid sessionId,
        UpdateChatSessionRequest request,
        Guid actorId,
        CancellationToken ct = default
    );
    Task<Result<ChatSessionDto>> CloseAsync(
        Guid sessionId,
        Guid actorId,
        CancellationToken ct = default
    );
    Task<Result<ChatSessionDto>> ReopenAsync(
        Guid sessionId,
        Guid actorId,
        CancellationToken ct = default
    );
    Task<Result<ChatSessionDto>> ArchiveAsync(
        Guid sessionId,
        Guid actorId,
        CancellationToken ct = default
    );
    Task<Result<ChatPermissionDto>> GetPermissionAsync(
        Guid sessionId,
        Guid actorId,
        CancellationToken ct = default
    );
    Task<Result<ChatSessionDocumentDto>> AttachDocumentAsync(
        Guid sessionId,
        Guid documentId,
        Guid actorId,
        CancellationToken ct = default
    );
    Task<Result<ChatSessionDto>> DetachDocumentAsync(
        Guid sessionId,
        Guid documentId,
        Guid actorId,
        CancellationToken ct = default
    );
    Task<Result<IReadOnlyList<ChatSessionDocumentDto>>> ListDocumentsAsync(
        Guid sessionId,
        Guid actorId,
        CancellationToken ct = default
    );
}
