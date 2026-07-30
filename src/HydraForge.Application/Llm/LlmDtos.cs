namespace HydraForge.Application.Llm;

// Chat

public sealed record ChatRequest(
    Guid ProviderModelConfigId,
    string ModelId,
    IReadOnlyList<ChatMessage> Messages,
    IReadOnlyList<CacheBlock> CacheBlocks,
    IReadOnlyList<ToolDefinition> Tools,
    int? MaxOutputTokens,
    decimal? Temperature);

public sealed record ChatMessage(ChatRole Role, string Content);

public sealed record ToolDefinition(
    string Name,
    string Description,
    IReadOnlyList<ToolParameter> Parameters);

public sealed record ToolParameter(
    string Name,
    string Type,
    string Description,
    bool IsRequired);

public sealed record CacheBlock(string Content, CacheBlockType Type);

public sealed record ChatChunk(
    string? Delta,
    ChatChunkFinishReason? FinishReason,
    UsageSnapshot? Usage);

public sealed record UsageSnapshot(
    int InputTokens,
    int OutputTokens,
    int CachedTokens);

// Image

public sealed record ImageRequest(
    Guid ProviderModelConfigId,
    string ModelId,
    string Prompt,
    ImageSize Size,
    int Count);

public sealed record GeneratedImage(IReadOnlyList<string> ImageDataUrlsOrKeys, string Resolution);

public sealed record ImageSize(int Width, int Height);

public sealed record InpaintRequest(
    Guid ProviderModelConfigId,
    string ModelId,
    string Prompt,
    string ImageKey,
    string MaskKey,
    ImageSize Size);

// Embedding

public sealed record EmbeddingRequest(
    Guid ProviderModelConfigId,
    string ModelId,
    IReadOnlyList<string> Inputs);

public sealed record EmbeddingResult(IReadOnlyList<ReadOnlyMemory<float>> Vectors);

// Routing

public sealed record RouteDecision(
    ProviderModelConfigDto Primary,
    ProviderDto PrimaryProvider,
    IReadOnlyList<FallbackProvider> Fallbacks);

public sealed record FallbackProvider(
    ProviderModelConfigDto Model,
    ProviderDto Provider);

// Context compression

public sealed record CompressedContext(
    IReadOnlyList<CacheBlock> Blocks,
    int EstimatedTokens,
    bool WasCompressed);

// Usage recording

public sealed record TokenUsageRecordInput(
    Guid UserId,
    Guid? ProjectId,
    Guid Feature,
    Guid ProviderModelConfigId,
    Guid ProviderId,
    string ModelId,
    string ModelName,
    int InputTokens,
    int OutputTokens,
    int CachedTokens,
    Guid? PipelineRunId,
    decimal Cost);

public sealed record ImageUsageRecordInput(
    Guid UserId,
    Guid? ProjectId,
    Guid Feature,
    Guid ProviderModelConfigId,
    Guid ProviderId,
    string ModelId,
    string ModelName,
    int ImageCount,
    string Resolution,
    decimal Cost);

// Admin probe

public sealed record ProviderModelDto(
    string ModelId,
    string Name,
    string? Description,
    IReadOnlyDictionary<string, string>? Metadata);

// Enums

public enum ChatChunkFinishReason
{
    Stop,
    Length,
    ContentFilter,
    ToolCalls,
    Unknown
}

public enum ChatRole
{
    System,
    User,
    Assistant,
    Tool
}

public enum CacheBlockType
{
    SystemContext,
    ProjectSnapshot,
    Memory
}

// Admin DTOs

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

// Shared provider model config DTO (used in RouteDecision)

public sealed record ProviderModelConfigDto(
    Guid Id,
    Guid ProviderId,
    string ModelId,
    string Name,
    string Tier,
    decimal? PricePerToken,
    int? MaxTokens,
    bool IsEnabled);
