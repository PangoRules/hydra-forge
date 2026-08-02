namespace HydraForge.Application.Llm;

using HydraForge.Application.Admin;
using HydraForge.Domain.Common;
using HydraForge.Domain.Enums;

public sealed class LlmCallGuard
{
    private readonly IUserTokenBudgetRepository _budgetRepository;
    private readonly IUsageRecorder _recorder;

    public LlmCallGuard(IUserTokenBudgetRepository budgetRepository, IUsageRecorder recorder)
    {
        _budgetRepository = budgetRepository;
        _recorder = recorder;
    }

    public async Task<Result> CheckTokenBudgetAsync(
        Guid userId,
        int estimatedTokens,
        CancellationToken ct = default
    )
    {
        var budget = await _budgetRepository.GetByUserIdAsync(userId, ct);

        if (budget is null)
        {
            return Result.Success();
        }

        if (DateTime.UtcNow > budget.PeriodEnd)
        {
            return Result.Success();
        }

        if (
            budget.MonthlyTokenBudget > 0
            && budget.MonthlyTokenUsed + estimatedTokens > budget.MonthlyTokenBudget
        )
        {
            return Result.Failure(
                new Error(
                    DomainErrorCodes.Llm.TokenBudgetExceeded,
                    "Monthly token budget exceeded."
                )
            );
        }

        return Result.Success();
    }

    public async Task<Result> CheckImageBudgetAsync(
        Guid userId,
        int count,
        CancellationToken ct = default
    )
    {
        var budget = await _budgetRepository.GetByUserIdAsync(userId, ct);

        if (budget is null)
        {
            return Result.Success();
        }

        if (DateTime.UtcNow > budget.PeriodEnd)
        {
            return Result.Success();
        }

        if (
            budget.MonthlyImageBudget > 0
            && budget.MonthlyImageUsed + count > budget.MonthlyImageBudget
        )
        {
            return Result.Failure(
                new Error(
                    DomainErrorCodes.Llm.ImageBudgetExceeded,
                    "Monthly image budget exceeded."
                )
            );
        }

        return Result.Success();
    }

    public async Task<int> AccrueAfterCallAsync(
        Guid userId,
        UsageSnapshot? usage,
        CancellationToken ct = default
    )
    {
        var totalTokens = usage?.InputTokens + usage?.OutputTokens ?? 0;

        await _recorder.AccrueTokenUsageAsync(userId, totalTokens, ct);

        var recordInput = new TokenUsageRecordInput(
            UserId: userId,
            ProjectId: null,
            Feature: AiFeature.PersonalChat,
            ProviderModelConfigId: Guid.Empty,
            ProviderId: Guid.Empty,
            ModelId: string.Empty,
            ModelName: string.Empty,
            InputTokens: usage?.InputTokens ?? 0,
            OutputTokens: usage?.OutputTokens ?? 0,
            CachedTokens: usage?.CachedTokens ?? 0,
            PipelineRunId: null,
            Cost: 0
        );
        await _recorder.RecordTokenAsync(recordInput, ct);

        return totalTokens;
    }
}
