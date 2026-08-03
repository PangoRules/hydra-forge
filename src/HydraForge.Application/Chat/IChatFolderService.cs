using HydraForge.Domain.Common;

namespace HydraForge.Application.Chat;

public interface IChatFolderService
{
    Task<Result<ChatFolderDto>> CreateAsync(
        CreateChatFolderRequest request,
        Guid actorId,
        CancellationToken ct = default
    );
    Task<Result<IReadOnlyList<ChatFolderDto>>> ListAsync(
        Guid actorId,
        Guid? projectId,
        CancellationToken ct = default
    );
    Task<Result<ChatFolderDto>> UpdateAsync(
        Guid folderId,
        UpdateChatFolderRequest request,
        Guid actorId,
        CancellationToken ct = default
    );
    Task<Result<ChatFolderDto>> ArchiveAsync(
        Guid folderId,
        Guid actorId,
        CancellationToken ct = default
    );
}
