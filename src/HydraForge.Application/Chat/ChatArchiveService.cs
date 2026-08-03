using HydraForge.Domain.Entities.Chat;

namespace HydraForge.Application.Chat;

public class ChatArchiveService(IChatSessionRepository sessionRepo)
{
    public async Task ArchiveFolderAsync(
        ChatFolder folder,
        CancellationToken ct = default
    )
    {
        folder.ArchivedAt = DateTime.UtcNow;

        var sessions = await sessionRepo.ListAsync(
            ownerId: folder.OwnerId,
            folderId: folder.Id,
            projectId: null,
            before: null,
            limit: int.MaxValue,
            ct
        );
        foreach (var session in sessions)
            session.ArchivedAt = DateTime.UtcNow;
    }

    public async Task ArchiveSessionAsync(
        ChatSession session,
        CancellationToken ct = default
    )
    {
        session.ArchivedAt = DateTime.UtcNow;
        await sessionRepo.UpdateAsync(session, ct);
    }
}
