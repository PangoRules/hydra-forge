using HydraForge.Domain.Entities.Admin;
using HydraForge.Domain.Enums;

namespace HydraForge.Application.Llm;

public interface IRoutingConfigProvider
{
    Task<FeatureRoutingConfig?> GetRoutingConfigAsync(AiFeature feature, CancellationToken ct);
    Task<
        IReadOnlyList<(ProviderModelConfig Model, LlmProvider Provider)>
    > GetEnabledModelsAtTierAsync(ModelTier tier, CancellationToken ct);
    Task<LlmProvider?> GetProviderAsync(Guid providerId, CancellationToken ct);
    Task<
        IReadOnlyList<(ProviderModelConfig Model, LlmProvider Provider)>
    > GetEnabledModelsAtTierForProviderAsync(ModelTier tier, Guid providerId, CancellationToken ct);
    Task<IReadOnlyList<(ProviderModelConfig Model, LlmProvider Provider)>> GetAllowedModelsAsync(
        Guid featureRoutingConfigId,
        CancellationToken ct
    );
}
