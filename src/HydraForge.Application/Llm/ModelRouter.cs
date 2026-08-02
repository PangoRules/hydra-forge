namespace HydraForge.Application.Llm;

using HydraForge.Application.Logging;
using HydraForge.Domain.Common;
using HydraForge.Domain.Entities.Admin;
using HydraForge.Domain.Enums;

public sealed class ModelRouter : IModelRouter
{
    private readonly IRoutingConfigProvider _provider;
    private readonly IWarnLogger _warnLogger;

    public ModelRouter(IRoutingConfigProvider provider, IWarnLogger warnLogger)
    {
        _provider = provider;
        _warnLogger = warnLogger;
    }

    public async Task<Result<RouteDecision>> ResolveAsync(
        AiFeature feature,
        Guid userId,
        Guid? projectId,
        int estimatedTokens,
        CancellationToken ct = default
    )
    {
        // TODO: Apply user-specific tier ceiling when user budget/role tiers are implemented.
        var routingConfig = await _provider.GetRoutingConfigAsync(feature, ct);

        if (routingConfig is null)
        {
            return Result<RouteDecision>.Failure(
                new Error(
                    DomainErrorCodes.Llm.NoModelForFeature,
                    $"No routing config found for feature {feature}."
                )
            );
        }

        var tier = routingConfig.DefaultTier;

        if (routingConfig.MaxUserTier.HasValue && tier > routingConfig.MaxUserTier.Value)
        {
            tier = routingConfig.MaxUserTier.Value;
        }

        var candidate = await FindModelAtTierAsync(tier, ct);
        var hadModelAtInitialTier = candidate is not null;

        if (candidate is null || estimatedTokens > candidate.Model.MaxTokens)
        {
            var (bumped, hadAnyModel) = await TryAutoBumpTierAsync(tier, estimatedTokens, ct);
            candidate = bumped;

            if (candidate is null)
            {
                var errorCode =
                    (hadAnyModel || hadModelAtInitialTier)
                        ? DomainErrorCodes.Llm.ContextWindowExceeded
                        : DomainErrorCodes.Llm.NoModelForFeature;
                return Result<RouteDecision>.Failure(
                    new Error(
                        errorCode,
                        $"No model found for feature {feature} with {estimatedTokens} tokens."
                    )
                );
            }
        }

        var primaryModel = candidate.Model;
        var primaryProvider = candidate.Provider;

        var fallbackCandidates = await BuildFallbackChainAsync(
            primaryProvider.Id,
            primaryModel.Tier,
            ct
        );

        var primaryModelDto = ToModelDto(primaryModel);
        var primaryProviderDto = ToProviderDto(primaryProvider);
        var fallbackDtos = fallbackCandidates
            .Select(f => new FallbackProvider(ToModelDto(f.Model), ToProviderDto(f.Provider)))
            .ToList();

        return Result<RouteDecision>.Success(
            new RouteDecision(primaryModelDto, primaryProviderDto, fallbackDtos, primaryProvider)
        );
    }

    private async Task<Candidate?> FindModelAtTierAsync(ModelTier tier, CancellationToken ct)
    {
        var candidates = await _provider.GetEnabledModelsAtTierAsync(tier, ct);
        return candidates
            .OrderBy(x => x.Provider.Name)
            .Select(x => new Candidate(x.Model, x.Provider))
            .FirstOrDefault();
    }

    private async Task<(Candidate? Candidate, bool HadAnyModel)> TryAutoBumpTierAsync(
        ModelTier initialTier,
        int estimatedTokens,
        CancellationToken ct
    )
    {
        var tiersToTry = new List<ModelTier>();

        if (initialTier == ModelTier.Economy)
            tiersToTry.Add(ModelTier.Standard);
        if (initialTier != ModelTier.Premium)
            tiersToTry.Add(ModelTier.Premium);

        var hadAnyModel = false;

        foreach (var targetTier in tiersToTry)
        {
            var candidate = await FindModelAtTierAsync(targetTier, ct);
            if (candidate is not null)
            {
                hadAnyModel = true;
                // Null MaxTokens means no configured limit — treat as unlimited,
                // matching the initial-tier acceptance check above (`estimatedTokens > candidate.Model.MaxTokens`
                // is also false when MaxTokens is null, so that path already accepts it).
                if (
                    candidate.Model.MaxTokens is null
                    || candidate.Model.MaxTokens >= estimatedTokens
                )
                {
                    return (candidate, true);
                }
            }
        }

        return (null, hadAnyModel);
    }

    private async Task<IReadOnlyList<Candidate>> BuildFallbackChainAsync(
        Guid providerId,
        ModelTier primaryModelTier,
        CancellationToken ct
    )
    {
        var candidates = new List<Candidate>();
        var visited = new HashSet<Guid> { providerId };
        var currentProviderId = providerId;

        while (true)
        {
            var provider = await _provider.GetProviderAsync(currentProviderId, ct);

            if (provider is null || !provider.FallbackProviderId.HasValue)
                break;

            var fallbackId = provider.FallbackProviderId.Value;

            if (visited.Contains(fallbackId))
            {
                _warnLogger.LogWarning($"Fallback cycle detected at provider {providerId}");
                break;
            }

            visited.Add(fallbackId);

            var fallbackModels = await _provider.GetEnabledModelsAtTierForProviderAsync(
                primaryModelTier,
                fallbackId,
                ct
            );

            var fallbackModel = fallbackModels
                .Select(x => new Candidate(x.Model, x.Provider))
                .FirstOrDefault();

            if (fallbackModel is not null)
            {
                candidates.Add(fallbackModel);
                currentProviderId = fallbackId;
            }
            else
            {
                break;
            }
        }

        return candidates;
    }

    private static ProviderModelConfigDto ToModelDto(ProviderModelConfig m) =>
        new(
            m.Id,
            m.ProviderId,
            m.ModelId,
            m.Name,
            m.Tier.ToString(),
            m.PricePerToken,
            m.MaxTokens,
            m.IsEnabled
        );

    private static ProviderDto ToProviderDto(LlmProvider p) =>
        new(
            p.Id,
            p.Name,
            p.BaseUrl,
            p.AdapterType.ToString(),
            p.ProviderType.ToString(),
            p.Tier.ToString(),
            p.FallbackProviderId,
            p.IsEnabled,
            p.CreatedAt,
            p.UpdatedAt
        );

    private record Candidate(ProviderModelConfig Model, LlmProvider Provider);
}
