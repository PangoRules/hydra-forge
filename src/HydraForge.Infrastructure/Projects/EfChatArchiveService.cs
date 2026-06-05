using HydraForge.Application.Projects;
using HydraForge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HydraForge.Infrastructure.Projects;

public class EfChatArchiveService(HydraForgeDbContext context) : IChatArchiveService
{
    public async Task ArchiveProjectAsync(Guid projectId, CancellationToken ct = default)
    {
        var archivedAt = DateTime.UtcNow;

        var chatFolders = await context.ChatFolders
            .Where(f => f.ProjectId == projectId)
            .ToListAsync(ct);

        foreach (var folder in chatFolders)
        {
            folder.ArchivedAt = archivedAt;
        }

        var folderIds = chatFolders.Select(f => f.Id).ToList();
        var chatSessions = await context.ChatSessions
            .Where(s => s.ProjectId == projectId || (s.FolderId.HasValue && folderIds.Contains(s.FolderId.Value)))
            .ToListAsync(ct);

        foreach (var session in chatSessions)
        {
            session.ArchivedAt = archivedAt;
        }

        await context.SaveChangesAsync(ct);
    }

    public async Task UnarchiveProjectAsync(Guid projectId, CancellationToken ct = default)
    {
        var chatFolders = await context.ChatFolders
            .Where(f => f.ProjectId == projectId)
            .ToListAsync(ct);

        foreach (var folder in chatFolders)
        {
            folder.ArchivedAt = null;
        }

        var folderIds = chatFolders.Select(f => f.Id).ToList();
        var chatSessions = await context.ChatSessions
            .Where(s => s.ProjectId == projectId || (s.FolderId.HasValue && folderIds.Contains(s.FolderId.Value)))
            .ToListAsync(ct);

        foreach (var session in chatSessions)
        {
            session.ArchivedAt = null;
        }

        await context.SaveChangesAsync(ct);
    }
}