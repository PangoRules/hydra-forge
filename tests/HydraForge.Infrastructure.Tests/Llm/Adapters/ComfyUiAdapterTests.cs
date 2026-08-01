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

        Assert.True(prompt.TryGetProperty("4", out var node4));
        var node4Clip = node4.GetProperty("inputs").GetProperty("clip");
        Assert.Equal(JsonValueKind.Array, node4Clip.ValueKind);
        Assert.Equal("3", node4Clip[0].GetString());
        Assert.Equal(0, node4Clip[1].GetInt32());

        Assert.True(prompt.TryGetProperty("5", out var node5));
        var node5Clip = node5.GetProperty("inputs").GetProperty("clip");
        Assert.Equal(JsonValueKind.Array, node5Clip.ValueKind);
        Assert.Equal("3", node5Clip[0].GetString());
        Assert.Equal(0, node5Clip[1].GetInt32());

        Assert.True(prompt.TryGetProperty("7", out var node7));
        var node7Model = node7.GetProperty("inputs").GetProperty("model");
        Assert.Equal(JsonValueKind.Array, node7Model.ValueKind);
        Assert.Equal("3", node7Model[0].GetString());
        var node7Positive = node7.GetProperty("inputs").GetProperty("positive");
        Assert.Equal(JsonValueKind.Array, node7Positive.ValueKind);
        Assert.Equal("4", node7Positive[0].GetString());
        var node7Negative = node7.GetProperty("inputs").GetProperty("negative");
        Assert.Equal(JsonValueKind.Array, node7Negative.ValueKind);
        Assert.Equal("5", node7Negative[0].GetString());

        Assert.True(prompt.TryGetProperty("8", out var node8));
        var node8Samples = node8.GetProperty("inputs").GetProperty("samples");
        Assert.Equal(JsonValueKind.Array, node8Samples.ValueKind);
        Assert.Equal("7", node8Samples[0].GetString());
        var node8Vae = node8.GetProperty("inputs").GetProperty("vae");
        Assert.Equal(JsonValueKind.Array, node8Vae.ValueKind);
        Assert.Equal("3", node8Vae[0].GetString());
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
    public async Task GenerateImageAsync_CallerCancellation_PropagatesOperationCanceled()
    {
        // Distinct from the internal 5-minute poll timeout (COMFYUI_HISTORY_TIMEOUT, which
        // returns a Result.Failure): a caller-cancelled token (e.g. HTTP request aborted)
        // must propagate as a real OperationCanceledException, not get reported as a
        // business failure — see PollHistoryUntilCompleteAsync's two-catch split.
        var queue = new QueueHandler();

        queue.Enqueue(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """{"prompt_id":"cancel-prompt"}""",
                Encoding.UTF8,
                "application/json"
            ),
        });

        for (var i = 0; i < 151; i++)
        {
            queue.Enqueue(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"cancel-prompt":{"outputs":{}}}""",
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
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            adapter.GenerateImageAsync(request, cts.Token)
        );
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
            ImageSize.Square1024,
            2
        );

        var result = await adapter.InpaintAsync(request);

        Assert.True(result.IsSuccess);

        var imageUploadRequest = queue.FirstRequest!;
        Assert.Equal(HttpMethod.Post, imageUploadRequest.Method);
        Assert.Contains("/upload/image", imageUploadRequest.RequestUri?.PathAndQuery);

        var workflowRequest = queue.AllRequests[2];
        Assert.Equal(HttpMethod.Post, workflowRequest.Method);
        Assert.Contains("/prompt", workflowRequest.RequestUri?.PathAndQuery);

        var workflowBody = await workflowRequest.Content!.ReadAsStringAsync();
        var workflowDoc = JsonDocument.Parse(workflowBody);
        var workflow = workflowDoc.RootElement.GetProperty("prompt");

        Assert.True(workflow.TryGetProperty("3", out var ckpt));
        Assert.Equal("CheckpointLoaderSimple", ckpt.GetProperty("class_type").GetString());

        Assert.True(workflow.TryGetProperty("4", out var loadImage));
        Assert.Equal("LoadImage", loadImage.GetProperty("class_type").GetString());
        Assert.Equal("image.png", loadImage.GetProperty("inputs").GetProperty("image").GetString());

        Assert.True(workflow.TryGetProperty("14", out var loadMask));
        Assert.Equal("LoadImage", loadMask.GetProperty("class_type").GetString());
        Assert.Equal("mask.png", loadMask.GetProperty("inputs").GetProperty("image").GetString());

        Assert.True(workflow.TryGetProperty("7", out var vaeEncode));
        Assert.Equal("VAEEncodeForInpaint", vaeEncode.GetProperty("class_type").GetString());
        var pixels = vaeEncode.GetProperty("inputs").GetProperty("pixels");
        Assert.Equal(JsonValueKind.Array, pixels.ValueKind);
        Assert.Equal("4", pixels[0].GetString());
        var mask = vaeEncode.GetProperty("inputs").GetProperty("mask");
        Assert.Equal(JsonValueKind.Array, mask.ValueKind);
        Assert.Equal("14", mask[0].GetString());

        Assert.True(workflow.TryGetProperty("11", out var ksampler));
        Assert.Equal("KSampler", ksampler.GetProperty("class_type").GetString());
        var ksamplerPositive = ksampler.GetProperty("inputs").GetProperty("positive");
        Assert.Equal(JsonValueKind.Array, ksamplerPositive.ValueKind);
        Assert.Equal("5", ksamplerPositive[0].GetString());
        var ksamplerNegative = ksampler.GetProperty("inputs").GetProperty("negative");
        Assert.Equal(JsonValueKind.Array, ksamplerNegative.ValueKind);
        Assert.Equal("6", ksamplerNegative[0].GetString());
        var ksamplerLatent = ksampler.GetProperty("inputs").GetProperty("latent");
        Assert.Equal(JsonValueKind.Array, ksamplerLatent.ValueKind);
        Assert.Equal("7", ksamplerLatent[0].GetString());
    }

    [Fact]
    public async Task GenerateImageAsync_SetsBatchSizeFromCount()
    {
        var queue = new QueueHandler();

        queue.Enqueue(_ =>
        {
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"prompt_id":"batch-prompt-id"}""",
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
                    """{"batch-prompt-id":{"outputs":{"9":{"images":[{"filename":"test.png","subfolder":"","type":"output"}]}}}}""",
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
            4
        );

        await adapter.GenerateImageAsync(request);

        Assert.NotNull(queue.FirstRequest);
        var bodyContent = await queue.FirstRequest.Content!.ReadAsStringAsync();
        var doc = JsonDocument.Parse(bodyContent);
        var prompt = doc.RootElement.GetProperty("prompt");

        Assert.True(prompt.TryGetProperty("6", out var latentNode));
        Assert.Equal(4, latentNode.GetProperty("inputs").GetProperty("batch_size").GetInt32());
    }
}
