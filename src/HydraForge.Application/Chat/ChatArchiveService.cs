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

        // Folders are strictly owner-scoped (see CLAUDE.md's scope-default lesson) —
        // Participated would admit "session I merely participate in as a project
        // member" into what must be an owner-only maintenance cascade.
        var sessions = await sessionRepo.ListAsync(
            actorId: folder.OwnerId,
            folderId: folder.Id,
            projectId: null,
            before: null,
            beforeId: null,
            limit: int.MaxValue,
            isAdmin: false,
            statusFilter: ChatSessionStatusFilter.NonArchived,
            scope: ChatSessionScope.Mine,
            types: null,
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
