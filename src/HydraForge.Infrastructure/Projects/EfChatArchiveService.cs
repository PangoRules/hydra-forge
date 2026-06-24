using HydraForge.Application.Projects;
using HydraForge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HydraForge.Infrastructure.Projects;

public class EfChatArchiveService(HydraForgeDbContext context) : IChatArchiveService
{
    public Task ArchiveProjectAsync(Guid projectId, CancellationToken ct = default)
        => SetProjectArchiveStateAsync(projectId, DateTime.UtcNow, ct);

    public Task UnarchiveProjectAsync(Guid projectId, CancellationToken ct = default)
        => SetProjectArchiveStateAsync(projectId, null, ct);

    private async Task SetProjectArchiveStateAsync(Guid projectId, DateTime? archivedAt, CancellationToken ct)
    {
        var chatFolders = await context.ChatFolders
            .Where(f => f.ProjectId == projectId)
            .ToListAsync(ct);

        foreach (var folder in chatFolders)
            folder.ArchivedAt = archivedAt;

        var folderIds = chatFolders.Select(f => f.Id).ToList();
        var chatSessions = await context.ChatSessions
            .Where(s => s.ProjectId == projectId || (s.FolderId.HasValue && folderIds.Contains(s.FolderId.Value)))
            .ToListAsync(ct);

        foreach (var session in chatSessions)
            session.ArchivedAt = archivedAt;

        await context.SaveChangesAsync(ct);
    }
}