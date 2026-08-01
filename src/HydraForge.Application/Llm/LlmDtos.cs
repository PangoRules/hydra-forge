namespace HydraForge.Application.Llm;

// Chat

public sealed record ChatRequest(
    Guid ProviderModelConfigId,
    string ModelId,
    IReadOnlyList<ChatMessage> Messages,
    IReadOnlyList<CacheBlock> CacheBlocks,
    IReadOnlyList<ToolDefinition> Tools,
    int? MaxOutputTokens,
    decimal? Temperature
);

public sealed record ChatMessage(ChatRole Role, string Content);

public sealed record ToolDefinition(
    string Name,
    string Description,
    IReadOnlyList<ToolParameter> Parameters
);

public sealed record ToolParameter(string Name, string Type, string Description, bool IsRequired);

public sealed record CacheBlock(string Content, CacheBlockType Type);

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
    IReadOnlyList<FallbackProvider> Fallbacks
);

public sealed record FallbackProvider(ProviderModelConfigDto Model, ProviderDto Provider);

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
    Guid Feature,
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
    Guid Feature,
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
}
