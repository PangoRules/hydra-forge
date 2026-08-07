using HydraForge.Domain.Entities.Chat;

namespace HydraForge.Application.Chat;

public class ChatArchiveService(
    IChatFolderRepository folderRepo,
    IChatSessionRepository sessionRepo
)
{
    public async Task ArchiveFolderAsync(Guid folderId, CancellationToken ct = default)
    {
        var folder = await folderRepo.GetByIdAsync(folderId, ct);
        if (folder == null)
            return;

        folder.Archive();
        await folderRepo.UpdateAsync(folder, ct);

        var sessions = await sessionRepo.ListAsync(
            ownerId: folder.OwnerId,
            folderId: folder.Id,
            projectId: null,
            before: null,
            beforeId: null,
            limit: int.MaxValue,
            statusFilter: ChatSessionStatusFilter.NonArchived,
            ct
        );
        foreach (var session in sessions)
        {
            session.Archive();
            await sessionRepo.UpdateAsync(session, ct);
        }
    }

    public async Task ArchiveSessionAsync(Guid sessionId, CancellationToken ct = default)
    {
        var session = await sessionRepo.GetByIdAsync(sessionId, ct);
        if (session == null)
            return;

        session.Archive();
        await sessionRepo.UpdateAsync(session, ct);
    }
}
