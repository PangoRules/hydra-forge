namespace HydraForge.Infrastructure.Llm;

using HydraForge.Application.Llm;
using HydraForge.Domain.Entities.Admin;
using HydraForge.Domain.Enums;
using HydraForge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class DbContextRoutingConfigProvider : IRoutingConfigProvider
{
    private readonly HydraForgeDbContext _context;

    public DbContextRoutingConfigProvider(HydraForgeDbContext context)
    {
        _context = context;
    }

    public async Task<FeatureRoutingConfig?> GetRoutingConfigAsync(
        AiFeature feature,
        CancellationToken ct
    )
    {
        return await _context
            .FeatureRoutingConfigs.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Feature == feature, ct);
    }

    public async Task<
        IReadOnlyList<(ProviderModelConfig Model, LlmProvider Provider)>
    > GetEnabledModelsAtTierAsync(ModelTier tier, CancellationToken ct)
    {
        var results = await _context
            .ProviderModelConfigs.AsNoTracking()
            .Where(m => m.IsEnabled && m.Tier == tier)
            .Join(
                _context.LlmProviders.AsNoTracking().Where(p => p.IsEnabled),
                m => m.ProviderId,
                p => p.Id,
                (m, p) => new { Model = m, Provider = p }
            )
            .OrderBy(x => x.Provider.Name)
            .ToListAsync(ct);

        return results.Select(x => (x.Model, x.Provider)).ToList();
    }

    public async Task<LlmProvider?> GetProviderAsync(Guid providerId, CancellationToken ct)
    {
        return await _context
            .LlmProviders.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == providerId, ct);
    }

    public async Task<
        IReadOnlyList<(ProviderModelConfig Model, LlmProvider Provider)>
    > GetEnabledModelsAtTierForProviderAsync(ModelTier tier, Guid providerId, CancellationToken ct)
    {
        var results = await _context
            .ProviderModelConfigs.AsNoTracking()
            .Where(m => m.IsEnabled && m.Tier == tier && m.ProviderId == providerId)
            .Join(
                _context.LlmProviders.AsNoTracking().Where(p => p.IsEnabled && p.Id == providerId),
                m => m.ProviderId,
                p => p.Id,
                (m, p) => new { Model = m, Provider = p }
            )
            .ToListAsync(ct);

        return results.Select(x => (x.Model, x.Provider)).ToList();
    }
}
