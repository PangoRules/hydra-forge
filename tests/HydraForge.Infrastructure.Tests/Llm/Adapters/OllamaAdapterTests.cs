namespace HydraForge.Infrastructure.Tests.Llm.Adapters;

using System.Net;
using System.Text;
using System.Text.Json;
using HydraForge.Application.Llm;
using HydraForge.Domain.Entities.Admin;
using HydraForge.Domain.Enums;
using HydraForge.Infrastructure.Llm.Adapters;
using Microsoft.Extensions.Logging;

public class OllamaAdapterTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private static LlmProvider CreateProvider(
        string baseUrl = "http://localhost:11434",
        string? apiKeyEncrypted = null
    )
    {
        return new LlmProvider
        {
            Id = Guid.NewGuid(),
            Name = "Test Ollama Provider",
            BaseUrl = baseUrl,
            ApiKeyEncrypted = apiKeyEncrypted ?? "",
            AdapterType = AdapterType.Ollama,
            ProviderType = ProviderType.Text,
            Tier = ModelTier.Standard,
            IsEnabled = true,
        };
    }

    private class FakeKeyVault(string? apiKey = null) : IKeyVault
    {
        public string Decrypt(string _) => apiKey ?? "decrypted-fake-key";

        public string Encrypt(string plaintext) => plaintext;
    }

    private class FakeLogger : ILogger<OllamaAdapter>
    {
        public LoggedError? Error { get; private set; }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter
        )
        {
            if (logLevel == LogLevel.Error)
                Error = new(formatter(state, exception));
        }

        public record LoggedError(string Message);
    }

    private static MemoryStream NdjsonStream(string ndjsonContent)
    {
        return new MemoryStream(Encoding.UTF8.GetBytes(ndjsonContent));
    }

    private class NdjsonStreamHandler(string ndjsonContent) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken ct
        )
        {
            LastRequest = request;
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(NdjsonStream(_ndjsonContent)),
            };
            response.Content.Headers.ContentType = new("application/x-ndjson");
            return Task.FromResult(response);
        }

        private readonly string _ndjsonContent = ndjsonContent;
    }

    private class JsonBodyHandler(HttpStatusCode statusCode, string jsonBody) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastBody { get; private set; }
        private readonly HttpStatusCode _statusCode = statusCode;
        private readonly string _jsonBody = jsonBody;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken ct
        )
        {
            LastRequest = request;
            if (request.Content is not null)
            {
                LastBody = request.Content.ReadAsStringAsync(ct).GetAwaiter().GetResult();
            }
            var response = new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_jsonBody, Encoding.UTF8, "application/json"),
            };
            return Task.FromResult(response);
        }
    }

    [Fact]
    public void AdapterType_ReturnsOllama()
    {
        var provider = CreateProvider();
        using var http = new HttpClient(new JsonBodyHandler(HttpStatusCode.OK, "{}"));
        var logger = new FakeLogger();
        var adapter = new OllamaAdapter(http, provider, logger);

        Assert.Equal(AdapterType.Ollama, adapter.AdapterType);
    }

    [Fact]
    public void SupportsToolCalling_ReturnsFalse()
    {
        var provider = CreateProvider();
        using var http = new HttpClient(new JsonBodyHandler(HttpStatusCode.OK, "{}"));
        var logger = new FakeLogger();
        var adapter = new OllamaAdapter(http, provider, logger);

        var result = adapter.SupportsToolCalling(
            new ProviderModelConfigDto(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "llama3.2",
                "Llama 3.2",
                "standard",
                null,
                null,
                true
            )
        );

        Assert.False(result);
    }

    [Fact]
    public async Task StreamChatAsync_SendsCorrectRequestShape()
    {
        var bodyHandler = new JsonBodyHandler(HttpStatusCode.OK, "");
        using var http = new HttpClient(bodyHandler);
        var provider = CreateProvider();
        var logger = new FakeLogger();
        var adapter = new OllamaAdapter(http, provider, logger);

        var request = new ChatRequest(
            Guid.NewGuid(),
            "llama3.2",
            [new ChatMessage(ChatRole.User, "Hello")],
            [],
            [],
            2048,
            0.7m
        );

        await foreach (var _ in adapter.StreamChatAsync(request)) { }

        Assert.NotNull(bodyHandler.LastBody);
        var doc = JsonDocument.Parse(bodyHandler.LastBody);
        Assert.Equal("llama3.2", doc.RootElement.GetProperty("model").GetString());
        Assert.True(doc.RootElement.GetProperty("stream").GetBoolean());
        Assert.Equal(
            2048,
            doc.RootElement.GetProperty("options").GetProperty("num_predict").GetInt32()
        );
        Assert.Equal(
            0.7m,
            doc.RootElement.GetProperty("options").GetProperty("temperature").GetDecimal()
        );
        Assert.Single(doc.RootElement.GetProperty("messages").EnumerateArray());
        Assert.Equal(
            "user",
            doc.RootElement.GetProperty("messages")[0].GetProperty("role").GetString()
        );
        Assert.Equal(
            "Hello",
            doc.RootElement.GetProperty("messages")[0].GetProperty("content").GetString()
        );
    }

    [Fact]
    public async Task StreamChatAsync_SendsCacheBlocksAsSystemMessages()
    {
        var bodyHandler = new JsonBodyHandler(HttpStatusCode.OK, "");
        using var http = new HttpClient(bodyHandler);
        var provider = CreateProvider();
        var logger = new FakeLogger();
        var adapter = new OllamaAdapter(http, provider, logger);

        var request = new ChatRequest(
            Guid.NewGuid(),
            "llama3.2",
            [new ChatMessage(ChatRole.User, "Hello")],
            [new CacheBlock("project snapshot content", CacheBlockType.ProjectSnapshot)],
            [],
            null,
            null
        );

        await foreach (var _ in adapter.StreamChatAsync(request)) { }

        Assert.NotNull(bodyHandler.LastBody);
        var doc = JsonDocument.Parse(bodyHandler.LastBody);
        var messages = doc.RootElement.GetProperty("messages").EnumerateArray().ToList();
        Assert.Equal(2, messages.Count);
        Assert.Equal("system", messages[0].GetProperty("role").GetString());
        Assert.Equal("project snapshot content", messages[0].GetProperty("content").GetString());
        Assert.Equal("user", messages[1].GetProperty("role").GetString());
    }

    [Fact]
    public async Task StreamChatAsync_YieldsContentDeltas_FromNdjsonLines()
    {
        var ndjson =
            "{\"model\":\"llama3.2\",\"created_at\":\"2024-01-01T00:00:00Z\",\"message\":{\"role\":\"assistant\",\"content\":\"Hello\"},\"done\":false}\n"
            + "{\"model\":\"llama3.2\",\"created_at\":\"2024-01-01T00:00:01Z\",\"message\":{\"role\":\"assistant\",\"content\":\"!\"},\"done\":false}\n"
            + "{\"model\":\"llama3.2\",\"created_at\":\"2024-01-01T00:00:02Z\",\"message\":{\"role\":\"assistant\",\"content\":\" world\"},\"done\":false}\n"
            + "{\"model\":\"llama3.2\",\"created_at\":\"2024-01-01T00:00:12Z\",\"message\":{\"role\":\"assistant\",\"content\":\"\"},\"done\":true,\"total_duration\":123456789,\"prompt_eval_count\":15,\"eval_count\":157}\n";

        using var http = new HttpClient(new NdjsonStreamHandler(ndjson));
        var provider = CreateProvider();
        var logger = new FakeLogger();
        var adapter = new OllamaAdapter(http, provider, logger);

        var request = new ChatRequest(
            Guid.NewGuid(),
            "llama3.2",
            [new ChatMessage(ChatRole.User, "Hi")],
            [],
            [],
            null,
            null
        );

        var chunks = new List<ChatChunk>();
        await foreach (var chunk in adapter.StreamChatAsync(request))
        {
            chunks.Add(chunk);
        }

        Assert.Equal(4, chunks.Count);
        Assert.Equal("Hello", chunks[0].Delta);
        Assert.Equal("!", chunks[1].Delta);
        Assert.Equal(" world", chunks[2].Delta);
        Assert.Null(chunks[3].Delta);
        Assert.Equal(ChatChunkFinishReason.Stop, chunks[3].FinishReason);
        Assert.NotNull(chunks[3].Usage);
        Assert.Equal(15, chunks[3].Usage!.InputTokens);
        Assert.Equal(157, chunks[3].Usage!.OutputTokens);
        Assert.Equal(0, chunks[3].Usage!.CachedTokens);
    }

    [Fact]
    public async Task StreamChatAsync_CachedTokens_AlwaysZero()
    {
        var ndjson =
            "{\"model\":\"llama3.2\",\"message\":{\"content\":\"Hi\"},\"done\":true,\"prompt_eval_count\":10,\"eval_count\":5}\n";

        using var http = new HttpClient(new NdjsonStreamHandler(ndjson));
        var provider = CreateProvider();
        var logger = new FakeLogger();
        var adapter = new OllamaAdapter(http, provider, logger);

        var request = new ChatRequest(
            Guid.NewGuid(),
            "llama3.2",
            [new ChatMessage(ChatRole.User, "Hi")],
            [],
            [],
            null,
            null
        );

        ChatChunk? lastChunk = null;
        await foreach (var chunk in adapter.StreamChatAsync(request))
        {
            lastChunk = chunk;
        }

        Assert.NotNull(lastChunk?.Usage);
        Assert.Equal(0, lastChunk.Usage.CachedTokens);
    }

    [Fact]
    public async Task StreamChatAsync_NonSuccessResponse_YieldsErrorChunk()
    {
        using var http = new HttpClient(
            new JsonBodyHandler(
                HttpStatusCode.ServiceUnavailable,
                "{\"error\":\"model not found\"}"
            )
        );
        var provider = CreateProvider();
        var logger = new FakeLogger();
        var adapter = new OllamaAdapter(http, provider, logger);

        var request = new ChatRequest(
            Guid.NewGuid(),
            "llama3.2",
            [new ChatMessage(ChatRole.User, "Hi")],
            [],
            [],
            null,
            null
        );

        var chunks = new List<ChatChunk>();
        await foreach (var chunk in adapter.StreamChatAsync(request))
        {
            chunks.Add(chunk);
        }

        Assert.Single(chunks);
        Assert.Equal(ChatChunkFinishReason.Error, chunks[0].FinishReason);
        Assert.NotNull(logger.Error);
        Assert.Contains("ServiceUnavailable", logger.Error.Message);
        Assert.Contains("model not found", logger.Error.Message);
    }

    [Fact]
    public async Task StreamChatAsync_NoTemperatureOrMaxTokens_OmitsOptions()
    {
        var bodyHandler = new JsonBodyHandler(HttpStatusCode.OK, "");
        using var http = new HttpClient(bodyHandler);
        var provider = CreateProvider();
        var logger = new FakeLogger();
        var adapter = new OllamaAdapter(http, provider, logger);

        var request = new ChatRequest(
            Guid.NewGuid(),
            "llama3.2",
            [new ChatMessage(ChatRole.User, "Hello")],
            [],
            [],
            null,
            null
        );

        await foreach (var _ in adapter.StreamChatAsync(request)) { }

        Assert.NotNull(bodyHandler.LastBody);
        var doc = JsonDocument.Parse(bodyHandler.LastBody);
        Assert.False(doc.RootElement.TryGetProperty("options", out _));
    }

    [Fact]
    public async Task GetModelsAsync_ParsesTagList()
    {
        var json = JsonSerializer.Serialize(
            new
            {
                models = new[]
                {
                    new
                    {
                        name = "llama3.2:latest",
                        modified_at = "2024-01-01T00:00:00Z",
                        size = 1234567890L,
                        digest = "sha256:abc123",
                    },
                    new
                    {
                        name = "mistral:latest",
                        modified_at = "2024-06-15T12:30:00Z",
                        size = 9876543210L,
                        digest = "sha256:def456",
                    },
                },
            },
            JsonOptions
        );

        using var http = new HttpClient(new JsonBodyHandler(HttpStatusCode.OK, json));
        var provider = CreateProvider();
        var logger = new FakeLogger();
        var adapter = new OllamaAdapter(http, provider, logger);

        var result = await adapter.GetModelsAsync();

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(2, result.Value.Count);
        Assert.Equal("llama3.2:latest", result.Value[0].ModelId);
        Assert.Equal("llama3.2:latest", result.Value[0].Name);
        Assert.Equal("2024-01-01T00:00:00Z", result.Value[0].Metadata!["modified_at"]);
        Assert.Equal("mistral:latest", result.Value[1].ModelId);
        Assert.Equal("mistral:latest", result.Value[1].Name);
        Assert.Equal("2024-06-15T12:30:00Z", result.Value[1].Metadata!["modified_at"]);
    }

    [Fact]
    public async Task GetModelsAsync_NonSuccessStatus_ReturnsFailure()
    {
        using var http = new HttpClient(new JsonBodyHandler(HttpStatusCode.ServiceUnavailable, ""));
        var provider = CreateProvider();
        var logger = new FakeLogger();
        var adapter = new OllamaAdapter(http, provider, logger);

        var result = await adapter.GetModelsAsync();

        Assert.True(result.IsFailure);
        Assert.Equal("LLM_MODELS_FETCH_FAILED", result.Error.Code);
    }

    [Fact]
    public async Task GetModelsAsync_InvalidJson_ReturnsFailure()
    {
        using var http = new HttpClient(new JsonBodyHandler(HttpStatusCode.OK, "not json {{{"));
        var provider = CreateProvider();
        var logger = new FakeLogger();
        var adapter = new OllamaAdapter(http, provider, logger);

        var result = await adapter.GetModelsAsync();

        Assert.True(result.IsFailure);
        Assert.Equal("LLM_MODELS_PARSE_FAILED", result.Error.Code);
    }

    [Fact]
    public async Task GetModelsAsync_MissingModelsField_ReturnsEmptyList()
    {
        using var http = new HttpClient(new JsonBodyHandler(HttpStatusCode.OK, "{}"));
        var provider = CreateProvider();
        var logger = new FakeLogger();
        var adapter = new OllamaAdapter(http, provider, logger);

        var result = await adapter.GetModelsAsync();

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Empty(result.Value);
    }
}
