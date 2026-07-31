namespace HydraForge.Infrastructure.Tests.Llm.Adapters;

using System.Net;
using System.Text;
using System.Text.Json;
using HydraForge.Application.Llm;
using HydraForge.Domain.Entities.Admin;
using HydraForge.Domain.Enums;
using HydraForge.Infrastructure.Llm.Adapters;
using Microsoft.Extensions.Logging;

public class AnthropicAdapterTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private static LlmProvider CreateProvider(
        string baseUrl = "https://api.anthropic.com",
        string? apiKeyEncrypted = null
    )
    {
        return new LlmProvider
        {
            Id = Guid.NewGuid(),
            Name = "Anthropic Test Provider",
            BaseUrl = baseUrl,
            ApiKeyEncrypted = apiKeyEncrypted ?? "",
            AdapterType = AdapterType.Anthropic,
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

    private class FakeLogger : ILogger<AnthropicAdapter>
    {
        public LoggedWarning? Warning { get; private set; }
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Warning)
                Warning = new(formatter(state, exception));
        }
        public record LoggedWarning(string Message);
    }

    private static MemoryStream SseStream(string sseContent)
    {
        return new MemoryStream(Encoding.UTF8.GetBytes(sseContent));
    }

    private class SseStreamHandler(string sseContent) : HttpMessageHandler
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
                Content = new StreamContent(SseStream(sseContent)),
            };
            response.Content.Headers.ContentType = new("text/plain");
            return Task.FromResult(response);
        }
    }

    private class JsonBodyHandler(HttpStatusCode statusCode, string jsonBody) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastBody { get; private set; }

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
            var response = new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(jsonBody, Encoding.UTF8, "application/json"),
            };
            return Task.FromResult(response);
        }
    }

    [Fact]
    public void AdapterType_ReturnsAnthropic()
    {
        var provider = CreateProvider();
        using var http = new HttpClient(new JsonBodyHandler(HttpStatusCode.OK, "{}"));
        var logger = new FakeLogger();
        var adapter = new AnthropicAdapter(http, new FakeKeyVault(), provider, logger);

        Assert.Equal(AdapterType.Anthropic, adapter.AdapterType);
    }

    [Fact]
    public void SupportsToolCalling_ReturnsTrue()
    {
        var provider = CreateProvider();
        using var http = new HttpClient(new JsonBodyHandler(HttpStatusCode.OK, "{}"));
        var logger = new FakeLogger();
        var adapter = new AnthropicAdapter(http, new FakeKeyVault(), provider, logger);

        var result = adapter.SupportsToolCalling(
            new ProviderModelConfigDto(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "claude-sonnet-4-20250514",
                "Claude Sonnet 4",
                "standard",
                null,
                null,
                true
            )
        );

        Assert.True(result);
    }

    [Fact]
    public async Task GetModelsAsync_ReturnsEmptyList_WithWarning()
    {
        var provider = CreateProvider();
        using var http = new HttpClient(new JsonBodyHandler(HttpStatusCode.OK, "{}"));
        var logger = new FakeLogger();
        var adapter = new AnthropicAdapter(http, new FakeKeyVault(), provider, logger);

        var result = await adapter.GetModelsAsync();

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Empty(result.Value);
        Assert.NotNull(logger.Warning);
        Assert.Contains("does not support listing models", logger.Warning.Message);
    }

    [Fact]
    public async Task StreamChatAsync_SendsCorrectRequestShape()
    {
        var bodyHandler = new JsonBodyHandler(HttpStatusCode.OK, "data: [DONE]\n\n");
        using var http = new HttpClient(bodyHandler);
        var provider = CreateProvider();
        var logger = new FakeLogger();
        var adapter = new AnthropicAdapter(http, new FakeKeyVault("test-key"), provider, logger);

        var request = new ChatRequest(
            Guid.NewGuid(),
            "claude-sonnet-4-20250514",
            [new ChatMessage(ChatRole.User, "Hello")],
            [],
            [],
            1024,
            0.7m
        );

        await foreach (var _ in adapter.StreamChatAsync(request)) { }

        Assert.NotNull(bodyHandler.LastRequest);
        Assert.Equal("https://api.anthropic.com/v1/messages", bodyHandler.LastRequest.RequestUri?.ToString());
        Assert.Equal("test-key", bodyHandler.LastRequest.Headers.GetValues("x-api-key").First());
        Assert.Equal("2023-06-01", bodyHandler.LastRequest.Headers.GetValues("anthropic-version").First());

        Assert.NotNull(bodyHandler.LastBody);
        var doc = JsonDocument.Parse(bodyHandler.LastBody);
        Assert.Equal("claude-sonnet-4-20250514", doc.RootElement.GetProperty("model").GetString());
        Assert.True(doc.RootElement.GetProperty("stream").GetBoolean());
        Assert.Equal(1024, doc.RootElement.GetProperty("max_tokens").GetInt32());
        Assert.Single(doc.RootElement.GetProperty("messages").EnumerateArray());
        Assert.Equal(
            "user",
            doc.RootElement.GetProperty("messages")[0].GetProperty("role").GetString()
        );
        Assert.Equal(
            "Hello",
            doc.RootElement.GetProperty("messages")[0].GetProperty("content").GetString()
        );
        Assert.False(doc.RootElement.TryGetProperty("system", out _));
    }

    [Fact]
    public async Task StreamChatAsync_SystemContextCacheBlock_PutsInSystemFieldWithCacheControl()
    {
        var bodyHandler = new JsonBodyHandler(HttpStatusCode.OK, "data: [DONE]\n\n");
        using var http = new HttpClient(bodyHandler);
        var provider = CreateProvider();
        var logger = new FakeLogger();
        var adapter = new AnthropicAdapter(http, new FakeKeyVault(), provider, logger);

        var request = new ChatRequest(
            Guid.NewGuid(),
            "claude-sonnet-4-20250514",
            [new ChatMessage(ChatRole.User, "Hello")],
            [new CacheBlock("system context content", CacheBlockType.SystemContext)],
            [],
            null,
            null
        );

        await foreach (var _ in adapter.StreamChatAsync(request)) { }

        Assert.NotNull(bodyHandler.LastBody);
        var doc = JsonDocument.Parse(bodyHandler.LastBody);
        Assert.True(doc.RootElement.TryGetProperty("system", out var systemEl));
        Assert.Equal("system context content", systemEl.GetString());
        // System block should NOT appear in messages array
        Assert.Single(doc.RootElement.GetProperty("messages").EnumerateArray());
    }

    [Fact]
    public async Task StreamChatAsync_ProjectSnapshotCacheBlock_PrependedToFirstUserMessageWithCacheControl()
    {
        var bodyHandler = new JsonBodyHandler(HttpStatusCode.OK, "data: [DONE]\n\n");
        using var http = new HttpClient(bodyHandler);
        var provider = CreateProvider();
        var logger = new FakeLogger();
        var adapter = new AnthropicAdapter(http, new FakeKeyVault(), provider, logger);

        var request = new ChatRequest(
            Guid.NewGuid(),
            "claude-sonnet-4-20250514",
            [new ChatMessage(ChatRole.User, "What is the status?")],
            [new CacheBlock("snapshot content here", CacheBlockType.ProjectSnapshot)],
            [],
            null,
            null
        );

        await foreach (var _ in adapter.StreamChatAsync(request)) { }

        Assert.NotNull(bodyHandler.LastBody);
        var doc = JsonDocument.Parse(bodyHandler.LastBody);
        var messages = doc.RootElement.GetProperty("messages").EnumerateArray().ToList();
        Assert.Single(messages);
        Assert.Equal("user", messages[0].GetProperty("role").GetString());
        Assert.Contains("[project_snapshot]", messages[0].GetProperty("content").GetString());
        Assert.Contains("snapshot content here", messages[0].GetProperty("content").GetString());
        Assert.True(messages[0].TryGetProperty("cache_control", out _));
    }

    [Fact]
    public async Task StreamChatAsync_YieldsContentDeltas_FromContentBlockDelta()
    {
        var sse =
            "data: {\"type\":\"content_block_delta\",\"delta\":{\"type\":\"text_delta\",\"text\":\"Hello\"}}\n\n"
            + "data: {\"type\":\"content_block_delta\",\"delta\":{\"type\":\"text_delta\",\"text\":\" world\"}}\n\n"
            + "data: {\"type\":\"message_delta\",\"message_delta\":{\"usage\":{\"input_tokens\":10,\"output_tokens\":2,\"cache_creation_input_tokens\":0,\"cache_read_input_tokens\":0},\"stop_reason\":\"end_turn\"}}\n\n"
            + "data: [DONE]\n\n";

        using var http = new HttpClient(new SseStreamHandler(sse));
        var provider = CreateProvider();
        var logger = new FakeLogger();
        var adapter = new AnthropicAdapter(http, new FakeKeyVault(), provider, logger);

        var request = new ChatRequest(
            Guid.NewGuid(),
            "claude-sonnet-4-20250514",
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

        Assert.Equal(3, chunks.Count);
        Assert.Equal("Hello", chunks[0].Delta);
        Assert.Equal(" world", chunks[1].Delta);
        Assert.Null(chunks[2].Delta);
        Assert.Equal(ChatChunkFinishReason.Stop, chunks[2].FinishReason);
        Assert.NotNull(chunks[2].Usage);
        Assert.Equal(10, chunks[2].Usage!.InputTokens);
        Assert.Equal(2, chunks[2].Usage!.OutputTokens);
        Assert.Equal(0, chunks[2].Usage!.CachedTokens);
    }

    [Fact]
    public async Task StreamChatAsync_CachedTokens_MappedFromCacheCreationAndCacheRead()
    {
        var sse =
            "data: {\"type\":\"content_block_delta\",\"delta\":{\"type\":\"text_delta\",\"text\":\"Hi\"}}\n\n"
            + "data: {\"type\":\"message_delta\",\"message_delta\":{\"usage\":{\"input_tokens\":10,\"output_tokens\":1,\"cache_creation_input_tokens\":5,\"cache_read_input_tokens\":3},\"stop_reason\":\"end_turn\"}}\n\n"
            + "data: [DONE]\n\n";

        using var http = new HttpClient(new SseStreamHandler(sse));
        var provider = CreateProvider();
        var logger = new FakeLogger();
        var adapter = new AnthropicAdapter(http, new FakeKeyVault(), provider, logger);

        var request = new ChatRequest(
            Guid.NewGuid(),
            "claude-sonnet-4-20250514",
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
        Assert.Equal(5 + 3, lastChunk.Usage.CachedTokens);
    }

    [Fact]
    public async Task StreamChatAsync_StopReason_MaxTokens_MapsToLength()
    {
        var sse =
            "data: {\"type\":\"content_block_delta\",\"delta\":{\"type\":\"text_delta\",\"text\":\"Hi\"}}\n\n"
            + "data: {\"type\":\"message_delta\",\"message_delta\":{\"usage\":{\"input_tokens\":10,\"output_tokens\":4096,\"cache_creation_input_tokens\":0,\"cache_read_input_tokens\":0},\"stop_reason\":\"max_tokens\"}}\n\n"
            + "data: [DONE]\n\n";

        using var http = new HttpClient(new SseStreamHandler(sse));
        var provider = CreateProvider();
        var logger = new FakeLogger();
        var adapter = new AnthropicAdapter(http, new FakeKeyVault(), provider, logger);

        var request = new ChatRequest(
            Guid.NewGuid(),
            "claude-sonnet-4-20250514",
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

        Assert.Equal(ChatChunkFinishReason.Length, lastChunk?.FinishReason);
    }

    [Fact]
    public async Task StreamChatAsync_ErrorStatus_YieldsErrorChunk()
    {
        using var http = new HttpClient(new JsonBodyHandler(HttpStatusCode.InternalServerError, ""));
        var provider = CreateProvider();
        var logger = new FakeLogger();
        var adapter = new AnthropicAdapter(http, new FakeKeyVault(), provider, logger);

        var request = new ChatRequest(
            Guid.NewGuid(),
            "claude-sonnet-4-20250514",
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
    }

    [Fact]
    public async Task StreamChatAsync_ApiKeyDecryptedAndSent()
    {
        var bodyHandler = new JsonBodyHandler(HttpStatusCode.OK, "data: [DONE]\n\n");
        using var http = new HttpClient(bodyHandler);
        var provider = CreateProvider("https://api.anthropic.com", "encrypted-cipher-text");
        var logger = new FakeLogger();
        var adapter = new AnthropicAdapter(http, new FakeKeyVault("decrypted-key"), provider, logger);

        var request = new ChatRequest(
            Guid.NewGuid(),
            "claude-sonnet-4-20250514",
            [new ChatMessage(ChatRole.User, "Hi")],
            [],
            [],
            null,
            null
        );

        await foreach (var _ in adapter.StreamChatAsync(request)) { }

        Assert.NotNull(bodyHandler.LastRequest);
        Assert.Equal("decrypted-key", bodyHandler.LastRequest.Headers.GetValues("x-api-key").First());
    }

    [Fact]
    public async Task StreamChatAsync_EmptyApiKey_StillSendsDecryptedValue()
    {
        // FakeKeyVault returns "decrypted-fake-key" for any input including empty string.
        // This tests that when ApiKeyEncrypted is empty, we still call keyVault.Decrypt.
        var bodyHandler = new JsonBodyHandler(HttpStatusCode.OK, "data: [DONE]\n\n");
        using var http = new HttpClient(bodyHandler);
        var provider = CreateProvider("https://api.anthropic.com", "");
        var logger = new FakeLogger();
        var adapter = new AnthropicAdapter(http, new FakeKeyVault(), provider, logger);

        var request = new ChatRequest(
            Guid.NewGuid(),
            "claude-sonnet-4-20250514",
            [new ChatMessage(ChatRole.User, "Hi")],
            [],
            [],
            null,
            null
        );

        await foreach (var _ in adapter.StreamChatAsync(request)) { }

        Assert.NotNull(bodyHandler.LastRequest);
        // FakeKeyVault returns "decrypted-fake-key" for any input
        Assert.Equal("decrypted-fake-key", bodyHandler.LastRequest.Headers.GetValues("x-api-key").First());
    }

    [Fact]
    public async Task StreamChatAsync_SystemCacheBlock_GoesToSystemField_NotMessages()
    {
        var bodyHandler = new JsonBodyHandler(HttpStatusCode.OK, "data: [DONE]\n\n");
        using var http = new HttpClient(bodyHandler);
        var provider = CreateProvider();
        var logger = new FakeLogger();
        var adapter = new AnthropicAdapter(http, new FakeKeyVault(), provider, logger);

        var request = new ChatRequest(
            Guid.NewGuid(),
            "claude-sonnet-4-20250514",
            [new ChatMessage(ChatRole.User, "Hello")],
            [
                new CacheBlock("system prompt", CacheBlockType.SystemContext),
                new CacheBlock("snapshot", CacheBlockType.ProjectSnapshot)
            ],
            [],
            null,
            null
        );

        await foreach (var _ in adapter.StreamChatAsync(request)) { }

        Assert.NotNull(bodyHandler.LastBody);
        var doc = JsonDocument.Parse(bodyHandler.LastBody);
        Assert.True(doc.RootElement.TryGetProperty("system", out var systemEl));
        Assert.Equal("system prompt", systemEl.GetString());
        Assert.Single(doc.RootElement.GetProperty("messages").EnumerateArray());
    }
}
