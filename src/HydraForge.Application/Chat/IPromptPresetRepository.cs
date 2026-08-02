using HydraForge.Domain.Entities.Chat;

namespace HydraForge.Application.Chat;

public interface IPromptPresetRepository
{
    Task<PromptPreset?> GetByIdAsync(Guid presetId, CancellationToken ct = default);
    Task<IReadOnlyList<PromptPreset>> ListByUserAsync(Guid userId, CancellationToken ct = default);
    Task AddAsync(PromptPreset preset, CancellationToken ct = default);
    Task UpdateAsync(PromptPreset preset, CancellationToken ct = default);
    Task ArchiveAsync(Guid presetId, CancellationToken ct = default);
    Task NullifyGroupAsync(Guid groupId, CancellationToken ct = default);
}
