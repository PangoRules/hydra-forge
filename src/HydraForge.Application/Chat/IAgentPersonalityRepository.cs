using HydraForge.Domain.Entities.PersonalSpace;

namespace HydraForge.Application.Chat;

public interface IAgentPersonalityRepository
{
    Task<AgentPersonality?> GetByIdAsync(Guid personalityId, CancellationToken ct = default);
    Task<IReadOnlyList<AgentPersonality>> ListByUserAsync(
        Guid userId,
        CancellationToken ct = default
    );
    Task<AgentPersonality?> GetDefaultAsync(Guid userId, CancellationToken ct = default);
    Task AddAsync(AgentPersonality personality, CancellationToken ct = default);
    Task UpdateAsync(AgentPersonality personality, CancellationToken ct = default);
    Task ArchiveAsync(Guid personalityId, CancellationToken ct = default);
    Task SetDefaultAsync(Guid personalityId, Guid userId, CancellationToken ct = default);
}
