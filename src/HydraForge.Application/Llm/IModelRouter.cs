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
        CancellationToken ct = default);
}
