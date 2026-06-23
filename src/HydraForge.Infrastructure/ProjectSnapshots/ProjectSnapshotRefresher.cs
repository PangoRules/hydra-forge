using HydraForge.Application.Cards;
using HydraForge.Application.ProjectSnapshots;
using HydraForge.Application.Projects;
using HydraForge.Domain.Entities.ProjectSpace;
using HydraForge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HydraForge.Infrastructure.ProjectSnapshots;

public class ProjectSnapshotRefresher(HydraForgeDbContext context) : IProjectSnapshotRefresher
{
    public async Task RefreshAsync(Guid projectId, CancellationToken ct = default)
    {
        var columns = await context.Columns
            .Where(c => c.ProjectId == projectId)
            .OrderBy(c => c.Position)
            .ToListAsync(ct);

        var activeCards = await context.Cards
            .Where(c => c.ProjectId == projectId && c.ArchivedAt == null)
            .OrderBy(c => c.Position)
            .ThenBy(c => c.CardNumber)
            .ToListAsync(ct);

        var cardIds = activeCards.Select(c => c.Id).ToList();
        var activeRelationships = await context.CardRelationships
            .Where(r => r.ArchivedAt == null && (cardIds.Contains(r.SourceCardId) || cardIds.Contains(r.TargetCardId)))
            .ToListAsync(ct);

        var templateContent = ProjectContextSnapshotRenderer.Render(columns, activeCards, activeRelationships);

        var existing = await context.ProjectContextSnapshots
            .FirstOrDefaultAsync(s => s.ProjectId == projectId, ct);

        if (existing != null)
        {
            existing.TemplateContent = templateContent;
            existing.TemplateGeneratedAt = DateTime.UtcNow;
            context.ProjectContextSnapshots.Update(existing);
        }
        else
        {
            var snapshot = new Domain.Entities.ProjectSpace.ProjectContextSnapshot
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                TemplateContent = templateContent,
                TemplateGeneratedAt = DateTime.UtcNow,
            };
            context.ProjectContextSnapshots.Add(snapshot);
        }

        await context.SaveChangesAsync(ct);
    }

    public async Task<ProjectContextSnapshot?> GetSnapshotAsync(Guid projectId, CancellationToken ct = default)
    {
        return await context.ProjectContextSnapshots.FirstOrDefaultAsync(s => s.ProjectId == projectId, ct);
    }
}