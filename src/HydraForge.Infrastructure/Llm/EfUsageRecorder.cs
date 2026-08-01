namespace HydraForge.Infrastructure.Llm;

using System.Data;
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
        var cost = input.Cost;

        var config = await _db.ProviderModelConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == input.ProviderModelConfigId, ct);

        if (config?.PricePerToken is { } pricePerToken)
        {
            var chargeableTokens = input.InputTokens + input.OutputTokens - input.CachedTokens;
            cost = chargeableTokens * pricePerToken;
        }

        var record = new TokenUsageRecord
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

        _db.TokenUsageRecords.Add(record);
        await _db.SaveChangesAsync(ct);
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

    public async Task<int> AccrueTokenUsageAsync(Guid userId, int tokens, CancellationToken ct = default)
    {
        using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        try
        {
            var budget = await LoadOrCreateBudgetAsync(userId, ct);

            budget.MonthlyTokenUsed += tokens;
            await _db.SaveChangesAsync(ct);

            await tx.CommitAsync(ct);
            return budget.MonthlyTokenUsed;
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<int> AccrueImageUsageAsync(Guid userId, int count, CancellationToken ct = default)
    {
        using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        try
        {
            var budget = await LoadOrCreateBudgetAsync(userId, ct);

            budget.MonthlyImageUsed += count;
            await _db.SaveChangesAsync(ct);

            await tx.CommitAsync(ct);
            return budget.MonthlyImageUsed;
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    private async Task<UserTokenBudget> LoadOrCreateBudgetAsync(Guid userId, CancellationToken ct)
    {
        var budget = await _db.UserTokenBudgets.FirstOrDefaultAsync(b => b.UserId == userId, ct);

        if (budget is null)
        {
            budget = new UserTokenBudget
            {
                UserId = userId,
                MonthlyTokenBudget = 0,
                MonthlyTokenUsed = 0,
                MonthlyImageUsed = 0,
                PeriodStart = DateTime.UtcNow,
                PeriodEnd = DateTime.UtcNow.AddMonths(1),
            };
            _db.UserTokenBudgets.Add(budget);
        }
        else if (DateTime.UtcNow > budget.PeriodEnd)
        {
            // Period is [PeriodStart, PeriodEnd) — strict > ensures no double-rollover on same tick
            budget.MonthlyTokenUsed = 0;
            budget.MonthlyImageUsed = 0;
            budget.PeriodStart = DateTime.UtcNow;
            budget.PeriodEnd = DateTime.UtcNow.AddMonths(1);
        }

        return budget;
    }
}
