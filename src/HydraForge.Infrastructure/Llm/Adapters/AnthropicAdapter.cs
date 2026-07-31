namespace HydraForge.Infrastructure.Llm.Adapters;

using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using HydraForge.Application.Llm;
using HydraForge.Domain.Common;
using HydraForge.Domain.Entities.Admin;
using HydraForge.Domain.Enums;
using Microsoft.Extensions.Logging;

public sealed class AnthropicAdapter(
    HttpClient http,
    IKeyVault keyVault,
    LlmProvider provider,
    ILogger<AnthropicAdapter> logger
) : ILlmClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public AdapterType AdapterType => AdapterType.Anthropic;

    public async IAsyncEnumerable<ChatChunk> StreamChatAsync(
        ChatRequest request,
        [EnumeratorCancellation] CancellationToken ct = default
    )
    {
        var baseUrl = provider.BaseUrl.TrimEnd('/');

        // System blocks: SystemContext → array with cache_control; Memory → array without cache_control
        var systemBlocks = new List<AnthropicSystemBlock>();

        var systemContextBlock = request.CacheBlocks.FirstOrDefault(b => b.Type == CacheBlockType.SystemContext);
        if (systemContextBlock is not null)
        {
            systemBlocks.Add(new AnthropicSystemBlock(systemContextBlock.Content, new AnthropicCacheControl()));
        }

        foreach (var block in request.CacheBlocks.Where(b => b.Type == CacheBlockType.Memory))
        {
            logger.LogDebug("Memory cache block attached to system array (no cache_control): {Content}", block.Content);
            systemBlocks.Add(new AnthropicSystemBlock(block.Content, null));
        }

        // Also include ChatRole.System messages in the system array (without cache_control)
        foreach (var msg in request.Messages.Where(m => m.Role == ChatRole.System))
        {
            systemBlocks.Add(new AnthropicSystemBlock(msg.Content, null));
        }

        var messages = new List<AnthropicMessage>(request.Messages.Count);
        var snapshotInjected = false;

        // Build messages — user messages in Anthropic format
        foreach (var msg in request.Messages.Where(m => m.Role != ChatRole.System))
        {
            var role = msg.Role switch
            {
                ChatRole.User => "user",
                ChatRole.Assistant => "assistant",
                _ => "user",
            };

            // Project snapshot cache blocks prepended to first user message only
            if (msg.Role == ChatRole.User && !snapshotInjected)
            {
                snapshotInjected = true;
                var snapshotBlocks = request.CacheBlocks.Where(b => b.Type == CacheBlockType.ProjectSnapshot).ToList();
                if (snapshotBlocks.Count > 0)
                {
                    var snapshotContent = string.Join("\n", snapshotBlocks.Select(b => b.Content));
                    messages.Add(new AnthropicMessage("user", $"[project_snapshot]\n{snapshotContent}\n\n{msg.Content}", true));
                    continue;
                }
            }

            messages.Add(new AnthropicMessage(role, msg.Content, false));
        }

        var body = new AnthropicChatRequest
        {
            Model = request.ModelId,
            MaxTokens = request.MaxOutputTokens ?? 4096,
            Messages = messages,
            Stream = true,
            System = systemBlocks.Count > 0 ? systemBlocks : null,
            Tools = request.Tools.Count > 0
                ? request.Tools.Select(t => AnthropicTool.FromDefinition(t)).ToList()
                : null,
        };

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/v1/messages")
        {
            Content = JsonContent.Create(body, options: JsonOptions),
        };

        if (!string.IsNullOrWhiteSpace(provider.ApiKeyEncrypted))
        {
            httpRequest.Headers.Add("x-api-key", keyVault.Decrypt(provider.ApiKeyEncrypted));
        }
        httpRequest.Headers.Add("anthropic-version", "2023-06-01");

        using var response = await http.SendAsync(
            httpRequest,
            HttpCompletionOption.ResponseHeadersRead,
            ct
        );

        if (!response.IsSuccessStatusCode)
        {
            var statusCode = response.StatusCode;
            var responseBody = await response.Content.ReadAsStringAsync(ct);
            logger.LogError("Anthropic API error {StatusCode}: {ResponseBody}", statusCode, responseBody);
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
                break;

            AnthropicChunkEvent? chunkEvent = null;
            try
            {
                chunkEvent = JsonSerializer.Deserialize<AnthropicChunkEvent>(data, JsonOptions);
            }
            catch (JsonException)
            {
                continue;
            }

            if (chunkEvent is null)
                continue;

            // content_block_delta — text delta
            if (chunkEvent.Delta is { } delta && delta.Text is { } text)
            {
                yield return new ChatChunk(text, null, null);
            }

            // Note: delta.partial_json (tool-use streaming) is not currently handled.
            // Anthropic streams tool_use content blocks as content_block_delta with type "input_json_delta".
            // Supporting this requires parsing those deltas and yielding ChatChunk with ToolCall delta,
            // which is outside the current plan scope.

            // message_delta — final usage + stop reason
            if (chunkEvent.MessageDelta is { } msgDelta)
            {
                if (msgDelta.Usage is { } msgUsage)
                {
                    usage = new UsageSnapshot(
                        msgUsage.InputTokens,
                        msgUsage.OutputTokens,
                        (msgUsage.CacheCreationInputTokens ?? 0) + (msgUsage.CacheReadInputTokens ?? 0)
                    );
                }

                if (msgDelta.StopReason is { } stopReason)
                {
                    finishReason = stopReason switch
                    {
                        "end_turn" => ChatChunkFinishReason.Stop,
                        "max_tokens" => ChatChunkFinishReason.Length,
                        "stop_sequence" => ChatChunkFinishReason.Stop,
                        "content_filtered" => ChatChunkFinishReason.ContentFilter,
                        _ => ChatChunkFinishReason.Stop,
                    };
                }
            }
        }

        yield return new ChatChunk(null, finishReason, usage);
    }

    public Task<Result<IReadOnlyList<ProviderModelDto>>> GetModelsAsync(CancellationToken ct = default)
    {
        logger.LogWarning("Anthropic adapter does not support listing models via API. Returning empty list.");
        return Task.FromResult(Result<IReadOnlyList<ProviderModelDto>>.Success(
            Array.Empty<ProviderModelDto>()));
    }

    public bool SupportsToolCalling(ProviderModelConfigDto model) => true;

    private sealed class AnthropicChatRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = "";

        [JsonPropertyName("max_tokens")]
        public int MaxTokens { get; set; }

        [JsonPropertyName("messages")]
        public List<AnthropicMessage> Messages { get; set; } = [];

        [JsonPropertyName("stream")]
        public bool Stream { get; set; }

        // System is an array of blocks when any system content exists, otherwise omitted
        [JsonPropertyName("system")]
        public List<AnthropicSystemBlock>? System { get; set; }

        [JsonPropertyName("tools")]
        public List<AnthropicTool>? Tools { get; set; }
    }

    private sealed class AnthropicSystemBlock
    {
        [JsonPropertyName("type")]
        public string Type { get; } = "text";

        [JsonPropertyName("text")]
        public string Text { get; set; } = "";

        [JsonPropertyName("cache_control")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public AnthropicCacheControl? CacheControl { get; set; }

        public AnthropicSystemBlock(string text, AnthropicCacheControl? cacheControl)
        {
            Text = text;
            CacheControl = cacheControl;
        }
    }

    private sealed class AnthropicMessage
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = "";

        [JsonPropertyName("content")]
        public string Content { get; set; } = "";

        [JsonPropertyName("cache_control")]
        public AnthropicCacheControl? CacheControl { get; set; }

        public AnthropicMessage(string role, string content, bool withCache)
        {
            Role = role;
            Content = content;
            if (withCache)
            {
                CacheControl = new AnthropicCacheControl();
            }
        }
    }

    private sealed class AnthropicCacheControl
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "ephemeral";
    }

    private sealed class AnthropicTool
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("description")]
        public string Description { get; set; } = "";

        [JsonPropertyName("input_schema")]
        public AnthropicInputSchema InputSchema { get; set; } = null!;

        public static AnthropicTool FromDefinition(ToolDefinition def)
        {
            return new AnthropicTool
            {
                Name = def.Name,
                Description = def.Description,
                InputSchema = AnthropicInputSchema.FromParameters(def.Parameters),
            };
        }
    }

    private sealed class AnthropicInputSchema
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "object";

        [JsonPropertyName("properties")]
        public Dictionary<string, AnthropicToolProperty> Properties { get; set; } = [];

        [JsonPropertyName("required")]
        public List<string> Required { get; set; } = [];

        public static AnthropicInputSchema FromParameters(IReadOnlyList<ToolParameter> parameters)
        {
            var schema = new AnthropicInputSchema();
            foreach (var param in parameters)
            {
                schema.Properties[param.Name] = new AnthropicToolProperty
                {
                    Type = param.Type,
                    Description = param.Description,
                };
                if (param.IsRequired)
                {
                    schema.Required.Add(param.Name);
                }
            }
            return schema;
        }
    }

    private sealed class AnthropicToolProperty
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "";

        [JsonPropertyName("description")]
        public string Description { get; set; } = "";
    }

    private sealed class AnthropicChunkEvent
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "";

        [JsonPropertyName("delta")]
        public AnthropicDelta? Delta { get; set; }

        [JsonPropertyName("message_delta")]
        public AnthropicMessageDelta? MessageDelta { get; set; }
    }

    private sealed class AnthropicDelta
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "";

        [JsonPropertyName("text")]
        public string? Text { get; set; }
    }

    private sealed class AnthropicMessageDelta
    {
        [JsonPropertyName("usage")]
        public AnthropicUsage? Usage { get; set; }

        [JsonPropertyName("stop_reason")]
        public string? StopReason { get; set; }
    }

    private sealed class AnthropicUsage
    {
        [JsonPropertyName("input_tokens")]
        public int InputTokens { get; set; }

        [JsonPropertyName("output_tokens")]
        public int OutputTokens { get; set; }

        [JsonPropertyName("cache_creation_input_tokens")]
        public int? CacheCreationInputTokens { get; set; }

        [JsonPropertyName("cache_read_input_tokens")]
        public int? CacheReadInputTokens { get; set; }
    }
}
