namespace HydraForge.Application.Llm;

// Provider DTOs

public sealed record ProviderDto(
    Guid Id,
    string Name,
    string BaseUrl,
    string AdapterType,
    string ProviderType,
    string Tier,
    Guid? FallbackProviderId,
    bool IsEnabled,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record ProviderPageDto(IReadOnlyList<ProviderDto> Items, int TotalCount);

public sealed record CreateProviderInput(
    string Name,
    string BaseUrl,
    string? ApiKey,
    string AdapterType,
    string ProviderType,
    string Tier,
    Guid? FallbackProviderId);

public sealed record UpdateProviderInput(
    string? Name,
    string? BaseUrl,
    string? ApiKey,
    string? Tier,
    Guid? FallbackProviderId,
    bool? IsEnabled);

// Model DTOs

public sealed record ProviderModelDto(
    string ModelId,
    string Name,
    string? Description,
    IReadOnlyDictionary<string, string>? Metadata);

public sealed record ProviderModelConfigDto(
    Guid Id,
    Guid ProviderId,
    string ModelId,
    string Name,
    string Tier,
    decimal? PricePerToken,
    int? MaxTokens,
    bool IsEnabled);

public sealed record CreateModelInput(
    string ModelId,
    string Name,
    string Tier,
    decimal? PricePerToken,
    int? MaxTokens,
    bool IsEnabled);

public sealed record UpdateModelInput(
    string? Name,
    string? Tier,
    decimal? PricePerToken,
    int? MaxTokens,
    bool? IsEnabled);

// Routing DTOs

public sealed record FeatureRoutingDto(
    Guid Id,
    Guid Feature,
    string DefaultTier,
    string? MaxUserTier,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record UpdateRoutingInput(
    string DefaultTier,
    string? MaxUserTier);

// Usage DTOs

public sealed record TokenUsagePageDto(
    IReadOnlyList<TokenUsageDto> Items,
    int TotalCount,
    int TotalInputTokens,
    int TotalOutputTokens,
    decimal TotalCost);

public sealed record TokenUsageDto(
    Guid Id,
    Guid UserId,
    Guid? ProjectId,
    Guid Feature,
    string ModelName,
    int InputTokens,
    int OutputTokens,
    int CachedTokens,
    decimal Cost,
    DateTime CreatedAt);

public sealed record ImageUsagePageDto(
    IReadOnlyList<ImageUsageDto> Items,
    int TotalCount,
    int TotalImageCount,
    decimal TotalCost);

public sealed record ImageUsageDto(
    Guid Id,
    Guid UserId,
    Guid? ProjectId,
    Guid Feature,
    string ModelName,
    int ImageCount,
    string Resolution,
    decimal Cost,
    DateTime CreatedAt);

// Budget DTOs

public sealed record UserBudgetDto(
    Guid UserId,
    int? DailyLimit,
    int? MonthlyLimit,
    int MonthlyTokenBudget,
    int MonthlyTokenUsed,
    int MonthlyImageBudget,
    int MonthlyImageUsed,
    DateTime PeriodStart,
    DateTime PeriodEnd);

public sealed record UpdateBudgetInput(
    int? DailyLimit,
    int? MonthlyLimit,
    int? MonthlyTokenBudget,
    int? MonthlyImageBudget);
