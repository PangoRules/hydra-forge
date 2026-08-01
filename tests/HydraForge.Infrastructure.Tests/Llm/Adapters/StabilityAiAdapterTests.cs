namespace HydraForge.Infrastructure.Tests.Llm.Adapters;

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using HydraForge.Application.Llm;
using HydraForge.Domain.Entities.Admin;
using HydraForge.Domain.Enums;
using HydraForge.Infrastructure.Llm.Adapters;
using Microsoft.Extensions.Logging;

public class StabilityAiAdapterTests
{
    private static LlmProvider CreateProvider(
        string baseUrl = "https://api.stability.ai",
        string? apiKeyEncrypted = null
    )
    {
        return new LlmProvider
        {
            Id = Guid.NewGuid(),
            Name = "StabilityAI",
            BaseUrl = baseUrl,
            ApiKeyEncrypted = apiKeyEncrypted ?? "",
            AdapterType = AdapterType.StabilityAi,
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

    private class FakeLogger : ILogger<StabilityAiAdapter>
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

    private class BinaryBodyHandler(HttpStatusCode statusCode, byte[] imageBytes)
        : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken ct
        )
        {
            LastRequest = request;
            var response = new HttpResponseMessage(statusCode)
            {
                Content = new ByteArrayContent(imageBytes),
            };
            response.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("image/png");
            return Task.FromResult(response);
        }
    }

    [Fact]
    public void AdapterType_ReturnsStabilityAi()
    {
        var provider = CreateProvider();
        using var http = new HttpClient(
            new MultipartBodyHandler(HttpStatusCode.OK, "{}")
        );
        var adapter = new StabilityAiAdapter(http, new FakeKeyVault(), provider, new FakeLogger());

        Assert.Equal(AdapterType.StabilityAi, adapter.AdapterType);
    }

    [Fact]
    public async Task GenerateImageAsync_SendsCorrectMultipartFields()
    {
        var bodyHandler = new MultipartBodyHandler(
            HttpStatusCode.OK,
            """{"image":"SGVsbG8gV29ybGQ="}"""
        );
        using var http = new HttpClient(bodyHandler);
        var provider = CreateProvider();
        var adapter = new StabilityAiAdapter(
            http,
            new FakeKeyVault("test-key"),
            provider,
            new FakeLogger()
        );

        var request = new ImageRequest(
            Guid.NewGuid(),
            "sd3.5-large",
            "A sunset over the ocean",
            ImageSize.Square1024,
            1
        );

        await adapter.GenerateImageAsync(request);

        Assert.NotNull(bodyHandler.LastRequest);
        Assert.Equal("multipart/form-data", bodyHandler.LastContentType);

        Assert.Contains(bodyHandler.CapturedParts, p => p.Name == "prompt");
        var promptPart = bodyHandler.CapturedParts.First(p => p.Name == "prompt");
        Assert.Equal("A sunset over the ocean", Encoding.UTF8.GetString(promptPart.Data));

        Assert.Contains(bodyHandler.CapturedParts, p => p.Name == "negative_prompt");
        var negativePart = bodyHandler.CapturedParts.First(p => p.Name == "negative_prompt");
        Assert.Equal(string.Empty, Encoding.UTF8.GetString(negativePart.Data));

        Assert.Contains(bodyHandler.CapturedParts, p => p.Name == "aspect_ratio");
        var aspectPart = bodyHandler.CapturedParts.First(p => p.Name == "aspect_ratio");
        Assert.Equal("1:1", Encoding.UTF8.GetString(aspectPart.Data));

        Assert.Contains(bodyHandler.CapturedParts, p => p.Name == "output_format");
        var formatPart = bodyHandler.CapturedParts.First(p => p.Name == "output_format");
        Assert.Equal("png", Encoding.UTF8.GetString(formatPart.Data));

        Assert.Contains(bodyHandler.CapturedParts, p => p.Name == "seed");
        var seedPart = bodyHandler.CapturedParts.First(p => p.Name == "seed");
        Assert.Equal("0", Encoding.UTF8.GetString(seedPart.Data));

        var url = bodyHandler.LastRequest.RequestUri!.ToString();
        Assert.Contains("/v2beta/stable-image/generate/sd3.5-large", url);
    }

    [Theory]
    [InlineData(ImageSize.Square1024, "1:1")]
    [InlineData(ImageSize.Landscape1792, "16:9")]
    [InlineData(ImageSize.Portrait1024, "9:16")]
    public async Task GenerateImageAsync_MapsAspectRatioCorrectly(ImageSize size, string expectedRatio)
    {
        var bodyHandler = new MultipartBodyHandler(
            HttpStatusCode.OK,
            """{"image":"SGVsbG8gV29ybGQ="}"""
        );
        using var http = new HttpClient(bodyHandler);
        var provider = CreateProvider();
        var adapter = new StabilityAiAdapter(http, new FakeKeyVault(), provider, new FakeLogger());

        var request = new ImageRequest(Guid.NewGuid(), "sd3.5-large", "prompt", size, 1);
        await adapter.GenerateImageAsync(request);

        Assert.Contains(bodyHandler.CapturedParts, p => p.Name == "aspect_ratio");
        var aspectPart = bodyHandler.CapturedParts.First(p => p.Name == "aspect_ratio");
        Assert.Equal(expectedRatio, Encoding.UTF8.GetString(aspectPart.Data));
    }

    [Fact]
    public async Task GenerateImageAsync_ParsesJsonResponse()
    {
        using var http = new HttpClient(
            new MultipartBodyHandler(HttpStatusCode.OK, """{"image":"SGVsbG8gV29ybGQ="}""")
        );
        var provider = CreateProvider();
        var adapter = new StabilityAiAdapter(http, new FakeKeyVault(), provider, new FakeLogger());

        var request = new ImageRequest(
            Guid.NewGuid(),
            "sd3.5-large",
            "A cat",
            ImageSize.Square1024,
            1
        );

        var result = await adapter.GenerateImageAsync(request);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Single(result.Value.ImageDataUrlsOrKeys);
        Assert.Equal("SGVsbG8gV29ybGQ=", result.Value.ImageDataUrlsOrKeys[0]);
    }

    [Fact]
    public async Task GenerateImageAsync_BinaryResponse_ReturnsBase64()
    {
        var pngBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        using var http = new HttpClient(new BinaryBodyHandler(HttpStatusCode.OK, pngBytes));
        var provider = CreateProvider();
        var adapter = new StabilityAiAdapter(http, new FakeKeyVault(), provider, new FakeLogger());

        var request = new ImageRequest(
            Guid.NewGuid(),
            "sd3.5-large",
            "A cat",
            ImageSize.Square1024,
            1
        );

        var result = await adapter.GenerateImageAsync(request);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Single(result.Value.ImageDataUrlsOrKeys);
        Assert.Equal(Convert.ToBase64String(pngBytes), result.Value.ImageDataUrlsOrKeys[0]);
    }

    [Fact]
    public async Task GenerateImageAsync_NonSuccessStatus_ReturnsFailure()
    {
        using var http = new HttpClient(
            new MultipartBodyHandler(HttpStatusCode.TooManyRequests, """{"error":"Rate limit exceeded"}""")
        );
        var provider = CreateProvider();
        var logger = new FakeLogger();
        var adapter = new StabilityAiAdapter(http, new FakeKeyVault(), provider, logger);

        var request = new ImageRequest(
            Guid.NewGuid(),
            "sd3.5-large",
            "A cat",
            ImageSize.Square1024,
            1
        );

        var result = await adapter.GenerateImageAsync(request);

        Assert.True(result.IsFailure);
        Assert.Equal("STABILITY_GENERATE_FAILED", result.Error.Code);
        Assert.NotNull(logger.Error);
        Assert.Contains("TooManyRequests", logger.Error.Message);
    }

    [Fact]
    public async Task GenerateImageAsync_EmptyJsonResponse_ReturnsFailure()
    {
        using var http = new HttpClient(
            new MultipartBodyHandler(HttpStatusCode.OK, "{}")
        );
        var provider = CreateProvider();
        var adapter = new StabilityAiAdapter(http, new FakeKeyVault(), provider, new FakeLogger());

        var request = new ImageRequest(
            Guid.NewGuid(),
            "sd3.5-large",
            "A cat",
            ImageSize.Square1024,
            1
        );

        var result = await adapter.GenerateImageAsync(request);

        Assert.True(result.IsFailure);
        Assert.Equal("STABILITY_EMPTY_RESPONSE", result.Error.Code);
    }

    [Fact]
    public async Task GenerateImageAsync_ApiKeyDecryptedAndSent()
    {
        var authHandler = new AuthCaptureHandler("""{"image":"SGVsbG8gV29ybGQ="}""");
        using var http = new HttpClient(authHandler);
        var provider = CreateProvider("https://api.stability.ai", "encrypted-cipher");
        var adapter = new StabilityAiAdapter(
            http,
            new FakeKeyVault("decrypted-api-key"),
            provider,
            new FakeLogger()
        );

        var request = new ImageRequest(
            Guid.NewGuid(),
            "sd3.5-large",
            "A cat",
            ImageSize.Square1024,
            1
        );

        await adapter.GenerateImageAsync(request);

        Assert.Equal("Bearer decrypted-api-key", authHandler.CapturedAuth);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    public async Task GenerateImageAsync_CountGreaterThanOne_ReturnsMultipleImages(int count)
    {
        var callCount = 0;
        var handler = new CountCapturingMultipartHandler(() =>
        {
            callCount++;
            var index = callCount.ToString();
            var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes($"image-{index}"));
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    $$"""{"image":"{{base64}}"}""",
                    Encoding.UTF8,
                    "application/json"
                ),
            };
        });
        using var http = new HttpClient(handler);
        var provider = CreateProvider();
        var adapter = new StabilityAiAdapter(http, new FakeKeyVault(), provider, new FakeLogger());

        var request = new ImageRequest(
            Guid.NewGuid(),
            "sd3.5-large",
            "A sunset",
            ImageSize.Square1024,
            count
        );

        var result = await adapter.GenerateImageAsync(request);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(count, result.Value.ImageDataUrlsOrKeys.Count);
        Assert.Equal(count, callCount);

        for (var i = 0; i < count; i++)
        {
            var expectedBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes($"image-{i + 1}"));
            Assert.Equal(expectedBase64, result.Value.ImageDataUrlsOrKeys[i]);
        }
    }

    [Fact]
    public async Task InpaintAsync_SendsMultipartFormWithImageAndMask()
    {
        var jsonResponse = """{"image":"SGVsbG8gV29ybGQ="}""";
        var multipartHandler = new MultipartBodyHandler(HttpStatusCode.OK, jsonResponse);
        using var http = new HttpClient(multipartHandler);
        var provider = CreateProvider();
        var adapter = new StabilityAiAdapter(http, new FakeKeyVault(), provider, new FakeLogger());

        var imageBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47 };
        var maskBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47 };

        var request = new InpaintRequest(
            Guid.NewGuid(),
            "sd3.5-large",
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

        Assert.Contains(multipartHandler.CapturedParts, p => p.Name == "output_format");
        var formatPart = multipartHandler.CapturedParts.First(p => p.Name == "output_format");
        Assert.Equal("png", Encoding.UTF8.GetString(formatPart.Data));

        var url = multipartHandler.LastRequest.RequestUri!.ToString();
        Assert.Contains("/v2beta/stable-image/edit/inpaint", url);
    }

    [Fact]
    public async Task InpaintAsync_ParsesJsonResponse()
    {
        using var http = new HttpClient(
            new MultipartBodyHandler(HttpStatusCode.OK, """{"image":"VGVzdCBJbWFnZQ=="}""")
        );
        var provider = CreateProvider();
        var adapter = new StabilityAiAdapter(http, new FakeKeyVault(), provider, new FakeLogger());

        var request = new InpaintRequest(
            Guid.NewGuid(),
            "sd3.5-large",
            "Remove background",
            new byte[] { 0x01 },
            new byte[] { 0x02 },
            ImageSize.Square1024
        );

        var result = await adapter.InpaintAsync(request);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Single(result.Value.ImageDataUrlsOrKeys);
        Assert.Equal("VGVzdCBJbWFnZQ==", result.Value.ImageDataUrlsOrKeys[0]);
    }

    [Fact]
    public async Task InpaintAsync_BinaryResponse_ReturnsBase64()
    {
        var pngBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        using var http = new HttpClient(new BinaryBodyHandler(HttpStatusCode.OK, pngBytes));
        var provider = CreateProvider();
        var adapter = new StabilityAiAdapter(http, new FakeKeyVault(), provider, new FakeLogger());

        var request = new InpaintRequest(
            Guid.NewGuid(),
            "sd3.5-large",
            "Remove background",
            new byte[] { 0x01 },
            new byte[] { 0x02 },
            ImageSize.Square1024
        );

        var result = await adapter.InpaintAsync(request);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Single(result.Value.ImageDataUrlsOrKeys);
        Assert.Equal(Convert.ToBase64String(pngBytes), result.Value.ImageDataUrlsOrKeys[0]);
    }

    [Fact]
    public async Task InpaintAsync_NonSuccessStatus_ReturnsFailure()
    {
        using var http = new HttpClient(
            new MultipartBodyHandler(HttpStatusCode.BadRequest, """{"error":"Invalid mask"}""")
        );
        var provider = CreateProvider();
        var logger = new FakeLogger();
        var adapter = new StabilityAiAdapter(http, new FakeKeyVault(), provider, logger);

        var request = new InpaintRequest(
            Guid.NewGuid(),
            "sd3.5-large",
            "prompt",
            new byte[] { 0x01 },
            new byte[] { 0x02 },
            ImageSize.Square1024
        );

        var result = await adapter.InpaintAsync(request);

        Assert.True(result.IsFailure);
        Assert.Equal("STABILITY_INPAINT_FAILED", result.Error.Code);
        Assert.NotNull(logger.Error);
        Assert.Contains("BadRequest", logger.Error.Message);
    }

    [Fact]
    public async Task InpaintAsync_NullImageBytes_ReturnsFailure()
    {
        using var http = new HttpClient(
            new MultipartBodyHandler(HttpStatusCode.OK, "{}")
        );
        var provider = CreateProvider();
        var adapter = new StabilityAiAdapter(http, new FakeKeyVault(), provider, new FakeLogger());

        var request = new InpaintRequest(
            Guid.NewGuid(),
            "sd3.5-large",
            "prompt",
            null!,
            new byte[] { 0x01 },
            ImageSize.Square1024
        );

        var result = await adapter.InpaintAsync(request);

        Assert.True(result.IsFailure);
        Assert.Equal("STABILITY_INPAINT_MISSING_INPUT", result.Error.Code);
    }

    [Fact]
    public async Task InpaintAsync_EmptyImageBytes_ReturnsFailure()
    {
        using var http = new HttpClient(
            new MultipartBodyHandler(HttpStatusCode.OK, "{}")
        );
        var provider = CreateProvider();
        var adapter = new StabilityAiAdapter(http, new FakeKeyVault(), provider, new FakeLogger());

        var request = new InpaintRequest(
            Guid.NewGuid(),
            "sd3.5-large",
            "prompt",
            Array.Empty<byte>(),
            new byte[] { 0x01 },
            ImageSize.Square1024
        );

        var result = await adapter.InpaintAsync(request);

        Assert.True(result.IsFailure);
        Assert.Equal("STABILITY_INPAINT_MISSING_INPUT", result.Error.Code);
    }

    [Fact]
    public async Task InpaintAsync_NullMaskBytes_ReturnsFailure()
    {
        using var http = new HttpClient(
            new MultipartBodyHandler(HttpStatusCode.OK, "{}")
        );
        var provider = CreateProvider();
        var adapter = new StabilityAiAdapter(http, new FakeKeyVault(), provider, new FakeLogger());

        var request = new InpaintRequest(
            Guid.NewGuid(),
            "sd3.5-large",
            "prompt",
            new byte[] { 0x01 },
            null!,
            ImageSize.Square1024
        );

        var result = await adapter.InpaintAsync(request);

        Assert.True(result.IsFailure);
        Assert.Equal("STABILITY_INPAINT_MISSING_INPUT", result.Error.Code);
    }

    [Fact]
    public async Task InpaintAsync_EmptyMaskBytes_ReturnsFailure()
    {
        using var http = new HttpClient(
            new MultipartBodyHandler(HttpStatusCode.OK, "{}")
        );
        var provider = CreateProvider();
        var adapter = new StabilityAiAdapter(http, new FakeKeyVault(), provider, new FakeLogger());

        var request = new InpaintRequest(
            Guid.NewGuid(),
            "sd3.5-large",
            "prompt",
            new byte[] { 0x01 },
            Array.Empty<byte>(),
            ImageSize.Square1024
        );

        var result = await adapter.InpaintAsync(request);

        Assert.True(result.IsFailure);
        Assert.Equal("STABILITY_INPAINT_MISSING_INPUT", result.Error.Code);
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

    private class CountCapturingMultipartHandler(Func<HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken ct
        )
        {
            LastRequest = request;
            return Task.FromResult(responseFactory());
        }
    }
}
