using HydraForge.Domain.Common;
using HydraForge.Domain.Entities.PersonalSpace;

namespace HydraForge.Application.Chat;

public class AgentPersonalityService(IAgentPersonalityRepository repo) : IAgentPersonalityService
{
    private readonly IAgentPersonalityRepository _repo = repo;

    public async Task<Result<AgentPersonalityDto>> CreateAsync(
        CreateAgentPersonalityRequest request,
        Guid actorId,
        CancellationToken ct = default
    )
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return Result<AgentPersonalityDto>.Failure(
                new Error(DomainErrorCodes.Validation.Required, "Personality name is required.")
            );

        var personality = new AgentPersonality
        {
            Id = Guid.NewGuid(),
            UserId = actorId,
            Name = request.Name,
            Description = request.Description,
            SystemPrompt = request.SystemPrompt,
            IsDefault = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        await _repo.AddAsync(personality, ct);

        if (request.IsDefault)
            await _repo.SetDefaultAsync(personality.Id, actorId, ct);

        return Result<AgentPersonalityDto>.Success(MapToDto(personality));
    }

    public async Task<Result<IReadOnlyList<AgentPersonalityDto>>> ListAsync(
        Guid actorId,
        CancellationToken ct = default
    )
    {
        var all = await _repo.ListByUserAsync(actorId, ct);

        if (all.Count == 0)
        {
            foreach (var seed in DefaultAgentPersonalities.All)
            {
                await _repo.AddAsync(
                    new AgentPersonality
                    {
                        Id = Guid.NewGuid(),
                        UserId = actorId,
                        Name = seed.Name,
                        Description = seed.Description,
                        SystemPrompt = seed.SystemPrompt,
                        IsDefault = false,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                    },
                    ct
                );
            }
            all = await _repo.ListByUserAsync(actorId, ct);
        }

        var active = all.Where(p => !p.ArchivedAt.HasValue).Select(MapToDto).ToList();
        return Result<IReadOnlyList<AgentPersonalityDto>>.Success(active);
    }

    public async Task<Result<AgentPersonalityDto>> UpdateAsync(
        Guid personalityId,
        UpdateAgentPersonalityRequest request,
        Guid actorId,
        CancellationToken ct = default
    )
    {
        var personality = await _repo.GetByIdAsync(personalityId, ct);
        if (personality == null)
            return Result<AgentPersonalityDto>.Failure(
                new Error(DomainErrorCodes.Chat.PersonalityNotFound, "Personality not found.")
            );

        if (personality.UserId != actorId)
            return Result<AgentPersonalityDto>.Failure(
                new Error(
                    DomainErrorCodes.Chat.PersonalityNotOwner,
                    "Only the owner can update the personality."
                )
            );

        if (personality.ArchivedAt != null)
            return Result<AgentPersonalityDto>.Failure(
                new Error(
                    DomainErrorCodes.Chat.PersonalityNotFound,
                    "Cannot update an archived personality."
                )
            );

        if (request.Name != null && string.IsNullOrWhiteSpace(request.Name))
            return Result<AgentPersonalityDto>.Failure(
                new Error(
                    DomainErrorCodes.Validation.Required,
                    "Personality name cannot be whitespace-only."
                )
            );

        personality.Update(request.Name, request.Description, request.SystemPrompt);

        await _repo.UpdateAsync(personality, ct);
        return Result<AgentPersonalityDto>.Success(MapToDto(personality));
    }

    public async Task<Result<AgentPersonalityDto>> ArchiveAsync(
        Guid personalityId,
        Guid actorId,
        CancellationToken ct = default
    )
    {
        var personality = await _repo.GetByIdAsync(personalityId, ct);
        if (personality == null)
            return Result<AgentPersonalityDto>.Failure(
                new Error(DomainErrorCodes.Chat.PersonalityNotFound, "Personality not found.")
            );

        if (personality.UserId != actorId)
            return Result<AgentPersonalityDto>.Failure(
                new Error(
                    DomainErrorCodes.Chat.PersonalityNotOwner,
                    "Only the owner can archive the personality."
                )
            );

        personality.Archive();
        await _repo.UpdateAsync(personality, ct);
        personality = await _repo.GetByIdAsync(personalityId, ct);
        if (personality == null)
            return Result<AgentPersonalityDto>.Failure(
                new Error(DomainErrorCodes.Chat.PersonalityNotFound, "Personality not found.")
            );

        return Result<AgentPersonalityDto>.Success(MapToDto(personality));
    }

    public async Task<Result<AgentPersonalityDto>> SetDefaultAsync(
        Guid personalityId,
        Guid actorId,
        CancellationToken ct = default
    )
    {
        var personality = await _repo.GetByIdAsync(personalityId, ct);
        if (personality == null)
            return Result<AgentPersonalityDto>.Failure(
                new Error(DomainErrorCodes.Chat.PersonalityNotFound, "Personality not found.")
            );

        if (personality.UserId != actorId)
            return Result<AgentPersonalityDto>.Failure(
                new Error(
                    DomainErrorCodes.Chat.PersonalityNotOwner,
                    "Only the owner can set the default personality."
                )
            );

        if (personality.ArchivedAt != null)
            return Result<AgentPersonalityDto>.Failure(
                new Error(
                    DomainErrorCodes.Chat.PersonalityNotFound,
                    "Cannot set an archived personality as default."
                )
            );

        await _repo.SetDefaultAsync(personalityId, actorId, ct);
        personality = await _repo.GetByIdAsync(personalityId, ct);
        if (personality == null)
            return Result<AgentPersonalityDto>.Failure(
                new Error(DomainErrorCodes.Chat.PersonalityNotFound, "Personality not found.")
            );

        return Result<AgentPersonalityDto>.Success(MapToDto(personality));
    }

    private static AgentPersonalityDto MapToDto(AgentPersonality p) =>
        new(
            p.Id,
            p.Name,
            p.Description,
            p.SystemPrompt,
            p.IsDefault,
            p.CreatedAt,
            p.UpdatedAt,
            p.ArchivedAt
        );
}
