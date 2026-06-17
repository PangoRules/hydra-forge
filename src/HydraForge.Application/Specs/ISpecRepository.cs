using HydraForge.Domain.Entities.ProjectSpace;

namespace HydraForge.Application.Specs;

public interface ISpecRepository
{
    Task<Spec?> GetByIdAsync(Guid specId, CancellationToken ct = default);
    Task<IReadOnlyList<Spec>> ListByProjectAsync(Guid projectId, SpecListFilter filter, CancellationToken ct = default);
    Task<SpecVersion?> GetVersionAsync(Guid specId, int version, CancellationToken ct = default);
    Task<IReadOnlyList<SpecVersion>> ListVersionsAsync(Guid specId, CancellationToken ct = default);
    Task<IReadOnlyDictionary<Guid, Guid?>> GetLinkedCardIdsAsync(Guid projectId, CancellationToken ct = default);
    Task<Guid?> GetLinkedCardIdAsync(Guid specId, CancellationToken ct = default);
    Task AddAsync(Spec spec, CancellationToken ct = default);
    Task AddVersionAsync(SpecVersion version, CancellationToken ct = default);
    Task UpdateAsync(Spec spec, CancellationToken ct = default);
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
