namespace HydraForge.Infrastructure.Chat;

using HydraForge.Application.Chat;
using HydraForge.Domain.Entities.PersonalSpace;
using HydraForge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class EfAgentPersonalityRepository(HydraForgeDbContext context)
    : IAgentPersonalityRepository
{
    public async Task<AgentPersonality?> GetByIdAsync(
        Guid personalityId,
        CancellationToken ct = default
    )
    {
        return await context.AgentPersonalities.FirstOrDefaultAsync(p => p.Id == personalityId, ct);
    }

    public async Task<IReadOnlyList<AgentPersonality>> ListByUserAsync(
        Guid userId,
        CancellationToken ct = default
    )
    {
        return await context
            .AgentPersonalities.Where(p => p.UserId == userId && p.ArchivedAt == null)
            .OrderByDescending(p => p.UpdatedAt)
            .ToListAsync(ct);
    }

    public async Task<AgentPersonality?> GetDefaultAsync(
        Guid userId,
        CancellationToken ct = default
    )
    {
        return await context.AgentPersonalities.FirstOrDefaultAsync(
            p => p.UserId == userId && p.IsDefault && p.ArchivedAt == null,
            ct
        );
    }

    public async Task AddAsync(AgentPersonality personality, CancellationToken ct = default)
    {
        context.AgentPersonalities.Add(personality);
        await context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(AgentPersonality personality, CancellationToken ct = default)
    {
        context.AgentPersonalities.Update(personality);
        await context.SaveChangesAsync(ct);
    }

    public async Task ArchiveAsync(Guid personalityId, CancellationToken ct = default)
    {
        var personality = await context.AgentPersonalities.FirstOrDefaultAsync(
            p => p.Id == personalityId,
            ct
        );
        if (personality != null)
        {
            personality.ArchivedAt = DateTime.UtcNow;
            await context.SaveChangesAsync(ct);
        }
    }

    public async Task SetDefaultAsync(
        Guid personalityId,
        Guid userId,
        CancellationToken ct = default
    )
    {
        var personalities = await context
            .AgentPersonalities.Where(p => p.UserId == userId)
            .ToListAsync(ct);

        foreach (var p in personalities)
            p.IsDefault = p.Id == personalityId;

        await context.SaveChangesAsync(ct);
    }
}
