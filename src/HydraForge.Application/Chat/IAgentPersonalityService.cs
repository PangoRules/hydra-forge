using HydraForge.Domain.Common;
using HydraForge.Domain.Entities.PersonalSpace;

namespace HydraForge.Application.Chat;

public record CreateAgentPersonalityRequest(
    string Name,
    string? Description,
    string SystemPrompt,
    bool IsDefault
);

public record UpdateAgentPersonalityRequest(
    string? Name,
    string? Description,
    string? SystemPrompt
);

public interface IAgentPersonalityService
{
    Task<Result<AgentPersonalityDto>> CreateAsync(
        CreateAgentPersonalityRequest request,
        Guid actorId,
        CancellationToken ct = default
    );

    Task<Result<IReadOnlyList<AgentPersonalityDto>>> ListAsync(
        Guid actorId,
        CancellationToken ct = default
    );

    Task<Result<AgentPersonalityDto>> UpdateAsync(
        Guid personalityId,
        UpdateAgentPersonalityRequest request,
        Guid actorId,
        CancellationToken ct = default
    );

    Task<Result<AgentPersonalityDto>> ArchiveAsync(
        Guid personalityId,
        Guid actorId,
        CancellationToken ct = default
    );

    Task<Result<AgentPersonalityDto>> SetDefaultAsync(
        Guid personalityId,
        Guid actorId,
        CancellationToken ct = default
    );
}
