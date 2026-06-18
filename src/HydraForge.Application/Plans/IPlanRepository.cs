using HydraForge.Domain.Entities.ProjectSpace;

namespace HydraForge.Application.Plans;

public interface IPlanRepository
{
    Task<Plan?> GetByIdAsync(Guid planId, CancellationToken ct = default);
    Task<IReadOnlyList<Plan>> ListByProjectAsync(Guid projectId, PlanListFilter filter, CancellationToken ct = default);
    Task<IReadOnlyList<Plan>> ListByCardAsync(Guid cardId, PlanListFilter filter, CancellationToken ct = default);
    Task<PlanVersion?> GetVersionAsync(Guid planId, int version, CancellationToken ct = default);
    Task<IReadOnlyList<PlanVersion>> ListVersionsAsync(Guid planId, CancellationToken ct = default);
    Task AddAsync(Plan plan, CancellationToken ct = default);
    Task AddVersionAsync(PlanVersion version, CancellationToken ct = default);
    Task UpdateAsync(Plan plan, CancellationToken ct = default);
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}