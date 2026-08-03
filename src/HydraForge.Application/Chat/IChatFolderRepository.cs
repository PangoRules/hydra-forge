using HydraForge.Domain.Entities.Chat;

namespace HydraForge.Application.Chat;

public interface IChatFolderRepository
{
    Task<ChatFolder?> GetByIdAsync(Guid folderId, CancellationToken ct = default);
    Task<IReadOnlyList<ChatFolder>> ListByOwnerAsync(
        Guid ownerId,
        Guid? projectId,
        CancellationToken ct = default
    );
    Task AddAsync(ChatFolder folder, CancellationToken ct = default);
    Task UpdateAsync(ChatFolder folder, CancellationToken ct = default);
}
