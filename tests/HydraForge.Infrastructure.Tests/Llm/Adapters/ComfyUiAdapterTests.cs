namespace HydraForge.Infrastructure.Tests.Llm.Adapters;

using System.Net;
using System.Text;
using System.Text.Json;
using HydraForge.Application.Llm;
using HydraForge.Domain.Entities.Admin;
using HydraForge.Domain.Enums;
using HydraForge.Infrastructure.Llm.Adapters;
using Microsoft.Extensions.Logging;

public class ComfyUiAdapterTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private static LlmProvider CreateProvider(
        string baseUrl = "https://comfy.local",
        string? apiKeyEncrypted = null,
        AdapterType adapterType = AdapterType.ComfyUi
    )
    {
        return new LlmProvider
        {
            Id = Guid.NewGuid(),
            Name = "ComfyUI",
            BaseUrl = baseUrl,
            ApiKeyEncrypted = apiKeyEncrypted ?? "",
            AdapterType = adapterType,
            ProviderType = ProviderType.Image,
            Tier = ModelTier.Standard,
            IsEnabled = true,
        };
    }

    private class FakeKeyVault(string? apiKey = null) : IKeyVault
    {
        public string Decrypt(string _) => apiKey ?? "decrypted-fake-key";

        public string Encrypt(string plaintext) => plaintext;
    }

    private class FakeLogger : ILogger<ComfyUiAdapter>
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

    private class QueueHandler : HttpMessageHandler
    {
        private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _handlers = new();

        public void Enqueue(Func<HttpRequestMessage, HttpResponseMessage> handler) =>
            _handlers.Enqueue(handler);

        public HttpRequestMessage? LastRequest { get; private set; }
        public HttpRequestMessage? FirstRequest { get; private set; }
        public List<HttpRequestMessage> AllRequests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken ct
        )
        {
            LastRequest = request;
            if (FirstRequest is null)
                FirstRequest = request;
            AllRequests.Add(request);
            if (_handlers.Count == 0)
                throw new InvalidOperationException("No handler enqueued for request");
            var handler = _handlers.Dequeue();
            return Task.FromResult(handler(request));
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
                LastBody = request.Content.ReadAsStringAsync(ct).GetAwaiter().GetResult();
            var response = new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(jsonBody, Encoding.UTF8, "application/json"),
            };
            return Task.FromResult(response);
        }
    }

    private class AuthCaptureHandler : HttpMessageHandler
    {
        public string? CapturedAuth { get; private set; }
        private readonly HttpResponseMessage _response;

        public AuthCaptureHandler(HttpResponseMessage response)
        {
            _response = response;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken ct
        )
        {
            CapturedAuth = request.Headers.Authorization?.ToString();
            return Task.FromResult(_response);
        }
    }

    [Fact]
    public void AdapterType_ReturnsProviderAdapterType_ComfyUi()
    {
        var provider = CreateProvider(adapterType: AdapterType.ComfyUi);
        using var http = new HttpClient(new JsonBodyHandler(HttpStatusCode.OK, "{}"));
        var adapter = new ComfyUiAdapter(http, new FakeKeyVault(), provider, new FakeLogger());

        Assert.Equal(AdapterType.ComfyUi, adapter.AdapterType);
    }

    [Fact]
    public void AdapterType_ReturnsProviderAdapterType_Diffusers()
    {
        var provider = CreateProvider(adapterType: AdapterType.Diffusers);
        using var http = new HttpClient(new JsonBodyHandler(HttpStatusCode.OK, "{}"));
        var adapter = new ComfyUiAdapter(http, new FakeKeyVault(), provider, new FakeLogger());

        Assert.Equal(AdapterType.Diffusers, adapter.AdapterType);
    }

    [Fact]
    public async Task GenerateImageAsync_SendsCorrectWorkflowShape()
    {
        var queue = new QueueHandler();

        queue.Enqueue(_ =>
        {
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"prompt_id":"test-prompt-id"}""",
                    Encoding.UTF8,
                    "application/json"
                ),
            };
        });

        queue.Enqueue(_ =>
        {
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"test-prompt-id":{"outputs":{"9":{"images":[{"filename":"test.png","subfolder":"","type":"output"}]}}}}""",
                    Encoding.UTF8,
                    "application/json"
                ),
            };
        });

        queue.Enqueue(_ =>
        {
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(new byte[] { 0x89, 0x50, 0x4E, 0x47 }),
            };
        });

        using var http = new HttpClient(queue);
        var provider = CreateProvider();
        var adapter = new ComfyUiAdapter(http, new FakeKeyVault(), provider, new FakeLogger());

        var request = new ImageRequest(
            Guid.NewGuid(),
            "sd_xl_base.safetensors",
            "A beautiful sunset",
            ImageSize.Square1024,
            1
        );

        await adapter.GenerateImageAsync(request);

        Assert.NotNull(queue.FirstRequest);
        Assert.Equal(HttpMethod.Post, queue.FirstRequest.Method);
        Assert.Contains("/prompt", queue.FirstRequest.RequestUri?.PathAndQuery);

        var bodyContent = await queue.FirstRequest.Content!.ReadAsStringAsync();
        var doc = JsonDocument.Parse(bodyContent);
        var prompt = doc.RootElement.GetProperty("prompt");

        Assert.True(prompt.TryGetProperty("3", out var checkpointNode));
        Assert.Equal(
            "CheckpointLoaderSimple",
            checkpointNode.GetProperty("class_type").GetString()
        );
        Assert.Equal(
            "sd_xl_base.safetensors",
            checkpointNode.GetProperty("inputs").GetProperty("ckpt_name").GetString()
        );

        Assert.True(prompt.TryGetProperty("4", out var positiveClipNode));
        Assert.Equal("CLIPTextEncode", positiveClipNode.GetProperty("class_type").GetString());
        Assert.Equal(
            "A beautiful sunset",
            positiveClipNode.GetProperty("inputs").GetProperty("text").GetString()
        );

        Assert.True(prompt.TryGetProperty("5", out var negativeClipNode));
        Assert.Equal("CLIPTextEncode", negativeClipNode.GetProperty("class_type").GetString());
        Assert.Equal(
            "bad quality",
            negativeClipNode.GetProperty("inputs").GetProperty("text").GetString()
        );

        Assert.True(prompt.TryGetProperty("6", out var latentNode));
        Assert.Equal("EmptyLatentImage", latentNode.GetProperty("class_type").GetString());
        Assert.Equal(1024, latentNode.GetProperty("inputs").GetProperty("width").GetInt32());
        Assert.Equal(1024, latentNode.GetProperty("inputs").GetProperty("height").GetInt32());

        Assert.True(prompt.TryGetProperty("7", out var ksamplerNode));
        Assert.Equal("KSampler", ksamplerNode.GetProperty("class_type").GetString());
    }

    [Fact]
    public async Task GenerateImageAsync_PollsHistoryUntilSuccess()
    {
        var queue = new QueueHandler();

        queue.Enqueue(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """{"prompt_id":"prompt-123"}""",
                Encoding.UTF8,
                "application/json"
            ),
        });

        queue.Enqueue(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """{"prompt-123":{"outputs":{}}}""",
                Encoding.UTF8,
                "application/json"
            ),
        });

        queue.Enqueue(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """{"prompt-123":{"outputs":{"9":{"images":[{"filename":"output.png","subfolder":"","type":"output"}]}}}}""",
                Encoding.UTF8,
                "application/json"
            ),
        });

        queue.Enqueue(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(new byte[] { 0x89, 0x50, 0x4E, 0x47 }),
        });

        using var http = new HttpClient(queue);
        var provider = CreateProvider();
        var adapter = new ComfyUiAdapter(http, new FakeKeyVault(), provider, new FakeLogger());

        var request = new ImageRequest(
            Guid.NewGuid(),
            "model.safetensors",
            "prompt",
            ImageSize.Square1024,
            1
        );

        var result = await adapter.GenerateImageAsync(request);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Single(result.Value.ImageDataUrlsOrKeys);
    }

    [Fact]
    public async Task GenerateImageAsync_HistoryTimeout_ReturnsFailure()
    {
        var queue = new QueueHandler();

        queue.Enqueue(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """{"prompt_id":"timeout-prompt"}""",
                Encoding.UTF8,
                "application/json"
            ),
        });

        for (var i = 0; i < 151; i++)
        {
            queue.Enqueue(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"timeout-prompt":{"outputs":{}}}""",
                    Encoding.UTF8,
                    "application/json"
                ),
            });
        }

        using var http = new HttpClient(queue);
        var provider = CreateProvider();
        var adapter = new ComfyUiAdapter(http, new FakeKeyVault(), provider, new FakeLogger());

        var request = new ImageRequest(
            Guid.NewGuid(),
            "model.safetensors",
            "prompt",
            ImageSize.Square1024,
            1
        );

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var result = await adapter.GenerateImageAsync(request, cts.Token);

        Assert.True(result.IsFailure);
        Assert.Equal("COMFYUI_HISTORY_TIMEOUT", result.Error.Code);
    }

    [Fact]
    public async Task GenerateImageAsync_HistoryError_ReturnsFailure()
    {
        var queue = new QueueHandler();

        queue.Enqueue(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """{"prompt_id":"error-prompt"}""",
                Encoding.UTF8,
                "application/json"
            ),
        });

        queue.Enqueue(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """{"error-prompt":{"status":{"error":{"message":"Out of memory"}},"outputs":{}}}""",
                Encoding.UTF8,
                "application/json"
            ),
        });

        using var http = new HttpClient(queue);
        var provider = CreateProvider();
        var adapter = new ComfyUiAdapter(http, new FakeKeyVault(), provider, new FakeLogger());

        var request = new ImageRequest(
            Guid.NewGuid(),
            "model.safetensors",
            "prompt",
            ImageSize.Square1024,
            1
        );

        var result = await adapter.GenerateImageAsync(request);

        Assert.True(result.IsFailure);
        Assert.Equal("COMFYUI_GENERATION_ERROR", result.Error.Code);
        Assert.Contains("Out of memory", result.Error.Message);
    }

    [Fact]
    public async Task GenerateImageAsync_ApiKeyDecryptedAndSent()
    {
        var queue = new QueueHandler();

        queue.Enqueue(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """{"prompt_id":"auth-prompt"}""",
                Encoding.UTF8,
                "application/json"
            ),
        });

        queue.Enqueue(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """{"auth-prompt":{"outputs":{"9":{"images":[{"filename":"out.png","subfolder":"","type":"output"}]}}}}""",
                Encoding.UTF8,
                "application/json"
            ),
        });

        queue.Enqueue(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(new byte[] { 0x89 }),
        });

        using var http = new HttpClient(queue);
        var provider = CreateProvider("https://comfy.local", "encrypted-cipher");
        var adapter = new ComfyUiAdapter(
            http,
            new FakeKeyVault("decrypted-api-key"),
            provider,
            new FakeLogger()
        );

        var request = new ImageRequest(Guid.NewGuid(), "model", "prompt", ImageSize.Square1024, 1);
        await adapter.GenerateImageAsync(request);

        Assert.NotNull(queue.LastRequest);
        Assert.Equal(
            "Bearer decrypted-api-key",
            queue.LastRequest.Headers.Authorization?.ToString()
        );
    }

    [Fact]
    public async Task InpaintAsync_NullImageBytes_ReturnsFailure()
    {
        using var http = new HttpClient(new JsonBodyHandler(HttpStatusCode.OK, "{}"));
        var provider = CreateProvider();
        var adapter = new ComfyUiAdapter(http, new FakeKeyVault(), provider, new FakeLogger());

        var request = new InpaintRequest(
            Guid.NewGuid(),
            "model",
            "prompt",
            null!,
            new byte[] { 0x01 },
            ImageSize.Square1024
        );

        var result = await adapter.InpaintAsync(request);

        Assert.True(result.IsFailure);
        Assert.Equal("COMFYUI_INPAINT_MISSING_INPUT", result.Error.Code);
    }

    [Fact]
    public async Task InpaintAsync_NullMaskBytes_ReturnsFailure()
    {
        using var http = new HttpClient(new JsonBodyHandler(HttpStatusCode.OK, "{}"));
        var provider = CreateProvider();
        var adapter = new ComfyUiAdapter(http, new FakeKeyVault(), provider, new FakeLogger());

        var request = new InpaintRequest(
            Guid.NewGuid(),
            "model",
            "prompt",
            new byte[] { 0x01 },
            null!,
            ImageSize.Square1024
        );

        var result = await adapter.InpaintAsync(request);

        Assert.True(result.IsFailure);
        Assert.Equal("COMFYUI_INPAINT_MISSING_INPUT", result.Error.Code);
    }

    [Fact]
    public async Task InpaintAsync_SendsInpaintWorkflow()
    {
        var queue = new QueueHandler();

        queue.Enqueue(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """{"name":"image.png","subfolder":""}""",
                Encoding.UTF8,
                "application/json"
            ),
        });

        queue.Enqueue(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """{"name":"mask.png","subfolder":""}""",
                Encoding.UTF8,
                "application/json"
            ),
        });

        queue.Enqueue(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """{"prompt_id":"inpaint-prompt"}""",
                Encoding.UTF8,
                "application/json"
            ),
        });

        queue.Enqueue(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """{"inpaint-prompt":{"outputs":{"13":{"images":[{"filename":"inpainted.png","subfolder":"","type":"output"}]}}}}""",
                Encoding.UTF8,
                "application/json"
            ),
        });

        queue.Enqueue(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(new byte[] { 0x89, 0x50, 0x4E, 0x47 }),
        });

        using var http = new HttpClient(queue);
        var provider = CreateProvider();
        var adapter = new ComfyUiAdapter(http, new FakeKeyVault(), provider, new FakeLogger());

        var request = new InpaintRequest(
            Guid.NewGuid(),
            "sd_xl_inpaint.safetensors",
            "Remove the background",
            new byte[] { 0x89, 0x50, 0x4E, 0x47 },
            new byte[] { 0x89, 0x50, 0x4E, 0x47 },
            ImageSize.Square1024
        );

        var result = await adapter.InpaintAsync(request);

        Assert.True(result.IsSuccess);

        var imageUploadRequest = queue.FirstRequest!;
        Assert.Equal(HttpMethod.Post, imageUploadRequest.Method);
        Assert.Contains("/upload/image", imageUploadRequest.RequestUri?.PathAndQuery);
    }
}
