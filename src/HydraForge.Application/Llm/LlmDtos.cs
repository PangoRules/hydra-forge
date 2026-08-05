using HydraForge.Domain.Entities.Admin;
using HydraForge.Domain.Enums;

namespace HydraForge.Application.Llm;

// Chat

public sealed record ChatRequest(
    Guid ProviderModelConfigId,
    string ModelId,
    IReadOnlyList<ChatMessage> Messages,
    IReadOnlyList<CacheBlock> CacheBlocks,
    IReadOnlyList<ToolDefinition> Tools,
    int? MaxOutputTokens,
    decimal? Temperature,
    string? ReasoningEffort = null,
    // "Auto" | "On" | "Off" (HydraForge.Domain.Enums.OllamaThinkMode) — the resolved
    // model's admin-configured override, only read by OllamaAdapter. Callers populate
    // this from route.Primary.OllamaThinkMode; other adapters ignore it.
    string? OllamaThinkMode = null
);

public sealed record ImageBlock(string StorageKey, string MediaType);

public sealed record ChatMessage(
    ChatRole Role,
    string Content,
    IReadOnlyList<ImageBlock>? Images = null
);

public sealed record ToolDefinition(
    string Name,
    string Description,
    IReadOnlyList<ToolParameter> Parameters
);

public sealed record ToolParameter(string Name, string Type, string Description, bool IsRequired);

public sealed record CacheBlock(string Content, CacheBlockType Type, bool IsPinned = false);

public sealed record ChatChunk(
    string? Delta,
    ChatChunkFinishReason? FinishReason,
    UsageSnapshot? Usage
);

public sealed record UsageSnapshot(int InputTokens, int OutputTokens, int CachedTokens);

// Image

public sealed record ImageRequest(
    Guid ProviderModelConfigId,
    string ModelId,
    string Prompt,
    ImageSize Size,
    int Count
);

public sealed record GeneratedImage(IReadOnlyList<string> ImageDataUrlsOrKeys, string Resolution);

public enum ImageSize
{
    Square1024,
    Landscape1792,
    Portrait1024,
}

public sealed record InpaintRequest(
    Guid ProviderModelConfigId,
    string ModelId,
    string Prompt,
    byte[] ImageBytes,
    byte[] MaskBytes,
    ImageSize Size,
    int Count = 1
);

// Embedding

public sealed record EmbeddingRequest(
    Guid ProviderModelConfigId,
    string ModelId,
    IReadOnlyList<string> Inputs
);

public sealed record EmbeddingResult(IReadOnlyList<ReadOnlyMemory<float>> Vectors);

// Routing

public sealed record RouteDecision(
    ProviderModelConfigDto Primary,
    ProviderDto PrimaryProvider,
    IReadOnlyList<FallbackProvider> Fallbacks,
    LlmProvider? Provider = null
);

public sealed record FallbackProvider(ProviderModelConfigDto Model, ProviderDto Provider);

/// <summary>
/// A model the calling user may pick for a given feature — either the admin's
/// curated <c>FeatureAllowedModel</c> list (in priority order) or, when no
/// allowlist is configured, every enabled model at the feature's effective tier.
/// </summary>
public sealed record AvailableModelDto(
    Guid ProviderModelConfigId,
    string ModelName,
    string ProviderName,
    string Tier,
    bool SupportsReasoning
);

// Context compression

public sealed record CompressedContext(
    IReadOnlyList<CacheBlock> Blocks,
    int EstimatedTokens,
    bool WasCompressed
);

// Usage recording

public sealed record TokenUsageRecordInput(
    Guid UserId,
    Guid? ProjectId,
    AiFeature Feature,
    Guid ProviderModelConfigId,
    Guid ProviderId,
    string ModelId,
    string ModelName,
    int InputTokens,
    int OutputTokens,
    int CachedTokens,
    Guid? PipelineRunId,
    decimal Cost
);

public sealed record ImageUsageRecordInput(
    Guid UserId,
    Guid? ProjectId,
    AiFeature Feature,
    Guid ProviderModelConfigId,
    Guid ProviderId,
    string ModelId,
    string ModelName,
    int ImageCount,
    string Resolution,
    decimal Cost
);

// Enums

public enum ChatChunkFinishReason
{
    Stop,
    Length,
    ContentFilter,
    ToolCalls,
    Error,
}

public enum ChatRole
{
    System,
    User,
    Assistant,
    Tool,
}

public enum CacheBlockType
{
    SystemContext,
    ProjectSnapshot,
    Memory,
    RagContext,
}
