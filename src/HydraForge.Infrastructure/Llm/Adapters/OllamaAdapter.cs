namespace HydraForge.Infrastructure.Llm.Adapters;

using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using HydraForge.Application.Llm;
using HydraForge.Domain.Common;
using HydraForge.Domain.Entities.Admin;
using HydraForge.Domain.Enums;
using Microsoft.Extensions.Logging;

public sealed class OllamaAdapter(
    HttpClient http,
    LlmProvider provider,
    ILogger<OllamaAdapter> logger
) : ILlmClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public AdapterType AdapterType => AdapterType.Ollama;

    public async IAsyncEnumerable<ChatChunk> StreamChatAsync(
        ChatRequest request,
        [EnumeratorCancellation] CancellationToken ct = default
    )
    {
        var baseUrl = provider.BaseUrl.TrimEnd('/');

        var body = new OllamaChatRequest
        {
            Model = request.ModelId,
            Messages =
            [
                .. request.Messages.Select(m => new OllamaMessage
                {
                    Role = m.Role.ToString().ToLowerInvariant(),
                    Content = m.Content,
                }),
            ],
            Stream = true,
            Options =
                request.Temperature is null && request.MaxOutputTokens is null
                    ? null
                    : new OllamaOptions
                    {
                        Temperature = request.Temperature,
                        NumPredict = request.MaxOutputTokens,
                    },
        };

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/api/chat")
        {
            Content = JsonContent.Create(body, options: JsonOptions),
        };

        using var response = await http.SendAsync(
            httpRequest,
            HttpCompletionOption.ResponseHeadersRead,
            ct
        );
        if (!response.IsSuccessStatusCode)
        {
            var statusCode = response.StatusCode;
            var responseBody = await response.Content.ReadAsStringAsync(ct);
            logger.LogError(
                "Ollama API error {StatusCode}: {ResponseBody}",
                statusCode,
                responseBody
            );
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

            OllamaChunkEvent? chunkEvent = null;
            try
            {
                chunkEvent = JsonSerializer.Deserialize<OllamaChunkEvent>(line, JsonOptions);
            }
            catch (JsonException)
            {
                continue;
            }

            if (chunkEvent is null)
                continue;

            var content = chunkEvent.Message?.Content;
            if (content is not null && content.Length > 0)
            {
                yield return new ChatChunk(content, null, null);
            }

            if (chunkEvent.Done)
            {
                finishReason = ChatChunkFinishReason.Stop;
                usage = new UsageSnapshot(
                    chunkEvent.PromptEvalCount ?? 0,
                    chunkEvent.EvalCount ?? 0,
                    0
                );
            }
        }

        yield return new ChatChunk(null, finishReason, usage);
    }

    public async Task<Result<IReadOnlyList<ProviderModelDto>>> GetModelsAsync(
        CancellationToken ct = default
    )
    {
        var baseUrl = provider.BaseUrl.TrimEnd('/');

        var httpRequest = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/api/tags");

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

        OllamaTagsResponse? tagsResponse;
        try
        {
            tagsResponse = await response.Content.ReadFromJsonAsync<OllamaTagsResponse>(
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

        if (tagsResponse?.Models is null)
        {
            return Result<IReadOnlyList<ProviderModelDto>>.Failure(
                new Error("LLM_MODELS_EMPTY", "Empty models response.")
            );
        }

        var models = tagsResponse
            .Models.Select(m => new ProviderModelDto(
                m.Name,
                m.Name,
                null,
                string.IsNullOrEmpty(m.ModifiedAt)
                    ? null
                    : new Dictionary<string, string> { ["modified_at"] = m.ModifiedAt }
            ))
            .ToList();

        return Result<IReadOnlyList<ProviderModelDto>>.Success(models);
    }

    public bool SupportsToolCalling(ProviderModelConfigDto model) => false;

    private sealed class OllamaChatRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = "";

        [JsonPropertyName("messages")]
        public List<OllamaMessage> Messages { get; set; } = [];

        [JsonPropertyName("stream")]
        public bool Stream { get; set; }

        [JsonPropertyName("options")]
        public OllamaOptions? Options { get; set; }
    }

    private sealed class OllamaMessage
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = "";

        [JsonPropertyName("content")]
        public string Content { get; set; } = "";
    }

    private sealed class OllamaOptions
    {
        [JsonPropertyName("temperature")]
        public decimal? Temperature { get; set; }

        [JsonPropertyName("num_predict")]
        public int? NumPredict { get; set; }
    }

    private sealed class OllamaChunkEvent
    {
        [JsonPropertyName("message")]
        public OllamaMessageContent? Message { get; set; }

        [JsonPropertyName("done")]
        public bool Done { get; set; }

        [JsonPropertyName("prompt_eval_count")]
        public int? PromptEvalCount { get; set; }

        [JsonPropertyName("eval_count")]
        public int? EvalCount { get; set; }
    }

    private sealed class OllamaMessageContent
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = "";

        [JsonPropertyName("content")]
        public string Content { get; set; } = "";
    }

    private sealed class OllamaTagsResponse
    {
        [JsonPropertyName("models")]
        public List<OllamaModel> Models { get; set; } = [];
    }

    private sealed class OllamaModel
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("modified_at")]
        public string? ModifiedAt { get; set; }
    }
}
