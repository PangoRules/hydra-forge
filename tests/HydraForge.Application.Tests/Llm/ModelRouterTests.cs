namespace HydraForge.Application.Tests.Llm;

using HydraForge.Application.Llm;
using HydraForge.Application.Logging;
using HydraForge.Domain.Common;
using HydraForge.Domain.Entities.Admin;
using HydraForge.Domain.Enums;

public class ModelRouterTests
{
    private static ModelRouter CreateRouter(IRoutingConfigProvider provider) =>
        new(provider, new NullWarnLogger());

    [Fact]
    public async Task ResolveAsync_FeatureNotConfigured_ReturnsNoModelForFeature()
    {
        var router = CreateRouter(new FakeRoutingConfigProvider());

        var result = await router.ResolveAsync(AiFeature.DeepResearch, Guid.NewGuid(), null, 1000);

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Llm.NoModelForFeature, result.Error.Code);
    }

    [Fact]
    public async Task ResolveAsync_DefaultTierWithEnabledProvider_ReturnsSuccess()
    {
        var providerId = Guid.NewGuid();
        var modelId = Guid.NewGuid();
        var provider = new FakeRoutingConfigProvider();
        provider.AddRouting(AiFeature.DeepResearch, ModelTier.Standard, null);
        provider.AddEnabledProvider(providerId, "TestProvider", ModelTier.Standard);
        provider.AddModel(modelId, providerId, "gpt-4", ModelTier.Standard, maxTokens: 8192);

        var router = CreateRouter(provider);

        var result = await router.ResolveAsync(AiFeature.DeepResearch, Guid.NewGuid(), null, 1000);

        Assert.True(result.IsSuccess);
        Assert.Equal("gpt-4", result.Value.Primary.ModelId);
    }

    [Fact]
    public async Task ResolveAsync_TierCeiling_CapsToMaxUserTier()
    {
        var providerId = Guid.NewGuid();
        var modelId = Guid.NewGuid();
        var provider = new FakeRoutingConfigProvider();
        // Default is Premium, but MaxUserTier caps to Standard
        provider.AddRouting(AiFeature.DeepResearch, ModelTier.Premium, ModelTier.Standard);
        // Only Standard model is enabled
        provider.AddEnabledProvider(providerId, "StdProvider", ModelTier.Standard);
        provider.AddModel(
            modelId,
            providerId,
            "standard-model",
            ModelTier.Standard,
            maxTokens: 4096
        );

        var router = CreateRouter(provider);

        var result = await router.ResolveAsync(AiFeature.DeepResearch, Guid.NewGuid(), null, 1000);

        Assert.True(result.IsSuccess);
        Assert.Equal("standard-model", result.Value.Primary.ModelId);
    }

    [Fact]
    public async Task ResolveAsync_NullMaxUserTier_StaysAtDefault()
    {
        var providerId = Guid.NewGuid();
        var modelId = Guid.NewGuid();
        var provider = new FakeRoutingConfigProvider();
        // MaxUserTier is null, so it stays at default Standard
        provider.AddRouting(AiFeature.DeepResearch, ModelTier.Standard, null);
        provider.AddEnabledProvider(providerId, "TestProvider", ModelTier.Standard);
        provider.AddModel(modelId, providerId, "gpt-4", ModelTier.Standard, maxTokens: 8192);

        var router = CreateRouter(provider);

        var result = await router.ResolveAsync(AiFeature.DeepResearch, Guid.NewGuid(), null, 1000);

        Assert.True(result.IsSuccess);
        Assert.Equal("gpt-4", result.Value.Primary.ModelId);
    }

    [Fact]
    public async Task ResolveAsync_TokensExceedModelContextWindow_BumpsToHigherTier()
    {
        var ecoProviderId = Guid.NewGuid();
        var stdProviderId = Guid.NewGuid();
        var premProviderId = Guid.NewGuid();
        var provider = new FakeRoutingConfigProvider();
        provider.AddRouting(AiFeature.DeepResearch, ModelTier.Economy, null);
        provider.AddEnabledProvider(ecoProviderId, "EcoProvider", ModelTier.Economy);
        provider.AddEnabledProvider(stdProviderId, "StdProvider", ModelTier.Standard);
        provider.AddEnabledProvider(premProviderId, "PremProvider", ModelTier.Premium);
        provider.AddModel(
            Guid.NewGuid(),
            ecoProviderId,
            "economy-small",
            ModelTier.Economy,
            maxTokens: 512
        );
        provider.AddModel(
            Guid.NewGuid(),
            stdProviderId,
            "standard-medium",
            ModelTier.Standard,
            maxTokens: 4096
        );
        provider.AddModel(
            Guid.NewGuid(),
            premProviderId,
            "premium-big",
            ModelTier.Premium,
            maxTokens: 16384
        );

        var router = CreateRouter(provider);

        // 10000 tokens: Economy (512 < 10000) → Standard (4096 < 10000) → Premium (16384 >= 10000)
        var result = await router.ResolveAsync(AiFeature.DeepResearch, Guid.NewGuid(), null, 10000);

        Assert.True(result.IsSuccess);
        Assert.Equal("premium-big", result.Value.Primary.ModelId);
    }

    [Fact]
    public async Task ResolveAsync_BumpedCandidateHasNullMaxTokens_TreatedAsUnlimited()
    {
        var stdProviderId = Guid.NewGuid();
        var provider = new FakeRoutingConfigProvider();
        // Default tier Economy has no models — must bump to Standard.
        provider.AddRouting(AiFeature.ProjectNarrative, ModelTier.Economy, null);
        provider.AddEnabledProvider(stdProviderId, "StdProvider", ModelTier.Standard);
        // MaxTokens unset (null) — means "no configured limit", not "zero capacity".
        provider.AddModel(Guid.NewGuid(), stdProviderId, "local-model", ModelTier.Standard);

        var router = CreateRouter(provider);

        var result = await router.ResolveAsync(
            AiFeature.ProjectNarrative,
            Guid.NewGuid(),
            null,
            4000
        );

        Assert.True(result.IsSuccess);
        Assert.Equal("local-model", result.Value.Primary.ModelId);
    }

    [Fact]
    public async Task ResolveAsync_NoModelFitsAfterAllTiers_ReturnsContextWindowExceeded()
    {
        var ecoProviderId = Guid.NewGuid();
        var stdProviderId = Guid.NewGuid();
        var premProviderId = Guid.NewGuid();
        var provider = new FakeRoutingConfigProvider();
        provider.AddRouting(AiFeature.DeepResearch, ModelTier.Economy, null);
        provider.AddEnabledProvider(ecoProviderId, "EcoProvider", ModelTier.Economy);
        provider.AddModel(
            Guid.NewGuid(),
            ecoProviderId,
            "eco-small",
            ModelTier.Economy,
            maxTokens: 512
        );
        provider.AddEnabledProvider(stdProviderId, "StdProvider", ModelTier.Standard);
        provider.AddModel(
            Guid.NewGuid(),
            stdProviderId,
            "std-small",
            ModelTier.Standard,
            maxTokens: 1024
        );
        provider.AddEnabledProvider(premProviderId, "PremProvider", ModelTier.Premium);
        provider.AddModel(
            Guid.NewGuid(),
            premProviderId,
            "prem-small",
            ModelTier.Premium,
            maxTokens: 2048
        );

        var router = CreateRouter(provider);

        // 5000 tokens exceeds all tiers: Economy (512), Standard (1024), Premium (2048)
        var result = await router.ResolveAsync(AiFeature.DeepResearch, Guid.NewGuid(), null, 5000);

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Llm.ContextWindowExceeded, result.Error.Code);
    }

    [Fact]
    public async Task ResolveAsync_FallbackChainSingleHop_ReturnsFallback()
    {
        var primaryId = Guid.NewGuid();
        var fallbackId = Guid.NewGuid();
        var primaryModelId = Guid.NewGuid();
        var fallbackModelId = Guid.NewGuid();
        var provider = new FakeRoutingConfigProvider();
        provider.AddRouting(AiFeature.DeepResearch, ModelTier.Standard, null);
        // Primary must sort first ('A') so it's selected as primary, not the fallback
        provider.AddEnabledProvider(
            primaryId,
            "APrimary",
            ModelTier.Standard,
            fallbackProviderId: fallbackId
        );
        provider.AddModel(
            primaryModelId,
            primaryId,
            "primary-model",
            ModelTier.Standard,
            maxTokens: 8192
        );
        provider.AddEnabledProvider(fallbackId, "BFallback", ModelTier.Standard);
        provider.AddModel(
            fallbackModelId,
            fallbackId,
            "fallback-model",
            ModelTier.Standard,
            maxTokens: 8192
        );

        var router = CreateRouter(provider);

        var result = await router.ResolveAsync(AiFeature.DeepResearch, Guid.NewGuid(), null, 1000);

        Assert.True(result.IsSuccess);
        Assert.Equal("primary-model", result.Value.Primary.ModelId);
        Assert.Single(result.Value.Fallbacks);
        Assert.Equal("fallback-model", result.Value.Fallbacks[0].Model.ModelId);
    }

    [Fact]
    public async Task ResolveAsync_FallbackChainMultiHop_ReturnsAllFallbacks()
    {
        var primaryId = Guid.NewGuid();
        var hop1Id = Guid.NewGuid();
        var hop2Id = Guid.NewGuid();
        var provider = new FakeRoutingConfigProvider();
        provider.AddRouting(AiFeature.DeepResearch, ModelTier.Standard, null);
        provider.AddEnabledProvider(
            primaryId,
            "APrimary",
            ModelTier.Standard,
            fallbackProviderId: hop1Id
        );
        provider.AddModel(
            Guid.NewGuid(),
            primaryId,
            "primary-model",
            ModelTier.Standard,
            maxTokens: 8192
        );
        provider.AddEnabledProvider(
            hop1Id,
            "BHop1",
            ModelTier.Standard,
            fallbackProviderId: hop2Id
        );
        provider.AddModel(
            Guid.NewGuid(),
            hop1Id,
            "hop1-model",
            ModelTier.Standard,
            maxTokens: 8192
        );
        provider.AddEnabledProvider(hop2Id, "CHop2", ModelTier.Standard);
        provider.AddModel(
            Guid.NewGuid(),
            hop2Id,
            "hop2-model",
            ModelTier.Standard,
            maxTokens: 8192
        );

        var router = CreateRouter(provider);

        var result = await router.ResolveAsync(AiFeature.DeepResearch, Guid.NewGuid(), null, 1000);

        Assert.True(result.IsSuccess);
        Assert.Equal("primary-model", result.Value.Primary.ModelId);
        Assert.Equal(2, result.Value.Fallbacks.Count);
        Assert.Equal("hop1-model", result.Value.Fallbacks[0].Model.ModelId);
        Assert.Equal("hop2-model", result.Value.Fallbacks[1].Model.ModelId);
    }

    [Fact]
    public async Task ResolveAsync_FallbackCycleDetection_StopsAtCycle()
    {
        var providerA = Guid.NewGuid();
        var providerB = Guid.NewGuid();
        var provider = new FakeRoutingConfigProvider();
        provider.AddRouting(AiFeature.DeepResearch, ModelTier.Standard, null);
        provider.AddEnabledProvider(
            providerA,
            "AProviderA",
            ModelTier.Standard,
            fallbackProviderId: providerB
        );
        provider.AddModel(
            Guid.NewGuid(),
            providerA,
            "model-a",
            ModelTier.Standard,
            maxTokens: 8192
        );
        provider.AddEnabledProvider(
            providerB,
            "BProviderB",
            ModelTier.Standard,
            fallbackProviderId: providerA
        );
        provider.AddModel(
            Guid.NewGuid(),
            providerB,
            "model-b",
            ModelTier.Standard,
            maxTokens: 8192
        );

        var router = CreateRouter(provider);

        var result = await router.ResolveAsync(AiFeature.DeepResearch, Guid.NewGuid(), null, 1000);

        Assert.True(result.IsSuccess);
        Assert.Equal("model-a", result.Value.Primary.ModelId);
        // Cycle detected after A→B→A, so only B (1 hop) should be in fallbacks
        Assert.Single(result.Value.Fallbacks);
        Assert.Equal("model-b", result.Value.Fallbacks[0].Model.ModelId);
    }

    [Fact]
    public async Task ResolveAsync_DisabledProviders_AreSkipped()
    {
        var enabledId = Guid.NewGuid();
        var disabledId = Guid.NewGuid();
        var provider = new FakeRoutingConfigProvider();
        provider.AddRouting(AiFeature.DeepResearch, ModelTier.Standard, null);
        provider.AddEnabledProvider(disabledId, "Disabled", ModelTier.Standard, isEnabled: false);
        provider.AddModel(
            Guid.NewGuid(),
            disabledId,
            "disabled-model",
            ModelTier.Standard,
            maxTokens: 8192
        );
        provider.AddEnabledProvider(enabledId, "Enabled", ModelTier.Standard);
        provider.AddModel(
            Guid.NewGuid(),
            enabledId,
            "enabled-model",
            ModelTier.Standard,
            maxTokens: 8192
        );

        var router = CreateRouter(provider);

        var result = await router.ResolveAsync(AiFeature.DeepResearch, Guid.NewGuid(), null, 1000);

        Assert.True(result.IsSuccess);
        Assert.Equal("enabled-model", result.Value.Primary.ModelId);
    }

    [Fact]
    public async Task ResolveAsync_DefaultTierIsPremiumAndModelTooSmall_ReturnsContextWindowExceeded()
    {
        // Regression: TryAutoBumpTierAsync has nothing to bump to when the initial tier is
        // already Premium, so it reports hadAnyModel=false even though a Premium model exists —
        // ResolveAsync must still recognize the initial-tier candidate and report
        // ContextWindowExceeded, not the misleading NoModelForFeature.
        var premProviderId = Guid.NewGuid();
        var provider = new FakeRoutingConfigProvider();
        provider.AddRouting(AiFeature.DeepResearch, ModelTier.Premium, null);
        provider.AddEnabledProvider(premProviderId, "PremProvider", ModelTier.Premium);
        provider.AddModel(
            Guid.NewGuid(),
            premProviderId,
            "prem-small",
            ModelTier.Premium,
            maxTokens: 2048
        );

        var router = CreateRouter(provider);

        var result = await router.ResolveAsync(AiFeature.DeepResearch, Guid.NewGuid(), null, 5000);

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Llm.ContextWindowExceeded, result.Error.Code);
    }

    [Fact]
    public async Task ResolveAsync_NoEnabledProviderAtTier_ReturnsNoModelForFeature()
    {
        var provider = new FakeRoutingConfigProvider();
        provider.AddRouting(AiFeature.DeepResearch, ModelTier.Standard, null);

        var router = CreateRouter(provider);

        var result = await router.ResolveAsync(AiFeature.DeepResearch, Guid.NewGuid(), null, 1000);

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Llm.NoModelForFeature, result.Error.Code);
    }

    [Fact]
    public async Task ResolveAsync_AllowlistConfigured_IgnoresTierAndUsesPriorityOrder()
    {
        var providerId = Guid.NewGuid();
        var lowPriorityModelId = Guid.NewGuid();
        var highPriorityModelId = Guid.NewGuid();
        var provider = new FakeRoutingConfigProvider();
        // Tier says Premium, but an allowlist exists so tier is irrelevant to model choice.
        provider.AddRouting(AiFeature.DeepResearch, ModelTier.Premium, null);
        provider.AddEnabledProvider(providerId, "TestProvider", ModelTier.Economy);
        provider.AddModel(
            lowPriorityModelId,
            providerId,
            "economy-model",
            ModelTier.Economy,
            maxTokens: 8192
        );
        provider.AddModel(
            highPriorityModelId,
            providerId,
            "another-economy-model",
            ModelTier.Economy,
            maxTokens: 8192
        );
        provider.AddAllowedModel(AiFeature.DeepResearch, highPriorityModelId, priority: 0);
        provider.AddAllowedModel(AiFeature.DeepResearch, lowPriorityModelId, priority: 1);

        var router = CreateRouter(provider);

        var result = await router.ResolveAsync(AiFeature.DeepResearch, Guid.NewGuid(), null, 1000);

        Assert.True(result.IsSuccess);
        Assert.Equal("another-economy-model", result.Value.Primary.ModelId);
    }

    [Fact]
    public async Task ResolveAsync_AllowlistConfigured_SkipsHigherPriorityModelWhenTooSmall()
    {
        var providerId = Guid.NewGuid();
        var tooSmallModelId = Guid.NewGuid();
        var fittingModelId = Guid.NewGuid();
        var provider = new FakeRoutingConfigProvider();
        provider.AddRouting(AiFeature.DeepResearch, ModelTier.Standard, null);
        provider.AddEnabledProvider(providerId, "TestProvider", ModelTier.Standard);
        provider.AddModel(
            tooSmallModelId,
            providerId,
            "small-model",
            ModelTier.Standard,
            maxTokens: 100
        );
        provider.AddModel(
            fittingModelId,
            providerId,
            "big-model",
            ModelTier.Standard,
            maxTokens: 8192
        );
        provider.AddAllowedModel(AiFeature.DeepResearch, tooSmallModelId, priority: 0);
        provider.AddAllowedModel(AiFeature.DeepResearch, fittingModelId, priority: 1);

        var router = CreateRouter(provider);

        var result = await router.ResolveAsync(AiFeature.DeepResearch, Guid.NewGuid(), null, 5000);

        Assert.True(result.IsSuccess);
        Assert.Equal("big-model", result.Value.Primary.ModelId);
    }

    [Fact]
    public async Task ResolveAsync_AllowlistConfigured_DisabledModelIsSkipped()
    {
        var providerId = Guid.NewGuid();
        var disabledModelId = Guid.NewGuid();
        var provider = new FakeRoutingConfigProvider();
        provider.AddRouting(AiFeature.DeepResearch, ModelTier.Standard, null);
        provider.AddEnabledProvider(providerId, "TestProvider", ModelTier.Standard);
        provider.AddModel(
            disabledModelId,
            providerId,
            "disabled-model",
            ModelTier.Standard,
            maxTokens: 8192,
            isEnabled: false
        );
        provider.AddAllowedModel(AiFeature.DeepResearch, disabledModelId, priority: 0);

        var router = CreateRouter(provider);

        var result = await router.ResolveAsync(AiFeature.DeepResearch, Guid.NewGuid(), null, 1000);

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Llm.NoModelForFeature, result.Error.Code);
    }

    [Fact]
    public async Task ResolveAsync_AllowlistConfigured_AllModelsTooSmall_ReturnsContextWindowExceeded()
    {
        var providerId = Guid.NewGuid();
        var tooSmallModelId = Guid.NewGuid();
        var provider = new FakeRoutingConfigProvider();
        provider.AddRouting(AiFeature.DeepResearch, ModelTier.Standard, null);
        provider.AddEnabledProvider(providerId, "TestProvider", ModelTier.Standard);
        provider.AddModel(
            tooSmallModelId,
            providerId,
            "small-model",
            ModelTier.Standard,
            maxTokens: 100
        );
        provider.AddAllowedModel(AiFeature.DeepResearch, tooSmallModelId, priority: 0);

        var router = CreateRouter(provider);

        var result = await router.ResolveAsync(AiFeature.DeepResearch, Guid.NewGuid(), null, 5000);

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Llm.ContextWindowExceeded, result.Error.Code);
    }
}

internal class FakeRoutingConfigProvider : IRoutingConfigProvider
{
    private readonly Dictionary<AiFeature, FeatureRoutingConfig> _routing = new();
    private readonly List<LlmProvider> _providers = new();
    private readonly List<ProviderModelConfig> _models = new();
    private readonly List<(
        Guid FeatureRoutingConfigId,
        Guid ProviderModelConfigId,
        int Priority
    )> _allowedModels = new();

    public void AddRouting(AiFeature feature, ModelTier defaultTier, ModelTier? maxUserTier)
    {
        _routing[feature] = new FeatureRoutingConfig
        {
            Id = Guid.NewGuid(),
            Feature = feature,
            DefaultTier = defaultTier,
            MaxUserTier = maxUserTier,
        };
    }

    public void AddAllowedModel(AiFeature feature, Guid providerModelConfigId, int priority)
    {
        _allowedModels.Add((_routing[feature].Id, providerModelConfigId, priority));
    }

    public void AddEnabledProvider(
        Guid id,
        string name,
        ModelTier tier,
        Guid? fallbackProviderId = null,
        bool isEnabled = true
    )
    {
        _providers.Add(
            new LlmProvider
            {
                Id = id,
                Name = name,
                BaseUrl = $"https://{name.ToLowerInvariant()}.com",
                AdapterType = AdapterType.OpenAiCompatible,
                ProviderType = ProviderType.Text,
                Tier = tier,
                FallbackProviderId = fallbackProviderId,
                IsEnabled = isEnabled,
            }
        );
    }

    public void AddModel(
        Guid id,
        Guid providerId,
        string modelId,
        ModelTier tier,
        int? maxTokens = null,
        bool isEnabled = true
    )
    {
        _models.Add(
            new ProviderModelConfig
            {
                Id = id,
                ProviderId = providerId,
                ModelId = modelId,
                Name = modelId,
                Tier = tier,
                MaxTokens = maxTokens,
                IsEnabled = isEnabled,
            }
        );
    }

    public Task<FeatureRoutingConfig?> GetRoutingConfigAsync(
        AiFeature feature,
        CancellationToken ct
    )
    {
        return Task.FromResult(_routing.TryGetValue(feature, out var cfg) ? cfg : null);
    }

    public Task<
        IReadOnlyList<(ProviderModelConfig Model, LlmProvider Provider)>
    > GetEnabledModelsAtTierAsync(ModelTier tier, CancellationToken ct)
    {
        var results = _models
            .Where(m => m.IsEnabled && m.Tier == tier)
            .Join(
                _providers.Where(p => p.IsEnabled),
                m => m.ProviderId,
                p => p.Id,
                (m, p) => (m, p)
            )
            .OrderBy(x => x.p.Name)
            .ToList();

        return Task.FromResult<IReadOnlyList<(ProviderModelConfig Model, LlmProvider Provider)>>(
            results
        );
    }

    public Task<LlmProvider?> GetProviderAsync(Guid providerId, CancellationToken ct)
    {
        return Task.FromResult(_providers.FirstOrDefault(p => p.Id == providerId));
    }

    public Task<
        IReadOnlyList<(ProviderModelConfig Model, LlmProvider Provider)>
    > GetEnabledModelsAtTierForProviderAsync(ModelTier tier, Guid providerId, CancellationToken ct)
    {
        var results = _models
            .Where(m => m.IsEnabled && m.Tier == tier && m.ProviderId == providerId)
            .Join(
                _providers.Where(p => p.IsEnabled && p.Id == providerId),
                m => m.ProviderId,
                p => p.Id,
                (m, p) => (m, p)
            )
            .ToList();

        return Task.FromResult<IReadOnlyList<(ProviderModelConfig Model, LlmProvider Provider)>>(
            results
        );
    }

    public Task<
        IReadOnlyList<(ProviderModelConfig Model, LlmProvider Provider)>
    > GetAllowedModelsAsync(Guid featureRoutingConfigId, CancellationToken ct)
    {
        var results = _allowedModels
            .Where(a => a.FeatureRoutingConfigId == featureRoutingConfigId)
            .OrderBy(a => a.Priority)
            .Join(
                _models,
                a => a.ProviderModelConfigId,
                m => m.Id,
                (a, m) => new { a.Priority, Model = m }
            )
            .Join(
                _providers,
                x => x.Model.ProviderId,
                p => p.Id,
                (x, p) =>
                    new
                    {
                        x.Priority,
                        Model = x.Model,
                        Provider = p,
                    }
            )
            .OrderBy(x => x.Priority)
            .Select(x => (x.Model, x.Provider))
            .ToList();

        return Task.FromResult<IReadOnlyList<(ProviderModelConfig Model, LlmProvider Provider)>>(
            results
        );
    }
}
