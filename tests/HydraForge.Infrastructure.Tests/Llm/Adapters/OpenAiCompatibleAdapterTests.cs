namespace HydraForge.Infrastructure.Tests.Llm.Adapters;

using System.Net;
using System.Text;
using System.Text.Json;
using HydraForge.Application.Llm;
using HydraForge.Domain.Entities.Admin;
using HydraForge.Domain.Enums;
using HydraForge.Infrastructure.Llm.Adapters;

public class OpenAiCompatibleAdapterTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private static LlmProvider CreateProvider(
        string baseUrl = "https://api.example.com",
        string? apiKeyEncrypted = null
    )
    {
        return new LlmProvider
        {
            Id = Guid.NewGuid(),
            Name = "Test Provider",
            BaseUrl = baseUrl,
            ApiKeyEncrypted = apiKeyEncrypted ?? "",
            AdapterType = AdapterType.OpenAiCompatible,
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

    private static MemoryStream SseStream(string sseContent)
    {
        return new MemoryStream(Encoding.UTF8.GetBytes(sseContent));
    }

    private class SseStreamHandler(string sseContent) : HttpMessageHandler
    {
        private readonly string _sseContent = sseContent;

        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken ct
        )
        {
            LastRequest = request;
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(SseStream(_sseContent)),
            };
            response.Content.Headers.ContentType = new("text/plain");
            return Task.FromResult(response);
        }
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
    public void AdapterType_ReturnsOpenAiCompatible()
    {
        var provider = CreateProvider();
        using var http = new HttpClient(new JsonBodyHandler(HttpStatusCode.OK, "{}"));
        var adapter = new OpenAiCompatibleAdapter(http, new FakeKeyVault(), provider);

        Assert.Equal(AdapterType.OpenAiCompatible, adapter.AdapterType);
    }

    [Fact]
    public void SupportsToolCalling_ReturnsTrue()
    {
        var provider = CreateProvider();
        using var http = new HttpClient(new JsonBodyHandler(HttpStatusCode.OK, "{}"));
        var adapter = new OpenAiCompatibleAdapter(http, new FakeKeyVault(), provider);

        var result = adapter.SupportsToolCalling(
            new ProviderModelConfigDto(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "gpt-4o",
                "GPT-4o",
                "standard",
                null,
                null,
                true
            )
        );

        Assert.True(result);
    }

    [Fact]
    public async Task StreamChatAsync_SendsCorrectRequestShape()
    {
        var bodyHandler = new JsonBodyHandler(HttpStatusCode.OK, "data: [DONE]\n\n");
        using var http = new HttpClient(bodyHandler);
        var provider = CreateProvider();
        var adapter = new OpenAiCompatibleAdapter(http, new FakeKeyVault("test-key"), provider);

        var request = new ChatRequest(
            Guid.NewGuid(),
            "gpt-4o",
            [new ChatMessage(ChatRole.User, "Hello")],
            [],
            [],
            1024,
            0.7m
        );

        await foreach (var _ in adapter.StreamChatAsync(request)) { }

        Assert.NotNull(bodyHandler.LastBody);
        var doc = JsonDocument.Parse(bodyHandler.LastBody);
        Assert.Equal("gpt-4o", doc.RootElement.GetProperty("model").GetString());
        Assert.True(doc.RootElement.GetProperty("stream").GetBoolean());
        Assert.Equal(1024, doc.RootElement.GetProperty("max_tokens").GetInt32());
        Assert.Equal(0.7m, doc.RootElement.GetProperty("temperature").GetDecimal());
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
    public async Task StreamChatAsync_SendsCachedBlocksAsSystemMessages()
    {
        var bodyHandler = new JsonBodyHandler(HttpStatusCode.OK, "data: [DONE]\n\n");
        using var http = new HttpClient(bodyHandler);
        var provider = CreateProvider();
        var adapter = new OpenAiCompatibleAdapter(http, new FakeKeyVault(), provider);

        var request = new ChatRequest(
            Guid.NewGuid(),
            "gpt-4o",
            [new ChatMessage(ChatRole.User, "Hello")],
            [new CacheBlock("system context", CacheBlockType.SystemContext)],
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
        var content = messages[0].GetProperty("content").GetString();
        Assert.StartsWith("[cache:", content);
        Assert.Contains("system context", content);
    }

    [Fact]
    public async Task StreamChatAsync_CacheBlockPrefix_IsStableAcrossCalls()
    {
        var bodyHandler1 = new JsonBodyHandler(HttpStatusCode.OK, "data: [DONE]\n\n");
        using var http1 = new HttpClient(bodyHandler1);
        var provider = CreateProvider();
        var adapter1 = new OpenAiCompatibleAdapter(http1, new FakeKeyVault(), provider);

        var request1 = new ChatRequest(
            Guid.NewGuid(),
            "gpt-4o",
            [new ChatMessage(ChatRole.User, "Hello")],
            [new CacheBlock("identical content", CacheBlockType.SystemContext)],
            [],
            null,
            null
        );

        await foreach (var _ in adapter1.StreamChatAsync(request1)) { }

        var bodyHandler2 = new JsonBodyHandler(HttpStatusCode.OK, "data: [DONE]\n\n");
        using var http2 = new HttpClient(bodyHandler2);
        var adapter2 = new OpenAiCompatibleAdapter(http2, new FakeKeyVault(), provider);

        var request2 = new ChatRequest(
            Guid.NewGuid(),
            "gpt-4o",
            [new ChatMessage(ChatRole.User, "Hello")],
            [new CacheBlock("identical content", CacheBlockType.SystemContext)],
            [],
            null,
            null
        );

        await foreach (var _ in adapter2.StreamChatAsync(request2)) { }

        var content1 = JsonDocument
            .Parse(bodyHandler1.LastBody!)
            .RootElement.GetProperty("messages")[0]
            .GetProperty("content")
            .GetString();
        var content2 = JsonDocument
            .Parse(bodyHandler2.LastBody!)
            .RootElement.GetProperty("messages")[0]
            .GetProperty("content")
            .GetString();
        Assert.Equal(content1, content2);
    }

    [Fact]
    public async Task StreamChatAsync_YieldsContentDeltas()
    {
        var sse =
            "data: {\"id\":\"1\",\"choices\":[{\"delta\":{\"content\":\"Hello\"}}]}\n\n"
            + "data: {\"id\":\"1\",\"choices\":[{\"delta\":{\"content\":\" world\"}}]}\n\n"
            + "data: {\"id\":\"1\",\"choices\":[{\"finish_reason\":\"stop\"}],\"usage\":{\"prompt_tokens\":10,\"completion_tokens\":2,\"total_tokens\":12}}\n\n"
            + "data: [DONE]\n\n";

        using var http = new HttpClient(new SseStreamHandler(sse));
        var provider = CreateProvider();
        var adapter = new OpenAiCompatibleAdapter(http, new FakeKeyVault(), provider);

        var request = new ChatRequest(
            Guid.NewGuid(),
            "gpt-4o",
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
    public async Task StreamChatAsync_WithCachedTokens_SetsCachedTokens()
    {
        var sse =
            "data: {\"id\":\"1\",\"choices\":[{\"delta\":{\"content\":\"Hi\"}}],\"usage\":{\"prompt_tokens\":10,\"completion_tokens\":1,\"total_tokens\":11,\"cached_tokens\":5}}\n\n"
            + "data: [DONE]\n\n";

        using var http = new HttpClient(new SseStreamHandler(sse));
        var provider = CreateProvider();
        var adapter = new OpenAiCompatibleAdapter(http, new FakeKeyVault(), provider);

        var request = new ChatRequest(
            Guid.NewGuid(),
            "gpt-4o",
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
        Assert.Equal(5, lastChunk.Usage.CachedTokens);
    }

    [Fact]
    public async Task StreamChatAsync_EmptyCacheBlocks_NoExtraMessages()
    {
        var bodyHandler = new JsonBodyHandler(HttpStatusCode.OK, "data: [DONE]\n\n");
        using var http = new HttpClient(bodyHandler);
        var provider = CreateProvider();
        var adapter = new OpenAiCompatibleAdapter(http, new FakeKeyVault(), provider);

        var request = new ChatRequest(
            Guid.NewGuid(),
            "gpt-4o",
            [new ChatMessage(ChatRole.User, "Hello")],
            [],
            [],
            null,
            null
        );

        await foreach (var _ in adapter.StreamChatAsync(request)) { }

        Assert.NotNull(bodyHandler.LastBody);
        var doc = JsonDocument.Parse(bodyHandler.LastBody);
        Assert.Single(doc.RootElement.GetProperty("messages").EnumerateArray());
    }

    [Fact]
    public async Task StreamChatAsync_ApiKeyDecryptedAndSent()
    {
        var authHandler = new AuthCaptureHandler("data: [DONE]\n\n");
        using var http = new HttpClient(authHandler);
        var provider = CreateProvider("https://api.example.com", "encrypted-cipher-text");
        var adapter = new OpenAiCompatibleAdapter(
            http,
            new FakeKeyVault("decrypted-key"),
            provider
        );

        var request = new ChatRequest(
            Guid.NewGuid(),
            "gpt-4o",
            [new ChatMessage(ChatRole.User, "Hi")],
            [],
            [],
            null,
            null
        );

        await foreach (var _ in adapter.StreamChatAsync(request)) { }

        Assert.Equal("Bearer decrypted-key", authHandler.CapturedAuth);
    }

    [Fact]
    public async Task StreamChatAsync_NoApiKey_NoAuthHeader()
    {
        var authHandler = new AuthCaptureHandler("data: [DONE]\n\n");
        using var http = new HttpClient(authHandler);
        var provider = CreateProvider("https://api.example.com", null);
        var adapter = new OpenAiCompatibleAdapter(http, new FakeKeyVault(), provider);

        var request = new ChatRequest(
            Guid.NewGuid(),
            "gpt-4o",
            [new ChatMessage(ChatRole.User, "Hi")],
            [],
            [],
            null,
            null
        );

        await foreach (var _ in adapter.StreamChatAsync(request)) { }

        Assert.Null(authHandler.CapturedAuth);
    }

    [Fact]
    public async Task GetModelsAsync_ParsesModelList()
    {
        var json = JsonSerializer.Serialize(
            new
            {
                data = new[]
                {
                    new
                    {
                        id = "gpt-4o",
                        name = "GPT-4o",
                        description = "Fast model",
                    },
                    new
                    {
                        id = "gpt-4o-mini",
                        name = "GPT-4o Mini",
                        description = "Mini model",
                    },
                },
            },
            JsonOptions
        );

        using var http = new HttpClient(new JsonBodyHandler(HttpStatusCode.OK, json));
        var provider = CreateProvider();
        var adapter = new OpenAiCompatibleAdapter(http, new FakeKeyVault(), provider);

        var result = await adapter.GetModelsAsync();

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(2, result.Value.Count);
        Assert.Equal("gpt-4o", result.Value[0].ModelId);
        Assert.Equal("GPT-4o", result.Value[0].Name);
        Assert.Equal("Fast model", result.Value[0].Description);
        Assert.Equal("gpt-4o-mini", result.Value[1].ModelId);
        Assert.Equal("GPT-4o Mini", result.Value[1].Name);
        Assert.Equal("Mini model", result.Value[1].Description);
    }

    [Fact]
    public async Task GetModelsAsync_NonSuccessStatus_ReturnsFailure()
    {
        using var http = new HttpClient(new JsonBodyHandler(HttpStatusCode.ServiceUnavailable, ""));
        var provider = CreateProvider();
        var adapter = new OpenAiCompatibleAdapter(http, new FakeKeyVault(), provider);

        var result = await adapter.GetModelsAsync();

        Assert.True(result.IsFailure);
        Assert.Equal("LLM_MODELS_FETCH_FAILED", result.Error.Code);
    }

    [Fact]
    public async Task GetModelsAsync_InvalidJson_ReturnsFailure()
    {
        using var http = new HttpClient(new JsonBodyHandler(HttpStatusCode.OK, "not json {{{"));
        var provider = CreateProvider();
        var adapter = new OpenAiCompatibleAdapter(http, new FakeKeyVault(), provider);

        var result = await adapter.GetModelsAsync();

        Assert.True(result.IsFailure);
        Assert.Equal("LLM_MODELS_PARSE_FAILED", result.Error.Code);
    }

    [Fact]
    public async Task GetModelsAsync_MissingDataField_ReturnsEmptyList()
    {
        using var http = new HttpClient(new JsonBodyHandler(HttpStatusCode.OK, "{}"));
        var provider = CreateProvider();
        var adapter = new OpenAiCompatibleAdapter(http, new FakeKeyVault(), provider);

        var result = await adapter.GetModelsAsync();

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Empty(result.Value);
    }

    [Fact]
    public async Task GetModelsAsync_ApiKeyDecryptedAndSent()
    {
        var authHandler = new AuthCaptureHandler("{\"data\":[]}");
        using var http = new HttpClient(authHandler);
        var provider = CreateProvider("https://api.example.com", "encrypted-cipher");
        var adapter = new OpenAiCompatibleAdapter(
            http,
            new FakeKeyVault("decrypted-api-key"),
            provider
        );

        await adapter.GetModelsAsync();

        Assert.Equal("Bearer decrypted-api-key", authHandler.CapturedAuth);
    }

    private class AuthCaptureHandler(string jsonBody) : HttpMessageHandler
    {
        public string? CapturedAuth { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken ct
        )
        {
            CapturedAuth = request.Headers.Authorization?.ToString();
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(jsonBody, Encoding.UTF8, "application/json"),
            };
            return Task.FromResult(response);
        }
    }
}
