using HydraForge.Domain.Common;
using HydraForge.Domain.Enums;

namespace HydraForge.Application.Llm;

public interface IModelRouter
{
    Task<Result<RouteDecision>> ResolveAsync(
        AiFeature feature,
        Guid userId,
        Guid? projectId,
        int estimatedTokens,
        CancellationToken ct = default,
        Guid? preferredProviderModelConfigId = null
    );

    /// <summary>
    /// Models the calling user may choose between for <paramref name="feature"/> —
    /// the admin's <c>FeatureAllowedModel</c> allowlist if one is configured,
    /// otherwise every enabled model at the feature's effective tier.
    /// </summary>
    Task<Result<IReadOnlyList<AvailableModelDto>>> ListAvailableModelsAsync(
        AiFeature feature,
        CancellationToken ct = default
    );
}
