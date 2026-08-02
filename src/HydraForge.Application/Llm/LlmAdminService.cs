namespace HydraForge.Application.Llm;

using HydraForge.Domain.Common;
using HydraForge.Domain.Entities.Admin;
using HydraForge.Domain.Enums;
using static HydraForge.Domain.Common.DomainErrorCodes;

public sealed class LlmAdminService : ILlmAdminService
{
    private readonly ILlmAdminRepository _repo;
    private readonly IKeyVault _keyVault;
    private readonly ILlmClientFactory _llmClientFactory;

    public LlmAdminService(
        ILlmAdminRepository repo,
        IKeyVault keyVault,
        ILlmClientFactory llmClientFactory
    )
    {
        _repo = repo;
        _keyVault = keyVault;
        _llmClientFactory = llmClientFactory;
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

        var dtos = configs
            .Select(c => new FeatureRoutingDto(
                c.Id,
                c.Feature,
                c.DefaultTier.ToString(),
                c.MaxUserTier?.ToString(),
                c.CreatedAt,
                c.UpdatedAt
            ))
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

        return Result<FeatureRoutingDto>.Success(ToRoutingDto(config));
    }

    // Usage

    public async Task<Result<TokenUsagePageDto>> QueryTokenUsageAsync(
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
    )
    {
        AiFeature? aiFeature = null;
        if (feature is { } f)
        {
            if (!Enum.TryParse<AiFeature>(f, true, out var parsed))
            {
                return Result<TokenUsagePageDto>.Failure(
                    new Error(DomainErrorCodes.Llm.InvalidFeature, $"Unknown feature: {f}")
                );
            }
            aiFeature = parsed;
        }

        var (items, totalCount, totalInput, totalOutput, totalCost) =
            await _repo.QueryTokenUsageAsync(
                userId,
                projectId,
                aiFeature,
                providerId,
                modelId,
                from,
                to,
                skip,
                take,
                ct
            );

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
                        r.CreatedAt
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
        string? feature,
        Guid? providerId,
        string? modelId,
        DateTime? from,
        DateTime? to,
        int skip,
        int take,
        CancellationToken ct = default
    )
    {
        AiFeature? aiFeature = null;
        if (feature is { } f)
        {
            if (!Enum.TryParse<AiFeature>(f, true, out var parsed))
            {
                return Result<ImageUsagePageDto>.Failure(
                    new Error(DomainErrorCodes.Llm.InvalidFeature, $"Unknown feature: {f}")
                );
            }
            aiFeature = parsed;
        }

        var (items, totalCount, totalImages, totalCost) = await _repo.QueryImageUsageAsync(
            userId,
            projectId,
            aiFeature,
            providerId,
            modelId,
            from,
            to,
            skip,
            take,
            ct
        );

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
                        r.CreatedAt
                    ))
                    .ToList(),
                totalCount,
                totalImages,
                totalCost
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
                new UserBudgetDto(
                    userId,
                    null,
                    null,
                    0,
                    0,
                    0,
                    0,
                    DateTime.UtcNow,
                    DateTime.UtcNow.AddMonths(1)
                )
            );
        }

        return Result<UserBudgetDto>.Success(
            new UserBudgetDto(
                budget.UserId,
                budget.DailyLimit,
                budget.MonthlyLimit,
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
                DailyLimit = input.DailyLimit,
                MonthlyLimit = input.MonthlyLimit,
                MonthlyTokenBudget = input.MonthlyTokenBudget ?? 0,
                MonthlyImageBudget = input.MonthlyImageBudget ?? 0,
                PeriodStart = DateTime.UtcNow,
                PeriodEnd = DateTime.UtcNow.AddMonths(1),
            };
            _repo.AddBudget(budget);
        }
        else
        {
            if (input.DailyLimit.HasValue)
            {
                budget.DailyLimit = input.DailyLimit.Value;
            }

            if (input.MonthlyLimit.HasValue)
            {
                budget.MonthlyLimit = input.MonthlyLimit.Value;
            }

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
                budget.DailyLimit,
                budget.MonthlyLimit,
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
            c.IsEnabled
        );

    private static FeatureRoutingDto ToRoutingDto(FeatureRoutingConfig c) =>
        new(
            c.Id,
            c.Feature,
            c.DefaultTier.ToString(),
            c.MaxUserTier?.ToString(),
            c.CreatedAt,
            c.UpdatedAt
        );
}
