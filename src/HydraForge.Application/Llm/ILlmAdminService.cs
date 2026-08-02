using HydraForge.Domain.Common;
using HydraForge.Domain.Enums;

namespace HydraForge.Application.Llm;

public interface ILlmAdminService
{
    // Providers
    Task<Result<ProviderPageDto>> ListProvidersAsync(
        int skip,
        int take,
        string? search,
        CancellationToken ct = default
    );
    Task<Result<ProviderDto>> CreateProviderAsync(
        CreateProviderInput input,
        CancellationToken ct = default
    );
    Task<Result<ProviderDto>> GetProviderAsync(Guid id, CancellationToken ct = default);
    Task<Result<ProviderDto>> UpdateProviderAsync(
        Guid id,
        UpdateProviderInput input,
        CancellationToken ct = default
    );
    Task<Result> DisableProviderAsync(Guid id, CancellationToken ct = default);

    // Models
    Task<Result<IReadOnlyList<ProviderModelDto>>> ProbeModelsAsync(
        Guid providerId,
        CancellationToken ct = default
    );
    Task<Result<ProviderModelConfigDto>> CreateModelAsync(
        Guid providerId,
        CreateModelInput input,
        CancellationToken ct = default
    );
    Task<Result<ProviderModelConfigDto>> GetModelAsync(
        Guid providerId,
        Guid modelId,
        CancellationToken ct = default
    );
    Task<Result<ProviderModelConfigDto>> UpdateModelAsync(
        Guid providerId,
        Guid modelId,
        UpdateModelInput input,
        CancellationToken ct = default
    );
    Task<Result> DeleteModelAsync(Guid providerId, Guid modelId, CancellationToken ct = default);

    // Routing
    Task<Result<IReadOnlyList<FeatureRoutingDto>>> ListRoutingAsync(CancellationToken ct = default);
    Task<Result<FeatureRoutingDto>> UpdateRoutingAsync(
        string feature,
        UpdateRoutingInput input,
        CancellationToken ct = default
    );

    // Usage
    Task<Result<TokenUsagePageDto>> QueryTokenUsageAsync(
        Guid? userId,
        Guid? projectId,
        string? feature,
        Guid? providerId,
        string? modelId,
        DateTime? from,
        DateTime? to,
        int skip,
        int take,
        CancellationToken ct = default
    );

    Task<Result<ImageUsagePageDto>> QueryImageUsageAsync(
        Guid? userId,
        Guid? projectId,
        string? feature,
        Guid? providerId,
        string? modelId,
        DateTime? from,
        DateTime? to,
        int skip,
        int take,
        CancellationToken ct = default
    );

    // Budget
    Task<Result<UserBudgetDto>> GetBudgetAsync(Guid userId, CancellationToken ct = default);
    Task<Result<UserBudgetDto>> UpdateBudgetAsync(
        Guid userId,
        UpdateBudgetInput input,
        CancellationToken ct = default
    );

    // Account usage
    Task<Result<AccountUsageResponse>> GetAccountUsageAsync(
        Guid userId,
        CancellationToken ct = default
    );
}
