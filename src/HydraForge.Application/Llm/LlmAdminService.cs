namespace HydraForge.Application.Llm;

using HydraForge.Application.Auth;
using HydraForge.Domain.Common;
using HydraForge.Domain.Entities.Admin;
using HydraForge.Domain.Enums;
using static HydraForge.Domain.Common.DomainErrorCodes;

public sealed class LlmAdminService : ILlmAdminService
{
    private readonly ILlmAdminRepository _repo;
    private readonly IKeyVault _keyVault;
    private readonly ILlmClientFactory _llmClientFactory;
    private readonly IUserRepository _userRepo;

    public LlmAdminService(
        ILlmAdminRepository repo,
        IKeyVault keyVault,
        ILlmClientFactory llmClientFactory,
        IUserRepository userRepo
    )
    {
        _repo = repo;
        _keyVault = keyVault;
        _llmClientFactory = llmClientFactory;
        _userRepo = userRepo;
    }

    // Providers

    public async Task<Result<ProviderPageDto>> ListProvidersAsync(
        int skip,
        int take,
        string? search,
        CancellationToken ct = default
    )
    {
        var totalCount = await _repo.CountProvidersAsync(search, ct);

        var items = await _repo.ListProvidersAsync(skip, take, search, ct);

        return Result<ProviderPageDto>.Success(
            new ProviderPageDto(
                items
                    .Select(p => new ProviderDto(
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
                    ))
                    .ToList(),
                totalCount
            )
        );
    }

    public async Task<Result<ProviderDto>> CreateProviderAsync(
        CreateProviderInput input,
        CancellationToken ct = default
    )
    {
        if (string.IsNullOrWhiteSpace(input.Name))
        {
            return Result<ProviderDto>.Failure(
                new Error(DomainErrorCodes.Validation.Required, "Provider name is required.")
            );
        }

        if (string.IsNullOrWhiteSpace(input.BaseUrl))
        {
            return Result<ProviderDto>.Failure(
                new Error(DomainErrorCodes.Validation.Required, "Provider base URL is required.")
            );
        }

        if (!Enum.TryParse<AdapterType>(input.AdapterType, true, out var adapterType))
        {
            return Result<ProviderDto>.Failure(
                new Error(
                    DomainErrorCodes.Validation.InvalidValue,
                    $"Unknown adapter type: {input.AdapterType}"
                )
            );
        }

        if (!Enum.TryParse<ProviderType>(input.ProviderType, true, out var providerType))
        {
            return Result<ProviderDto>.Failure(
                new Error(
                    DomainErrorCodes.Validation.InvalidValue,
                    $"Unknown provider type: {input.ProviderType}"
                )
            );
        }

        if (!Enum.TryParse<ModelTier>(input.Tier, true, out var tier))
        {
            return Result<ProviderDto>.Failure(
                new Error(
                    DomainErrorCodes.Validation.InvalidValue,
                    $"Unknown model tier: {input.Tier}"
                )
            );
        }

        var apiKeyEncrypted = input.ApiKey is not null
            ? _keyVault.Encrypt(input.ApiKey)
            : string.Empty;

        var provider = new LlmProvider
        {
            Name = input.Name,
            BaseUrl = input.BaseUrl,
            ApiKeyEncrypted = apiKeyEncrypted,
            AdapterType = adapterType,
            ProviderType = providerType,
            Tier = tier,
            FallbackProviderId = input.FallbackProviderId,
            IsEnabled = true,
        };

        _repo.AddProvider(provider);
        await _repo.SaveChangesAsync(ct);

        return Result<ProviderDto>.Success(ToProviderDto(provider));
    }

    public async Task<Result<ProviderDto>> GetProviderAsync(Guid id, CancellationToken ct = default)
    {
        var provider = await _repo.GetProviderByIdAsync(id, ct);
        if (provider is null)
            return Result<ProviderDto>.Failure(
                new Error(DomainErrorCodes.Llm.ProviderNotFound, $"Provider not found: {id}")
            );
        return Result<ProviderDto>.Success(ToProviderDto(provider));
    }

    public async Task<Result<ProviderDto>> UpdateProviderAsync(
        Guid id,
        UpdateProviderInput input,
        CancellationToken ct = default
    )
    {
        var provider = await _repo.GetProviderByIdAsync(id, ct);
        if (provider is null)
        {
            return Result<ProviderDto>.Failure(
                new Error(DomainErrorCodes.Llm.ProviderNotFound, $"Provider not found: {id}")
            );
        }

        if (input.Name is { } name && !string.IsNullOrWhiteSpace(name))
        {
            provider.Name = name;
        }

        if (input.BaseUrl is { } baseUrl && !string.IsNullOrWhiteSpace(baseUrl))
        {
            provider.BaseUrl = baseUrl;
        }

        if (input.ApiKey is not null)
        {
            provider.ApiKeyEncrypted = _keyVault.Encrypt(input.ApiKey);
        }

        if (input.AdapterType is { } adapterTypeStr)
        {
            if (!Enum.TryParse<AdapterType>(adapterTypeStr, true, out var adapterType))
            {
                return Result<ProviderDto>.Failure(
                    new Error(
                        DomainErrorCodes.Validation.InvalidValue,
                        $"Unknown adapter type: {adapterTypeStr}"
                    )
                );
            }

            provider.AdapterType = adapterType;
        }

        if (input.ProviderType is { } providerTypeStr)
        {
            if (!Enum.TryParse<ProviderType>(providerTypeStr, true, out var providerType))
            {
                return Result<ProviderDto>.Failure(
                    new Error(
                        DomainErrorCodes.Validation.InvalidValue,
                        $"Unknown provider type: {providerTypeStr}"
                    )
                );
            }

            provider.ProviderType = providerType;
        }

        if (input.Tier is { } tierStr)
        {
            if (!Enum.TryParse<ModelTier>(tierStr, true, out var tier))
            {
                return Result<ProviderDto>.Failure(
                    new Error(
                        DomainErrorCodes.Validation.InvalidValue,
                        $"Unknown model tier: {tierStr}"
                    )
                );
            }

            provider.Tier = tier;
        }

        if (input.FallbackProviderId is { } fallbackId)
        {
            provider.FallbackProviderId = fallbackId;
        }

        if (input.IsEnabled.HasValue)
        {
            provider.IsEnabled = input.IsEnabled.Value;
        }

        provider.UpdatedAt = DateTime.UtcNow;
        await _repo.SaveChangesAsync(ct);

        return Result<ProviderDto>.Success(ToProviderDto(provider));
    }

    public async Task<Result> DisableProviderAsync(Guid id, CancellationToken ct = default)
    {
        var provider = await _repo.GetProviderByIdAsync(id, ct);
        if (provider is null)
        {
            return Result.Failure(
                new Error(DomainErrorCodes.Llm.ProviderNotFound, $"Provider not found: {id}")
            );
        }

        provider.IsEnabled = false;
        provider.UpdatedAt = DateTime.UtcNow;
        await _repo.SaveChangesAsync(ct);

        return Result.Success();
    }

    public async Task<Result> PermanentlyDeleteProviderAsync(
        Guid id,
        CancellationToken ct = default
    )
    {
        var provider = await _repo.GetProviderByIdAsync(id, ct);
        if (provider is null)
        {
            return Result.Failure(
                new Error(DomainErrorCodes.Llm.ProviderNotFound, $"Provider not found: {id}")
            );
        }

        // Token/image usage records keep their own denormalized ProviderId/ModelName
        // snapshot and aren't FK-linked to LlmProvider, so historical usage stays
        // intact and readable after the provider itself is gone.
        await _repo.RemoveModelConfigsByProviderAsync(id, ct);
        await _repo.ClearFallbackReferencesAsync(id, ct);
        _repo.RemoveProvider(provider);
        await _repo.SaveChangesAsync(ct);

        return Result.Success();
    }

    // Models

    public async Task<Result<IReadOnlyList<ProviderModelDto>>> ProbeModelsAsync(
        Guid providerId,
        CancellationToken ct = default
    )
    {
        var provider = await _repo.GetProviderByIdAsync(providerId, ct);
        if (provider is null)
        {
            return Result<IReadOnlyList<ProviderModelDto>>.Failure(
                new Error(
                    DomainErrorCodes.Llm.ProviderNotFound,
                    $"Provider not found: {providerId}"
                )
            );
        }

        var client = _llmClientFactory.For(provider);
        var result = await client.GetModelsAsync(ct);
        if (result.IsFailure)
        {
            return Result<IReadOnlyList<ProviderModelDto>>.Failure(
                new Error(
                    DomainErrorCodes.Llm.ProviderUnavailable,
                    $"Failed to probe models: {result.Error.Message}"
                )
            );
        }

        return Result<IReadOnlyList<ProviderModelDto>>.Success(result.Value);
    }

    public async Task<Result<IReadOnlyList<ProviderModelConfigDto>>> ListModelsAsync(
        Guid providerId,
        CancellationToken ct = default
    )
    {
        var provider = await _repo.GetProviderByIdAsync(providerId, ct);
        if (provider is null)
        {
            return Result<IReadOnlyList<ProviderModelConfigDto>>.Failure(
                new Error(
                    DomainErrorCodes.Llm.ProviderNotFound,
                    $"Provider not found: {providerId}"
                )
            );
        }

        var configs = await _repo.ListModelConfigsAsync(providerId, ct);
        return Result<IReadOnlyList<ProviderModelConfigDto>>.Success(
            configs.Select(ToModelConfigDto).ToList()
        );
    }

    public async Task<Result<ProviderModelConfigDto>> CreateModelAsync(
        Guid providerId,
        CreateModelInput input,
        CancellationToken ct = default
    )
    {
        var provider = await _repo.GetProviderByIdAsync(providerId, ct);
        if (provider is null)
        {
            return Result<ProviderModelConfigDto>.Failure(
                new Error(
                    DomainErrorCodes.Llm.ProviderNotFound,
                    $"Provider not found: {providerId}"
                )
            );
        }

        var existingConfigs = await _repo.ListModelConfigsAsync(providerId, ct);
        if (existingConfigs.Any(c => c.ModelId == input.ModelId))
        {
            return Result<ProviderModelConfigDto>.Failure(
                new Error(
                    DomainErrorCodes.Llm.ModelAlreadyExists,
                    $"Model '{input.ModelId}' is already configured for this provider"
                )
            );
        }

        if (!Enum.TryParse<ModelTier>(input.Tier, true, out var tier))
        {
            return Result<ProviderModelConfigDto>.Failure(
                new Error(
                    DomainErrorCodes.Validation.InvalidValue,
                    $"Unknown model tier: {input.Tier}"
                )
            );
        }

        var config = new ProviderModelConfig
        {
            ProviderId = providerId,
            ModelId = input.ModelId,
            Name = input.Name,
            Tier = tier,
            PricePerToken = input.PricePerToken,
            MaxTokens = input.MaxTokens,
            IsEnabled = input.IsEnabled,
            SupportsReasoning = input.SupportsReasoning,
        };

        _repo.AddModelConfig(config);
        await _repo.SaveChangesAsync(ct);

        return Result<ProviderModelConfigDto>.Success(ToModelConfigDto(config));
    }

    public async Task<Result<ProviderModelConfigDto>> GetModelAsync(
        Guid providerId,
        Guid modelId,
        CancellationToken ct = default
    )
    {
        var config = await _repo.GetModelConfigAsync(providerId, modelId, ct);
        if (config is null)
            return Result<ProviderModelConfigDto>.Failure(
                new Error(DomainErrorCodes.Llm.ModelNotFound, $"Model not found: {modelId}")
            );
        return Result<ProviderModelConfigDto>.Success(ToModelConfigDto(config));
    }

    public async Task<Result<ProviderModelConfigDto>> UpdateModelAsync(
        Guid providerId,
        Guid modelId,
        UpdateModelInput input,
        CancellationToken ct = default
    )
    {
        var config = await _repo.GetModelConfigAsync(providerId, modelId, ct);
        if (config is null)
        {
            return Result<ProviderModelConfigDto>.Failure(
                new Error(DomainErrorCodes.Llm.ModelNotFound, $"Model not found: {modelId}")
            );
        }

        if (input.Name is { } name)
        {
            config.Name = name;
        }

        if (input.Tier is { } tierStr)
        {
            if (!Enum.TryParse<ModelTier>(tierStr, true, out var tier))
            {
                return Result<ProviderModelConfigDto>.Failure(
                    new Error(
                        DomainErrorCodes.Validation.InvalidValue,
                        $"Unknown model tier: {tierStr}"
                    )
                );
            }

            config.Tier = tier;
        }

        if (input.PricePerToken.HasValue)
        {
            config.PricePerToken = input.PricePerToken.Value;
        }

        if (input.MaxTokens.HasValue)
        {
            config.MaxTokens = input.MaxTokens.Value;
        }

        if (input.IsEnabled.HasValue)
        {
            config.IsEnabled = input.IsEnabled.Value;
        }

        if (input.SupportsReasoning.HasValue)
        {
            config.SupportsReasoning = input.SupportsReasoning.Value;
        }

        config.UpdatedAt = DateTime.UtcNow;
        await _repo.SaveChangesAsync(ct);

        return Result<ProviderModelConfigDto>.Success(ToModelConfigDto(config));
    }

    public async Task<Result> DeleteModelAsync(
        Guid providerId,
        Guid modelId,
        CancellationToken ct = default
    )
    {
        var config = await _repo.GetModelConfigAsync(providerId, modelId, ct);
        if (config is null)
        {
            return Result.Failure(
                new Error(DomainErrorCodes.Llm.ModelNotFound, $"Model not found: {modelId}")
            );
        }

        _repo.DeleteModelConfig(config);
        await _repo.SaveChangesAsync(ct);

        return Result.Success();
    }

    // Routing

    public async Task<Result<IReadOnlyList<FeatureRoutingDto>>> ListRoutingAsync(
        CancellationToken ct = default
    )
    {
        var configs = await _repo.ListRoutingAsync(ct);
        var allowedModels = await _repo.ListAllowedModelsAsync(ct);
        var allowedByConfig = allowedModels
            .GroupBy(a => a.FeatureRoutingConfigId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<Guid>)g.Select(a => a.ProviderModelConfigId).ToList()
            );

        var dtos = configs
            .Select(c =>
                ToRoutingDto(
                    c,
                    allowedByConfig.TryGetValue(c.Id, out var ids) ? ids : Array.Empty<Guid>()
                )
            )
            .ToList();

        return Result<IReadOnlyList<FeatureRoutingDto>>.Success(dtos);
    }

    public async Task<Result<FeatureRoutingDto>> UpdateRoutingAsync(
        string feature,
        UpdateRoutingInput input,
        CancellationToken ct = default
    )
    {
        if (!Enum.TryParse<AiFeature>(feature, true, out var aiFeature))
        {
            return Result<FeatureRoutingDto>.Failure(
                new Error(DomainErrorCodes.Llm.InvalidFeature, $"Unknown feature: {feature}")
            );
        }

        var config = await _repo.GetRoutingByFeatureAsync(aiFeature, ct);
        if (config is null)
        {
            return Result<FeatureRoutingDto>.Failure(
                new Error(
                    DomainErrorCodes.Llm.RoutingNotFound,
                    $"Routing config not found for feature: {feature}"
                )
            );
        }

        if (!Enum.TryParse<ModelTier>(input.DefaultTier, true, out var defaultTier))
        {
            return Result<FeatureRoutingDto>.Failure(
                new Error(
                    DomainErrorCodes.Validation.InvalidValue,
                    $"Unknown model tier: {input.DefaultTier}"
                )
            );
        }

        config.DefaultTier = defaultTier;

        if (input.MaxUserTier is { } maxTierStr)
        {
            if (!Enum.TryParse<ModelTier>(maxTierStr, true, out var maxTier))
            {
                return Result<FeatureRoutingDto>.Failure(
                    new Error(
                        DomainErrorCodes.Validation.InvalidValue,
                        $"Unknown model tier: {maxTierStr}"
                    )
                );
            }

            config.MaxUserTier = maxTier;
        }
        else
        {
            config.MaxUserTier = null;
        }

        config.UpdatedAt = DateTime.UtcNow;
        await _repo.SaveChangesAsync(ct);

        var allowedModels = await _repo.ListAllowedModelsByFeatureAsync(config.Id, ct);
        return Result<FeatureRoutingDto>.Success(
            ToRoutingDto(config, allowedModels.Select(a => a.ProviderModelConfigId).ToList())
        );
    }

    public async Task<Result<FeatureRoutingDto>> SetAllowedModelsAsync(
        string feature,
        SetAllowedModelsInput input,
        CancellationToken ct = default
    )
    {
        if (!Enum.TryParse<AiFeature>(feature, true, out var aiFeature))
        {
            return Result<FeatureRoutingDto>.Failure(
                new Error(DomainErrorCodes.Llm.InvalidFeature, $"Unknown feature: {feature}")
            );
        }

        var config = await _repo.GetRoutingByFeatureAsync(aiFeature, ct);
        if (config is null)
        {
            return Result<FeatureRoutingDto>.Failure(
                new Error(
                    DomainErrorCodes.Llm.RoutingNotFound,
                    $"Routing config not found for feature: {feature}"
                )
            );
        }

        var modelConfigIds = input.ModelConfigIds.Distinct().ToList();

        foreach (var modelConfigId in modelConfigIds)
        {
            var model = await _repo.GetModelConfigByIdAsync(modelConfigId, ct);
            if (model is null)
            {
                return Result<FeatureRoutingDto>.Failure(
                    new Error(
                        DomainErrorCodes.Llm.ModelNotFound,
                        $"Model config not found: {modelConfigId}"
                    )
                );
            }
        }

        var existing = await _repo.ListAllowedModelsByFeatureAsync(config.Id, ct);
        foreach (var allowedModel in existing)
        {
            _repo.RemoveAllowedModel(allowedModel);
        }

        for (var priority = 0; priority < modelConfigIds.Count; priority++)
        {
            _repo.AddAllowedModel(
                new FeatureAllowedModel
                {
                    FeatureRoutingConfigId = config.Id,
                    ProviderModelConfigId = modelConfigIds[priority],
                    Priority = priority,
                }
            );
        }

        config.UpdatedAt = DateTime.UtcNow;
        await _repo.SaveChangesAsync(ct);

        return Result<FeatureRoutingDto>.Success(ToRoutingDto(config, modelConfigIds));
    }

    // Usage

    public async Task<Result<TokenUsagePageDto>> QueryTokenUsageAsync(
        Guid? userId,
        Guid? projectId,
        IReadOnlyList<string>? features,
        Guid? providerId,
        string? modelId,
        DateTime? from,
        DateTime? to,
        int skip,
        int take,
        CancellationToken ct = default
    )
    {
        if (!TryParseFeatures(features, out var aiFeatures, out var invalidFeature))
        {
            return Result<TokenUsagePageDto>.Failure(
                new Error(DomainErrorCodes.Llm.InvalidFeature, $"Unknown feature: {invalidFeature}")
            );
        }

        var (items, totalCount, totalInput, totalOutput, totalCost) =
            await _repo.QueryTokenUsageAsync(
                userId,
                projectId,
                aiFeatures,
                providerId,
                modelId,
                from,
                to,
                skip,
                take,
                ct
            );

        var userNames = await ResolveUserNamesAsync(items.Select(r => r.UserId), ct);

        return Result<TokenUsagePageDto>.Success(
            new TokenUsagePageDto(
                items
                    .Select(r => new TokenUsageDto(
                        r.Id,
                        r.UserId,
                        r.ProjectId,
                        r.Feature,
                        r.ModelName,
                        r.InputTokens,
                        r.OutputTokens,
                        r.CachedTokens,
                        r.Cost,
                        r.CreatedAt,
                        userNames.GetValueOrDefault(r.UserId)
                    ))
                    .ToList(),
                totalCount,
                totalInput,
                totalOutput,
                totalCost
            )
        );
    }

    public async Task<Result<ImageUsagePageDto>> QueryImageUsageAsync(
        Guid? userId,
        Guid? projectId,
        IReadOnlyList<string>? features,
        Guid? providerId,
        string? modelId,
        DateTime? from,
        DateTime? to,
        int skip,
        int take,
        CancellationToken ct = default
    )
    {
        if (!TryParseFeatures(features, out var aiFeatures, out var invalidFeature))
        {
            return Result<ImageUsagePageDto>.Failure(
                new Error(DomainErrorCodes.Llm.InvalidFeature, $"Unknown feature: {invalidFeature}")
            );
        }

        var (items, totalCount, totalImages, totalCost) = await _repo.QueryImageUsageAsync(
            userId,
            projectId,
            aiFeatures,
            providerId,
            modelId,
            from,
            to,
            skip,
            take,
            ct
        );

        var userNames = await ResolveUserNamesAsync(items.Select(r => r.UserId), ct);

        return Result<ImageUsagePageDto>.Success(
            new ImageUsagePageDto(
                items
                    .Select(r => new ImageUsageDto(
                        r.Id,
                        r.UserId,
                        r.ProjectId,
                        r.Feature,
                        r.ModelName,
                        r.ImageCount,
                        r.Resolution,
                        r.Cost,
                        r.CreatedAt,
                        userNames.GetValueOrDefault(r.UserId)
                    ))
                    .ToList(),
                totalCount,
                totalImages,
                totalCost
            )
        );
    }

    private static bool TryParseFeatures(
        IReadOnlyList<string>? features,
        out List<AiFeature>? parsed,
        out string? invalidValue
    )
    {
        parsed = null;
        invalidValue = null;

        if (features is null || features.Count == 0)
            return true;

        var result = new List<AiFeature>(features.Count);
        foreach (var f in features)
        {
            if (!Enum.TryParse<AiFeature>(f, true, out var value))
            {
                invalidValue = f;
                return false;
            }
            result.Add(value);
        }

        parsed = result;
        return true;
    }

    private async Task<Dictionary<Guid, string>> ResolveUserNamesAsync(
        IEnumerable<Guid> userIds,
        CancellationToken ct
    )
    {
        var distinctIds = userIds.Distinct().ToList();
        if (distinctIds.Count == 0)
            return [];

        var users = await _userRepo.FindByIdsAsync(distinctIds, ct);
        return users.ToDictionary(kv => kv.Key, kv => kv.Value.Username);
    }

    // Account usage

    public async Task<Result<AccountUsageResponse>> GetAccountUsageAsync(
        Guid userId,
        CancellationToken ct = default
    )
    {
        var budget = await _repo.GetBudgetByUserIdAsync(userId, ct);

        var periodStart = budget?.PeriodStart ?? DateTime.UtcNow;
        var periodEnd = budget?.PeriodEnd ?? DateTime.UtcNow.AddMonths(1);
        var tokensBudget = budget?.MonthlyTokenBudget ?? 0;
        var imagesBudget = budget?.MonthlyImageBudget ?? 0;

        var tokenResult = await _repo.QueryTokenUsageAsync(
            userId,
            null,
            null,
            null,
            null,
            periodStart,
            periodEnd,
            0,
            20,
            ct
        );
        var imageResult = await _repo.QueryImageUsageAsync(
            userId,
            null,
            null,
            null,
            null,
            periodStart,
            periodEnd,
            0,
            20,
            ct
        );

        var (tokenItems, _, totalInput, totalOutput, _) = tokenResult;
        var (imageItems, _, totalImages, _) = imageResult;

        var tokensUsed = totalInput + totalOutput;
        var imagesUsed = totalImages;

        var recentCalls = new List<RecentCallDto>();

        foreach (var t in tokenItems)
        {
            recentCalls.Add(
                new RecentCallDto(
                    t.Feature.ToString(),
                    t.ModelName,
                    t.InputTokens + t.OutputTokens,
                    0,
                    t.Cost,
                    t.CreatedAt
                )
            );
        }

        foreach (var i in imageItems)
        {
            recentCalls.Add(
                new RecentCallDto(
                    i.Feature.ToString(),
                    i.ModelName,
                    0,
                    i.ImageCount,
                    i.Cost,
                    i.CreatedAt
                )
            );
        }

        var sorted = recentCalls.OrderByDescending(r => r.Timestamp).Take(20).ToList();

        return Result<AccountUsageResponse>.Success(
            new AccountUsageResponse(
                tokensUsed,
                tokensBudget,
                imagesUsed,
                imagesBudget,
                periodStart,
                periodEnd,
                sorted
            )
        );
    }

    // Budget

    public async Task<Result<UserBudgetDto>> GetBudgetAsync(
        Guid userId,
        CancellationToken ct = default
    )
    {
        var budget = await _repo.GetBudgetByUserIdAsync(userId, ct);

        if (budget is null)
        {
            return Result<UserBudgetDto>.Success(
                new UserBudgetDto(userId, 0, 0, 0, 0, DateTime.UtcNow, DateTime.UtcNow.AddMonths(1))
            );
        }

        return Result<UserBudgetDto>.Success(
            new UserBudgetDto(
                budget.UserId,
                budget.MonthlyTokenBudget,
                budget.MonthlyTokenUsed,
                budget.MonthlyImageBudget,
                budget.MonthlyImageUsed,
                budget.PeriodStart,
                budget.PeriodEnd
            )
        );
    }

    public async Task<Result<UserBudgetDto>> UpdateBudgetAsync(
        Guid userId,
        UpdateBudgetInput input,
        CancellationToken ct = default
    )
    {
        var budget = await _repo.GetBudgetByUserIdAsync(userId, ct);

        if (budget is null)
        {
            budget = new UserTokenBudget
            {
                UserId = userId,
                MonthlyTokenBudget = input.MonthlyTokenBudget ?? 0,
                MonthlyImageBudget = input.MonthlyImageBudget ?? 0,
                PeriodStart = DateTime.UtcNow,
                PeriodEnd = DateTime.UtcNow.AddMonths(1),
            };
            _repo.AddBudget(budget);
        }
        else
        {
            if (input.MonthlyTokenBudget.HasValue)
            {
                budget.MonthlyTokenBudget = input.MonthlyTokenBudget.Value;
            }

            if (input.MonthlyImageBudget.HasValue)
            {
                budget.MonthlyImageBudget = input.MonthlyImageBudget.Value;
            }
        }

        await _repo.SaveChangesAsync(ct);

        return Result<UserBudgetDto>.Success(
            new UserBudgetDto(
                budget.UserId,
                budget.MonthlyTokenBudget,
                budget.MonthlyTokenUsed,
                budget.MonthlyImageBudget,
                budget.MonthlyImageUsed,
                budget.PeriodStart,
                budget.PeriodEnd
            )
        );
    }

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

    private static ProviderModelConfigDto ToModelConfigDto(ProviderModelConfig c) =>
        new(
            c.Id,
            c.ProviderId,
            c.ModelId,
            c.Name,
            c.Tier.ToString(),
            c.PricePerToken,
            c.MaxTokens,
            c.IsEnabled,
            c.SupportsReasoning
        );

    private static FeatureRoutingDto ToRoutingDto(
        FeatureRoutingConfig c,
        IReadOnlyList<Guid> allowedModelConfigIds
    ) =>
        new(
            c.Id,
            c.Feature,
            c.DefaultTier.ToString(),
            c.MaxUserTier?.ToString(),
            c.CreatedAt,
            c.UpdatedAt,
            allowedModelConfigIds
        );
}
