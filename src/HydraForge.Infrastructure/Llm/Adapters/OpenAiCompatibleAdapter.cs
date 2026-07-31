namespace HydraForge.Infrastructure.Llm.Adapters;

using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using HydraForge.Application.Llm;
using HydraForge.Domain.Common;
using HydraForge.Domain.Entities.Admin;
using HydraForge.Domain.Enums;

public sealed class OpenAiCompatibleAdapter(
    HttpClient http,
    IKeyVault keyVault,
    LlmProvider provider
) : ILlmClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public AdapterType AdapterType => AdapterType.OpenAiCompatible;

    public async IAsyncEnumerable<ChatChunk> StreamChatAsync(
        ChatRequest request,
        [EnumeratorCancellation] CancellationToken ct = default
    )
    {
        var baseUrl = provider.BaseUrl.TrimEnd('/');

        var messages = new List<ChatMessage>(request.Messages);
        int insertIndex = 0;
        foreach (var block in request.CacheBlocks)
        {
            var prefix = ComputeCachePrefix(block);
            messages.Insert(
                insertIndex++,
                new ChatMessage(ChatRole.System, $"[{prefix}]{block.Content}")
            );
        }

        var body = new OpenAiChatRequest
        {
            Model = request.ModelId,
            Messages =
            [
                .. messages.Select(m => new OpenAiMessage
                {
                    Role = m.Role.ToString().ToLowerInvariant(),
                    Content = m.Content,
                }),
            ],
            Stream = true,
            MaxTokens = request.MaxOutputTokens,
            Temperature = request.Temperature,
            Tools =
                request.Tools.Count > 0
                    ?
                    [
                        .. request.Tools.Select(t => new OpenAiTool
                        {
                            Type = "function",
                            Function = new OpenAiFunction
                            {
                                Name = t.Name,
                                Description = t.Description,
                                Parameters = t.Parameters,
                            },
                        }),
                    ]
                    : null,
        };

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/chat/completions")
        {
            Content = JsonContent.Create(body, options: JsonOptions),
        };

        if (!string.IsNullOrWhiteSpace(provider.ApiKeyEncrypted))
        {
            var apiKey = keyVault.Decrypt(provider.ApiKeyEncrypted);
            httpRequest.Headers.Add("Authorization", $"Bearer {apiKey}");
        }

        using var response = await http.SendAsync(
            httpRequest,
            HttpCompletionOption.ResponseHeadersRead,
            ct
        );
        if (!response.IsSuccessStatusCode)
        {
            yield return new ChatChunk(null, ChatChunkFinishReason.Error, null);
            yield break;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        UsageSnapshot? usage = null;
        ChatChunkFinishReason? finishReason = null;

        string? line;
        while ((line = await reader.ReadLineAsync(ct)) is not null)
        {
            if (line == "")
                continue;
            if (!line.StartsWith("data: "))
                continue;

            var data = line["data: ".Length..].Trim();
            if (data == "[DONE]")
            {
                break;
            }

            OpenAiChunkEvent? chunkEvent = null;
            try
            {
                chunkEvent = JsonSerializer.Deserialize<OpenAiChunkEvent>(data, JsonOptions);
            }
            catch (JsonException)
            {
                continue;
            }

            if (chunkEvent is null)
                continue;

            var content = chunkEvent.Choices?.FirstOrDefault()?.Delta?.Content;
            if (content is not null)
            {
                yield return new ChatChunk(content, null, null);
            }

            if (chunkEvent.Usage is not null)
            {
                usage = new UsageSnapshot(
                    chunkEvent.Usage.PromptTokens,
                    chunkEvent.Usage.CompletionTokens,
                    chunkEvent.Usage.CachedTokens ?? 0
                );
            }

            if (chunkEvent.Choices?.FirstOrDefault()?.FinishReason is { } fr)
            {
                finishReason = fr.ToLowerInvariant() switch
                {
                    "stop" => ChatChunkFinishReason.Stop,
                    "length" => ChatChunkFinishReason.Length,
                    "content_filter" => ChatChunkFinishReason.ContentFilter,
                    "tool_calls" => ChatChunkFinishReason.ToolCalls,
                    _ => ChatChunkFinishReason.Stop,
                };
            }
        }

        yield return new ChatChunk(null, finishReason, usage);
    }

    public async Task<Result<IReadOnlyList<ProviderModelDto>>> GetModelsAsync(
        CancellationToken ct = default
    )
    {
        var baseUrl = provider.BaseUrl.TrimEnd('/');

        var httpRequest = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/models");

        if (!string.IsNullOrWhiteSpace(provider.ApiKeyEncrypted))
        {
            var apiKey = keyVault.Decrypt(provider.ApiKeyEncrypted);
            httpRequest.Headers.Add("Authorization", $"Bearer {apiKey}");
        }

        using var response = await http.SendAsync(httpRequest, ct);

        if (!response.IsSuccessStatusCode)
        {
            return Result<IReadOnlyList<ProviderModelDto>>.Failure(
                new Error(
                    "LLM_MODELS_FETCH_FAILED",
                    $"Failed to fetch models: {response.StatusCode}"
                )
            );
        }

        OpenAiModelsResponse? modelsResponse;
        try
        {
            modelsResponse = await response.Content.ReadFromJsonAsync<OpenAiModelsResponse>(
                JsonOptions,
                ct
            );
        }
        catch (JsonException ex)
        {
            return Result<IReadOnlyList<ProviderModelDto>>.Failure(
                new Error(
                    "LLM_MODELS_PARSE_FAILED",
                    $"Failed to parse models response: {ex.Message}"
                )
            );
        }

        if (modelsResponse?.Data is null)
        {
            return Result<IReadOnlyList<ProviderModelDto>>.Failure(
                new Error("LLM_MODELS_EMPTY", "Empty models response.")
            );
        }

        var models = modelsResponse
            .Data.Select(m => new ProviderModelDto(m.Id, m.Name ?? m.Id, m.Description, m.Metadata))
            .ToList();

        return Result<IReadOnlyList<ProviderModelDto>>.Success(models);
    }

    public bool SupportsToolCalling(ProviderModelConfigDto model) => true;

    private static string ComputeCachePrefix(CacheBlock block)
    {
        var hashInput = $"cache:{block.Type}:{block.Content}";
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(hashInput));
        var hash = Convert.ToHexString(hashBytes)[..16].ToLowerInvariant();
        return $"cache:{hash}";
    }

    private sealed class OpenAiChatRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = "";

        [JsonPropertyName("messages")]
        public List<OpenAiMessage> Messages { get; set; } = [];

        [JsonPropertyName("stream")]
        public bool Stream { get; set; }

        [JsonPropertyName("max_tokens")]
        public int? MaxTokens { get; set; }

        [JsonPropertyName("temperature")]
        public decimal? Temperature { get; set; }

        [JsonPropertyName("tools")]
        public List<OpenAiTool>? Tools { get; set; }
    }

    private sealed class OpenAiMessage
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = "";

        [JsonPropertyName("content")]
        public string Content { get; set; } = "";
    }

    private sealed class OpenAiTool
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "function";

        [JsonPropertyName("function")]
        public OpenAiFunction Function { get; set; } = null!;
    }

    private sealed class OpenAiFunction
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("description")]
        public string Description { get; set; } = "";

        [JsonPropertyName("parameters")]
        public IReadOnlyList<ToolParameter> Parameters { get; set; } = [];
    }

    private sealed class OpenAiChunkEvent
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("choices")]
        public List<OpenAiChoice>? Choices { get; set; }

        [JsonPropertyName("usage")]
        public OpenAiUsage? Usage { get; set; }
    }

    private sealed class OpenAiChoice
    {
        [JsonPropertyName("delta")]
        public OpenAiDelta? Delta { get; set; }

        [JsonPropertyName("finish_reason")]
        public string? FinishReason { get; set; }
    }

    private sealed class OpenAiDelta
    {
        [JsonPropertyName("content")]
        public string? Content { get; set; }
    }

    private sealed class OpenAiUsage
    {
        [JsonPropertyName("prompt_tokens")]
        public int PromptTokens { get; set; }

        [JsonPropertyName("completion_tokens")]
        public int CompletionTokens { get; set; }

        [JsonPropertyName("total_tokens")]
        public int TotalTokens { get; set; }

        [JsonPropertyName("cached_tokens")]
        public int? CachedTokens { get; set; }
    }

    private sealed class OpenAiModelsResponse
    {
        [JsonPropertyName("data")]
        public List<OpenAiModel> Data { get; set; } = [];
    }

    private sealed class OpenAiModel
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("metadata")]
        public Dictionary<string, string>? Metadata { get; set; }
    }
}
