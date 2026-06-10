using HydraForge.Domain.Entities.ProjectSpace;

namespace HydraForge.Application.Checklist;

public interface IChecklistItemRepository
{
    Task<ChecklistItem?> GetByIdAsync(Guid itemId, CancellationToken ct = default);
    Task<IReadOnlyList<ChecklistItem>> ListByCardAsync(Guid cardId, CancellationToken ct = default);
    Task<int> GetMaxPositionAsync(Guid cardId, CancellationToken ct = default);
    Task AddAsync(ChecklistItem item, CancellationToken ct = default);
    Task UpdateAsync(ChecklistItem item, CancellationToken ct = default);
    Task DeleteAsync(Guid itemId, CancellationToken ct = default);
    Task CompactPositionsAsync(Guid cardId, int deletedPosition, CancellationToken ct = default);
    Task UpdatePositionsAsync(IReadOnlyList<ChecklistItem> items, CancellationToken ct = default);
}