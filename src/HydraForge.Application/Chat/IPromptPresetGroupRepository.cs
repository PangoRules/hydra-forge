using HydraForge.Domain.Entities.Chat;

namespace HydraForge.Application.Chat;

public interface IPromptPresetGroupRepository
{
    Task<PromptPresetGroup?> GetByIdAsync(Guid groupId, CancellationToken ct = default);
    Task<IReadOnlyList<PromptPresetGroup>> ListByUserAsync(Guid userId, CancellationToken ct = default);
    Task AddAsync(PromptPresetGroup group, CancellationToken ct = default);
    Task UpdateAsync(PromptPresetGroup group, CancellationToken ct = default);
    Task ArchiveAsync(Guid groupId, CancellationToken ct = default);
}
