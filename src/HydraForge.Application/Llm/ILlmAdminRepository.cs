namespace HydraForge.Application.Llm;

using HydraForge.Domain.Entities.Admin;
using HydraForge.Domain.Enums;

public interface ILlmAdminRepository
{
    Task<List<LlmProvider>> ListProvidersAsync(
        int skip,
        int take,
        string? search,
        CancellationToken ct = default
    );
    Task<int> CountProvidersAsync(string? search, CancellationToken ct = default);
    Task<LlmProvider?> GetProviderByIdAsync(Guid id, CancellationToken ct = default);
    void AddProvider(LlmProvider provider);

    Task<List<ProviderModelConfig>> ListModelConfigsAsync(
        Guid providerId,
        CancellationToken ct = default
    );
    Task<ProviderModelConfig?> GetModelConfigAsync(
        Guid providerId,
        Guid modelId,
        CancellationToken ct = default
    );
    void AddModelConfig(ProviderModelConfig config);
    void DeleteModelConfig(ProviderModelConfig config);

    Task<List<FeatureRoutingConfig>> ListRoutingAsync(CancellationToken ct = default);
    Task<FeatureRoutingConfig?> GetRoutingByFeatureAsync(
        AiFeature feature,
        CancellationToken ct = default
    );

    Task<(
        List<TokenUsageRecord> Items,
        int TotalCount,
        long TotalInput,
        long TotalOutput,
        decimal TotalCost
    )> QueryTokenUsageAsync(
        Guid? userId,
        Guid? projectId,
        IReadOnlyList<AiFeature>? features,
        Guid? providerId,
        string? modelId,
        DateTime? from,
        DateTime? to,
        int skip,
        int take,
        CancellationToken ct = default
    );

    Task<(
        List<ImageUsageRecord> Items,
        int TotalCount,
        int TotalImages,
        decimal TotalCost
    )> QueryImageUsageAsync(
        Guid? userId,
        Guid? projectId,
        IReadOnlyList<AiFeature>? features,
        Guid? providerId,
        string? modelId,
        DateTime? from,
        DateTime? to,
        int skip,
        int take,
        CancellationToken ct = default
    );

    Task<UserTokenBudget?> GetBudgetByUserIdAsync(Guid userId, CancellationToken ct = default);
    void AddBudget(UserTokenBudget budget);

    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
