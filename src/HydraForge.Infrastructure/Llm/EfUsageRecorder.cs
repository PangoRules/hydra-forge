namespace HydraForge.Infrastructure.Llm;

using HydraForge.Application.Llm;
using HydraForge.Domain.Entities.Admin;
using HydraForge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class EfUsageRecorder : IUsageRecorder
{
    private readonly HydraForgeDbContext _db;

    public EfUsageRecorder(HydraForgeDbContext db)
    {
        _db = db;
    }

    public async Task RecordTokenAsync(TokenUsageRecordInput input, CancellationToken ct = default)
    {
        var config = await _db
            .ProviderModelConfigs.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == input.ProviderModelConfigId, ct);

        _db.TokenUsageRecords.Add(ToRecord(input, config?.PricePerToken));
        await _db.SaveChangesAsync(ct);
    }

    public async Task RecordTokenBatchAsync(
        IReadOnlyList<TokenUsageRecordInput> inputs,
        CancellationToken ct = default
    )
    {
        if (inputs.Count == 0)
            return;

        var configIds = inputs.Select(i => i.ProviderModelConfigId).Distinct().ToList();
        var pricesByConfigId = await _db
            .ProviderModelConfigs.AsNoTracking()
            .Where(c => configIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.PricePerToken, ct);

        foreach (var input in inputs)
        {
            pricesByConfigId.TryGetValue(input.ProviderModelConfigId, out var pricePerToken);
            _db.TokenUsageRecords.Add(ToRecord(input, pricePerToken));
        }

        await _db.SaveChangesAsync(ct);
    }

    private static TokenUsageRecord ToRecord(TokenUsageRecordInput input, decimal? pricePerToken)
    {
        var cost = input.Cost;

        if (pricePerToken is { } price)
        {
            var chargeableTokens = input.InputTokens + input.OutputTokens - input.CachedTokens;
            cost = chargeableTokens * price;
        }

        return new TokenUsageRecord
        {
            UserId = input.UserId,
            ProjectId = input.ProjectId,
            Feature = input.Feature,
            ProviderModelConfigId = input.ProviderModelConfigId,
            ProviderId = input.ProviderId,
            ModelId = input.ModelId,
            ModelName = input.ModelName,
            InputTokens = input.InputTokens,
            OutputTokens = input.OutputTokens,
            CachedTokens = input.CachedTokens,
            PipelineRunId = input.PipelineRunId,
            Cost = cost,
        };
    }

    public async Task RecordImageAsync(ImageUsageRecordInput input, CancellationToken ct = default)
    {
        var record = new ImageUsageRecord
        {
            UserId = input.UserId,
            ProjectId = input.ProjectId,
            Feature = input.Feature,
            ProviderModelConfigId = input.ProviderModelConfigId,
            ProviderId = input.ProviderId,
            ModelId = input.ModelId,
            ModelName = input.ModelName,
            ImageCount = input.ImageCount,
            Resolution = input.Resolution,
            Cost = input.Cost,
        };

        _db.ImageUsageRecords.Add(record);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<int> AccrueTokenUsageAsync(
        Guid userId,
        int tokens,
        CancellationToken ct = default
    )
    {
        var budget = await LoadOrCreateBudgetAsync(userId, ct);

        budget.MonthlyTokenUsed += tokens;
        await _db.SaveChangesAsync(ct);

        return budget.MonthlyTokenUsed;
    }

    public async Task<int> AccrueImageUsageAsync(
        Guid userId,
        int count,
        CancellationToken ct = default
    )
    {
        var budget = await LoadOrCreateBudgetAsync(userId, ct);

        budget.MonthlyImageUsed += count;
        await _db.SaveChangesAsync(ct);

        return budget.MonthlyImageUsed;
    }

    private async Task<UserTokenBudget> LoadOrCreateBudgetAsync(Guid userId, CancellationToken ct)
    {
        var budget = await _db.UserTokenBudgets.FirstOrDefaultAsync(b => b.UserId == userId, ct);

        if (budget is null)
        {
            var periodStart = DateTime.UtcNow;
            budget = new UserTokenBudget
            {
                UserId = userId,
                MonthlyTokenBudget = 0,
                MonthlyTokenUsed = 0,
                MonthlyImageUsed = 0,
                PeriodStart = periodStart,
                PeriodEnd = periodStart.AddMonths(1),
            };
            _db.UserTokenBudgets.Add(budget);
        }
        else if (DateTime.UtcNow > budget.PeriodEnd)
        {
            var periodStart = DateTime.UtcNow;
            budget.MonthlyTokenUsed = 0;
            budget.MonthlyImageUsed = 0;
            budget.PeriodStart = periodStart;
            budget.PeriodEnd = periodStart.AddMonths(1);
        }

        return budget;
    }
}
