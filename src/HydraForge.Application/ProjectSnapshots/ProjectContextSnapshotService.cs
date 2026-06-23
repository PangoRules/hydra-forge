using HydraForge.Application.Cards;
using HydraForge.Application.Projects;
using HydraForge.Domain.Entities.ProjectSpace;

namespace HydraForge.Application.ProjectSnapshots;

public class ProjectContextSnapshotService(
    IColumnRepository columnRepo,
    ICardRepository cardRepo,
    ICardRelationshipRepository relationshipRepo,
    IProjectContextSnapshotRepository snapshotRepo
) : IProjectSnapshotRefresher
{
    public async Task RefreshAsync(Guid projectId, CancellationToken ct = default)
    {
        var columns = await columnRepo.GetByProjectIdAsync(projectId, ct);
        var activeCards = await cardRepo.ListByProjectAsync(
            projectId,
            new CardListFilter(IncludeArchived: false),
            ct
        );
        var activeRelationships = await relationshipRepo.ListActiveByProjectAsync(projectId, ct);

        var templateContent = ProjectContextSnapshotRenderer.Render(
            columns,
            activeCards,
            activeRelationships
        );

        var existing = await snapshotRepo.GetByProjectIdAsync(projectId, ct);

        if (existing != null)
        {
            existing.TemplateContent = templateContent;
            existing.TemplateGeneratedAt = DateTime.UtcNow;
            await snapshotRepo.UpdateAsync(existing, ct);
        }
        else
        {
            var snapshot = new ProjectContextSnapshot
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                TemplateContent = templateContent,
                TemplateGeneratedAt = DateTime.UtcNow,
            };
            await snapshotRepo.AddAsync(snapshot, ct);
        }
    }

    Task<ProjectContextSnapshot?> IProjectSnapshotRefresher.GetSnapshotAsync(Guid projectId, CancellationToken ct)
    {
        return snapshotRepo.GetByProjectIdAsync(projectId, ct);
    }
}