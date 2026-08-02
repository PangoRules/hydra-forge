namespace HydraForge.Infrastructure.Llm;

using HydraForge.Application.Llm;
using HydraForge.Domain.Entities.Admin;
using HydraForge.Domain.Enums;
using HydraForge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class EfLlmAdminRepository : ILlmAdminRepository
{
    private readonly HydraForgeDbContext _db;

    public EfLlmAdminRepository(HydraForgeDbContext db)
    {
        _db = db;
    }

    public async Task<List<LlmProvider>> ListProvidersAsync(int skip, int take, string? search, CancellationToken ct = default)
    {
        var query = _db.LlmProviders.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(p => p.Name.Contains(search));
        return await query.OrderBy(p => p.Name).Skip(skip).Take(take).ToListAsync(ct);
    }

    public async Task<int> CountProvidersAsync(string? search, CancellationToken ct = default)
    {
        var query = _db.LlmProviders.AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(p => p.Name.Contains(search));
        return await query.CountAsync(ct);
    }

    public Task<LlmProvider?> GetProviderByIdAsync(Guid id, CancellationToken ct = default)
        => _db.LlmProviders.FirstOrDefaultAsync(p => p.Id == id, ct);

    public void AddProvider(LlmProvider provider) => _db.LlmProviders.Add(provider);

    public Task<ProviderModelConfig?> GetModelConfigAsync(Guid providerId, Guid modelId, CancellationToken ct = default)
        => _db.ProviderModelConfigs.FirstOrDefaultAsync(c => c.Id == modelId && c.ProviderId == providerId, ct);

    public void AddModelConfig(ProviderModelConfig config) => _db.ProviderModelConfigs.Add(config);

    public void DeleteModelConfig(ProviderModelConfig config) => _db.ProviderModelConfigs.Remove(config);

    public Task<List<FeatureRoutingConfig>> ListRoutingAsync(CancellationToken ct = default)
        => _db.FeatureRoutingConfigs.AsNoTracking().OrderBy(c => c.Feature).ToListAsync(ct);

    public Task<FeatureRoutingConfig?> GetRoutingByFeatureAsync(AiFeature feature, CancellationToken ct = default)
        => _db.FeatureRoutingConfigs.FirstOrDefaultAsync(c => c.Feature == feature, ct);

    public async Task<(List<TokenUsageRecord> Items, int TotalCount, long TotalInput, long TotalOutput, decimal TotalCost)> QueryTokenUsageAsync(
        Guid? userId, Guid? projectId, AiFeature? feature, Guid? providerId, string? modelId,
        DateTime? from, DateTime? to, int skip, int take, CancellationToken ct = default)
    {
        var query = _db.TokenUsageRecords.AsNoTracking().AsQueryable();

        if (userId.HasValue)
            query = query.Where(r => r.UserId == userId.Value);

        if (projectId.HasValue)
            query = query.Where(r => r.ProjectId == projectId.Value);

        if (feature.HasValue)
            query = query.Where(r => r.Feature == feature.Value);

        if (providerId.HasValue)
            query = query.Where(r => r.ProviderId == providerId.Value);

        if (!string.IsNullOrWhiteSpace(modelId))
            query = query.Where(r => r.ModelId == modelId);

        if (from.HasValue)
            query = query.Where(r => r.CreatedAt >= from.Value);

        if (to.HasValue)
            query = query.Where(r => r.CreatedAt <= to.Value);

        var totalCount = await query.CountAsync(ct);
        var totalInput = await query.SumAsync(r => r.InputTokens, ct);
        var totalOutput = await query.SumAsync(r => r.OutputTokens, ct);
        var totalCost = await query.SumAsync(r => r.Cost, ct);

        var items = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);

        return (items, totalCount, totalInput, totalOutput, totalCost);
    }

    public async Task<(List<ImageUsageRecord> Items, int TotalCount, int TotalImages, decimal TotalCost)> QueryImageUsageAsync(
        Guid? userId, Guid? projectId, AiFeature? feature, Guid? providerId, string? modelId,
        DateTime? from, DateTime? to, int skip, int take, CancellationToken ct = default)
    {
        var query = _db.ImageUsageRecords.AsNoTracking().AsQueryable();

        if (userId.HasValue)
            query = query.Where(r => r.UserId == userId.Value);

        if (projectId.HasValue)
            query = query.Where(r => r.ProjectId == projectId.Value);

        if (feature.HasValue)
            query = query.Where(r => r.Feature == feature.Value);

        if (providerId.HasValue)
            query = query.Where(r => r.ProviderId == providerId.Value);

        if (!string.IsNullOrWhiteSpace(modelId))
            query = query.Where(r => r.ModelId == modelId);

        if (from.HasValue)
            query = query.Where(r => r.CreatedAt >= from.Value);

        if (to.HasValue)
            query = query.Where(r => r.CreatedAt <= to.Value);

        var totalCount = await query.CountAsync(ct);
        var totalImages = await query.SumAsync(r => r.ImageCount, ct);
        var totalCost = await query.SumAsync(r => r.Cost, ct);

        var items = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);

        return (items, totalCount, totalImages, totalCost);
    }

    public Task<UserTokenBudget?> GetBudgetByUserIdAsync(Guid userId, CancellationToken ct = default)
        => _db.UserTokenBudgets.FirstOrDefaultAsync(b => b.UserId == userId, ct);

    public void AddBudget(UserTokenBudget budget) => _db.UserTokenBudgets.Add(budget);

    public Task<int> SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
