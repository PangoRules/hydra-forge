namespace HydraForge.Infrastructure.Tests.Llm.Adapters;

using System.Net;
using System.Text;
using System.Text.Json;
using HydraForge.Application.Llm;
using HydraForge.Domain.Entities.Admin;
using HydraForge.Domain.Enums;
using HydraForge.Infrastructure.Llm.Adapters;
using Microsoft.Extensions.Logging;

public class DallEAdapterTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private static LlmProvider CreateProvider(
        string baseUrl = "https://api.openai.com",
        string? apiKeyEncrypted = null
    )
    {
        return new LlmProvider
        {
            Id = Guid.NewGuid(),
            Name = "OpenAI",
            BaseUrl = baseUrl,
            ApiKeyEncrypted = apiKeyEncrypted ?? "",
            AdapterType = AdapterType.DallE,
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

    private class FakeLogger : ILogger<DallEAdapter>
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
                Content = new StringContent(
                    jsonBody,
                    Encoding.UTF8,
                    System.Net.Http.Headers.MediaTypeHeaderValue.Parse("application/json")
                ),
            };
            return Task.FromResult(response);
        }
    }

    private class MultipartBodyHandler(HttpStatusCode statusCode, string jsonBody)
        : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastContentType { get; private set; }
        public List<(string Name, string? FileName, byte[] Data)> CapturedParts { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken ct
        )
        {
            LastRequest = request;
            LastContentType = request.Content?.Headers.ContentType?.MediaType;

            if (request.Content is MultipartContent multipart)
            {
                foreach (var part in multipart)
                {
                    var name = part.Headers.ContentDisposition?.Name?.Trim('"') ?? "";
                    var fileName = part.Headers.ContentDisposition?.FileName?.Trim('"');
                    using var ms = new MemoryStream();
                    await part.CopyToAsync(ms);
                    CapturedParts.Add((name, fileName, ms.ToArray()));
                }
            }

            var response = new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(
                    jsonBody,
                    Encoding.UTF8,
                    System.Net.Http.Headers.MediaTypeHeaderValue.Parse("application/json")
                ),
            };
            return response;
        }
    }

    [Fact]
    public void AdapterType_ReturnsDallE()
    {
        var provider = CreateProvider();
        using var http = new HttpClient(new JsonBodyHandler(HttpStatusCode.OK, "{}"));
        var adapter = new DallEAdapter(http, new FakeKeyVault(), provider, new FakeLogger());

        Assert.Equal(AdapterType.DallE, adapter.AdapterType);
    }

    [Fact]
    public async Task GenerateImageAsync_SendsCorrectRequestShape()
    {
        var bodyHandler = new JsonBodyHandler(HttpStatusCode.OK, """{"data":[]}""");
        using var http = new HttpClient(bodyHandler);
        var provider = CreateProvider();
        var adapter = new DallEAdapter(
            http,
            new FakeKeyVault("test-key"),
            provider,
            new FakeLogger()
        );

        var request = new ImageRequest(
            Guid.NewGuid(),
            "dall-e-3",
            "A sunset over the ocean",
            ImageSize.Square1024,
            1
        );

        await adapter.GenerateImageAsync(request);

        Assert.NotNull(bodyHandler.LastBody);
        var doc = JsonDocument.Parse(bodyHandler.LastBody);
        Assert.Equal("dall-e-3", doc.RootElement.GetProperty("model").GetString());
        Assert.Equal("A sunset over the ocean", doc.RootElement.GetProperty("prompt").GetString());
        Assert.Equal(1, doc.RootElement.GetProperty("n").GetInt32());
        Assert.Equal("1024x1024", doc.RootElement.GetProperty("size").GetString());
        Assert.Equal("url", doc.RootElement.GetProperty("response_format").GetString());
    }

    [Theory]
    [InlineData(ImageSize.Square1024, "1024x1024")]
    [InlineData(ImageSize.Landscape1792, "1792x1024")]
    [InlineData(ImageSize.Portrait1024, "1024x1792")]
    public async Task GenerateImageAsync_MapsSizeCorrectly(ImageSize size, string expectedApiSize)
    {
        var bodyHandler = new JsonBodyHandler(HttpStatusCode.OK, """{"data":[]}""");
        using var http = new HttpClient(bodyHandler);
        var provider = CreateProvider();
        var adapter = new DallEAdapter(http, new FakeKeyVault(), provider, new FakeLogger());

        var request = new ImageRequest(Guid.NewGuid(), "dall-e-3", "prompt", size, 1);
        await adapter.GenerateImageAsync(request);

        var doc = JsonDocument.Parse(bodyHandler.LastBody!);
        Assert.Equal(expectedApiSize, doc.RootElement.GetProperty("size").GetString());
    }

    [Fact]
    public async Task GenerateImageAsync_ParsesUrlResponse()
    {
        var json = JsonSerializer.Serialize(
            new
            {
                data = new[]
                {
                    new { url = "https://example.com/image1.png" },
                    new { url = "https://example.com/image2.png" },
                },
            },
            JsonOptions
        );

        using var http = new HttpClient(new JsonBodyHandler(HttpStatusCode.OK, json));
        var provider = CreateProvider();
        var adapter = new DallEAdapter(http, new FakeKeyVault(), provider, new FakeLogger());

        var request = new ImageRequest(
            Guid.NewGuid(),
            "dall-e-3",
            "A cat",
            ImageSize.Square1024,
            2
        );

        var result = await adapter.GenerateImageAsync(request);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(2, result.Value.ImageDataUrlsOrKeys.Count);
        Assert.Equal("https://example.com/image1.png", result.Value.ImageDataUrlsOrKeys[0]);
        Assert.Equal("https://example.com/image2.png", result.Value.ImageDataUrlsOrKeys[1]);
    }

    [Fact]
    public async Task GenerateImageAsync_ParsesB64JsonResponse()
    {
        var json = JsonSerializer.Serialize(
            new
            {
                data = new[]
                {
                    new { b64_json = "SGVsbG8gV29ybGQ=" },
                    new { b64_json = "VGVzdCBJbWFnZQ==" },
                },
            },
            JsonOptions
        );

        using var http = new HttpClient(new JsonBodyHandler(HttpStatusCode.OK, json));
        var provider = CreateProvider();
        var adapter = new DallEAdapter(http, new FakeKeyVault(), provider, new FakeLogger());

        var request = new ImageRequest(
            Guid.NewGuid(),
            "dall-e-3",
            "A cat",
            ImageSize.Square1024,
            2
        );

        var result = await adapter.GenerateImageAsync(request);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(2, result.Value.ImageDataUrlsOrKeys.Count);
        Assert.Equal("SGVsbG8gV29ybGQ=", result.Value.ImageDataUrlsOrKeys[0]);
        Assert.Equal("VGVzdCBJbWFnZQ==", result.Value.ImageDataUrlsOrKeys[1]);
    }

    [Fact]
    public async Task GenerateImageAsync_NonSuccessStatus_ReturnsFailure()
    {
        var errorBody = """{"error":{"message":"Rate limit exceeded"}}""";
        using var http = new HttpClient(
            new JsonBodyHandler(HttpStatusCode.TooManyRequests, errorBody)
        );
        var provider = CreateProvider();
        var logger = new FakeLogger();
        var adapter = new DallEAdapter(http, new FakeKeyVault(), provider, logger);

        var request = new ImageRequest(
            Guid.NewGuid(),
            "dall-e-3",
            "A cat",
            ImageSize.Square1024,
            1
        );

        var result = await adapter.GenerateImageAsync(request);

        Assert.True(result.IsFailure);
        Assert.Equal("DALLE_GENERATE_FAILED", result.Error.Code);
        Assert.NotNull(logger.Error);
        Assert.Contains("TooManyRequests", logger.Error.Message);
        Assert.Contains("Rate limit exceeded", logger.Error.Message);
    }

    [Fact]
    public async Task GenerateImageAsync_InvalidJson_ReturnsFailure()
    {
        using var http = new HttpClient(new JsonBodyHandler(HttpStatusCode.OK, "not json {{{"));
        var provider = CreateProvider();
        var adapter = new DallEAdapter(http, new FakeKeyVault(), provider, new FakeLogger());

        var request = new ImageRequest(
            Guid.NewGuid(),
            "dall-e-3",
            "A cat",
            ImageSize.Square1024,
            1
        );

        var result = await adapter.GenerateImageAsync(request);

        Assert.True(result.IsFailure);
        Assert.Equal("DALLE_PARSE_FAILED", result.Error.Code);
    }

    [Fact]
    public async Task GenerateImageAsync_EmptyData_ReturnsFailure()
    {
        using var http = new HttpClient(new JsonBodyHandler(HttpStatusCode.OK, """{"data":[]}"""));
        var provider = CreateProvider();
        var adapter = new DallEAdapter(http, new FakeKeyVault(), provider, new FakeLogger());

        var request = new ImageRequest(
            Guid.NewGuid(),
            "dall-e-3",
            "A cat",
            ImageSize.Square1024,
            1
        );

        var result = await adapter.GenerateImageAsync(request);

        Assert.True(result.IsFailure);
        Assert.Equal("DALLE_EMPTY_RESPONSE", result.Error.Code);
    }

    [Fact]
    public async Task GenerateImageAsync_DataItemsWithoutUrlOrB64Json_ReturnsFailure()
    {
        var json = """{"data":[{"revised_prompt":"A cat, revised"}]}""";
        using var http = new HttpClient(new JsonBodyHandler(HttpStatusCode.OK, json));
        var provider = CreateProvider();
        var adapter = new DallEAdapter(http, new FakeKeyVault(), provider, new FakeLogger());

        var request = new ImageRequest(
            Guid.NewGuid(),
            "dall-e-3",
            "A cat",
            ImageSize.Square1024,
            1
        );

        var result = await adapter.GenerateImageAsync(request);

        Assert.True(result.IsFailure);
        Assert.Equal("DALLE_NO_IMAGE_DATA", result.Error.Code);
    }

    [Fact]
    public async Task GenerateImageAsync_ApiKeyDecryptedAndSent()
    {
        var authHandler = new AuthCaptureHandler("""{"data":[]}""");
        using var http = new HttpClient(authHandler);
        var provider = CreateProvider("https://api.openai.com", "encrypted-cipher");
        var adapter = new DallEAdapter(
            http,
            new FakeKeyVault("decrypted-api-key"),
            provider,
            new FakeLogger()
        );

        var request = new ImageRequest(
            Guid.NewGuid(),
            "dall-e-3",
            "A cat",
            ImageSize.Square1024,
            1
        );

        await adapter.GenerateImageAsync(request);

        Assert.Equal("Bearer decrypted-api-key", authHandler.CapturedAuth);
    }

    [Fact]
    public async Task GenerateImageAsync_NoApiKey_NoAuthHeader()
    {
        var authHandler = new AuthCaptureHandler("""{"data":[]}""");
        using var http = new HttpClient(authHandler);
        var provider = CreateProvider("https://api.openai.com", null);
        var adapter = new DallEAdapter(http, new FakeKeyVault(), provider, new FakeLogger());

        var request = new ImageRequest(
            Guid.NewGuid(),
            "dall-e-3",
            "A cat",
            ImageSize.Square1024,
            1
        );

        await adapter.GenerateImageAsync(request);

        Assert.Null(authHandler.CapturedAuth);
    }

    [Fact]
    public async Task InpaintAsync_SendsMultipartFormWithImageAndMask()
    {
        var jsonResponse = JsonSerializer.Serialize(
            new { data = new[] { new { url = "https://example.com/inpainted.png" } } },
            JsonOptions
        );
        var multipartHandler = new MultipartBodyHandler(HttpStatusCode.OK, jsonResponse);
        using var http = new HttpClient(multipartHandler);
        var provider = CreateProvider();
        var adapter = new DallEAdapter(http, new FakeKeyVault(), provider, new FakeLogger());

        var imageBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47 };
        var maskBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47 };

        var request = new InpaintRequest(
            Guid.NewGuid(),
            "dall-e-3",
            "Remove the background",
            imageBytes,
            maskBytes,
            ImageSize.Square1024
        );

        await adapter.InpaintAsync(request);

        Assert.NotNull(multipartHandler.LastRequest);
        Assert.Equal("multipart/form-data", multipartHandler.LastContentType);

        Assert.Contains(multipartHandler.CapturedParts, p => p.Name == "image");
        var imagePart = multipartHandler.CapturedParts.First(p => p.Name == "image");
        Assert.Equal("image.png", imagePart.FileName);
        Assert.Equal(imageBytes, imagePart.Data);

        Assert.Contains(multipartHandler.CapturedParts, p => p.Name == "mask");
        var maskPart = multipartHandler.CapturedParts.First(p => p.Name == "mask");
        Assert.Equal("mask.png", maskPart.FileName);
        Assert.Equal(maskBytes, maskPart.Data);

        Assert.Contains(multipartHandler.CapturedParts, p => p.Name == "prompt");
        var promptPart = multipartHandler.CapturedParts.First(p => p.Name == "prompt");
        Assert.Equal("Remove the background", Encoding.UTF8.GetString(promptPart.Data));

        Assert.Contains(multipartHandler.CapturedParts, p => p.Name == "model");
        var modelPart = multipartHandler.CapturedParts.First(p => p.Name == "model");
        Assert.Equal("dall-e-3", Encoding.UTF8.GetString(modelPart.Data));

        Assert.Contains(multipartHandler.CapturedParts, p => p.Name == "n");
        var nPart = multipartHandler.CapturedParts.First(p => p.Name == "n");
        Assert.Equal("1", Encoding.UTF8.GetString(nPart.Data));

        Assert.Contains(multipartHandler.CapturedParts, p => p.Name == "size");
        var sizePart = multipartHandler.CapturedParts.First(p => p.Name == "size");
        Assert.Equal("1024x1024", Encoding.UTF8.GetString(sizePart.Data));
    }

    [Fact]
    public async Task InpaintAsync_ParsesResponseCorrectly()
    {
        var json = JsonSerializer.Serialize(
            new { data = new[] { new { url = "https://example.com/result.png" } } },
            JsonOptions
        );
        using var http = new HttpClient(new JsonBodyHandler(HttpStatusCode.OK, json));
        var provider = CreateProvider();
        var adapter = new DallEAdapter(http, new FakeKeyVault(), provider, new FakeLogger());

        var request = new InpaintRequest(
            Guid.NewGuid(),
            "dall-e-3",
            "Remove background",
            new byte[] { 0x01 },
            new byte[] { 0x02 },
            ImageSize.Landscape1792
        );

        var result = await adapter.InpaintAsync(request);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Single(result.Value.ImageDataUrlsOrKeys);
        Assert.Equal("https://example.com/result.png", result.Value.ImageDataUrlsOrKeys[0]);
    }

    [Fact]
    public async Task InpaintAsync_NonSuccessStatus_ReturnsFailure()
    {
        var errorBody = """{"error":{"message":"Invalid mask dimensions"}}""";
        using var http = new HttpClient(new JsonBodyHandler(HttpStatusCode.BadRequest, errorBody));
        var provider = CreateProvider();
        var logger = new FakeLogger();
        var adapter = new DallEAdapter(http, new FakeKeyVault(), provider, logger);

        var request = new InpaintRequest(
            Guid.NewGuid(),
            "dall-e-3",
            "prompt",
            new byte[] { 0x01 },
            new byte[] { 0x02 },
            ImageSize.Square1024
        );

        var result = await adapter.InpaintAsync(request);

        Assert.True(result.IsFailure);
        Assert.Equal("DALLE_INPAINT_FAILED", result.Error.Code);
        Assert.NotNull(logger.Error);
        Assert.Contains("BadRequest", logger.Error.Message);
        Assert.Contains("Invalid mask dimensions", logger.Error.Message);
    }

    [Fact]
    public async Task InpaintAsync_NullImageBytes_ReturnsFailure()
    {
        using var http = new HttpClient(new JsonBodyHandler(HttpStatusCode.OK, "{}"));
        var provider = CreateProvider();
        var adapter = new DallEAdapter(http, new FakeKeyVault(), provider, new FakeLogger());

        var request = new InpaintRequest(
            Guid.NewGuid(),
            "dall-e-3",
            "prompt",
            null!,
            new byte[] { 0x01 },
            ImageSize.Square1024
        );

        var result = await adapter.InpaintAsync(request);

        Assert.True(result.IsFailure);
        Assert.Equal("DALLE_INPAINT_MISSING_INPUT", result.Error.Code);
    }

    [Fact]
    public async Task InpaintAsync_EmptyImageBytes_ReturnsFailure()
    {
        using var http = new HttpClient(new JsonBodyHandler(HttpStatusCode.OK, "{}"));
        var provider = CreateProvider();
        var adapter = new DallEAdapter(http, new FakeKeyVault(), provider, new FakeLogger());

        var request = new InpaintRequest(
            Guid.NewGuid(),
            "dall-e-3",
            "prompt",
            Array.Empty<byte>(),
            new byte[] { 0x01 },
            ImageSize.Square1024
        );

        var result = await adapter.InpaintAsync(request);

        Assert.True(result.IsFailure);
        Assert.Equal("DALLE_INPAINT_MISSING_INPUT", result.Error.Code);
    }

    [Fact]
    public async Task InpaintAsync_NullMaskBytes_ReturnsFailure()
    {
        using var http = new HttpClient(new JsonBodyHandler(HttpStatusCode.OK, "{}"));
        var provider = CreateProvider();
        var adapter = new DallEAdapter(http, new FakeKeyVault(), provider, new FakeLogger());

        var request = new InpaintRequest(
            Guid.NewGuid(),
            "dall-e-3",
            "prompt",
            new byte[] { 0x01 },
            null!,
            ImageSize.Square1024
        );

        var result = await adapter.InpaintAsync(request);

        Assert.True(result.IsFailure);
        Assert.Equal("DALLE_INPAINT_MISSING_INPUT", result.Error.Code);
    }

    [Fact]
    public async Task InpaintAsync_EmptyMaskBytes_ReturnsFailure()
    {
        using var http = new HttpClient(new JsonBodyHandler(HttpStatusCode.OK, "{}"));
        var provider = CreateProvider();
        var adapter = new DallEAdapter(http, new FakeKeyVault(), provider, new FakeLogger());

        var request = new InpaintRequest(
            Guid.NewGuid(),
            "dall-e-3",
            "prompt",
            new byte[] { 0x01 },
            Array.Empty<byte>(),
            ImageSize.Square1024
        );

        var result = await adapter.InpaintAsync(request);

        Assert.True(result.IsFailure);
        Assert.Equal("DALLE_INPAINT_MISSING_INPUT", result.Error.Code);
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
