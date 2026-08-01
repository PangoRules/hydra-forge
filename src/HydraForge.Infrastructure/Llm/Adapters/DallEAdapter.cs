namespace HydraForge.Infrastructure.Llm.Adapters;

using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using HydraForge.Application.Llm;
using HydraForge.Domain.Common;
using HydraForge.Domain.Entities.Admin;
using HydraForge.Domain.Enums;
using Microsoft.Extensions.Logging;

public sealed class DallEAdapter(
    HttpClient http,
    IKeyVault keyVault,
    LlmProvider provider,
    ILogger<DallEAdapter> logger
) : IImageClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public AdapterType AdapterType => AdapterType.DallE;

    public async Task<Result<GeneratedImage>> GenerateImageAsync(
        ImageRequest request,
        CancellationToken ct = default
    )
    {
        var baseUrl = provider.BaseUrl.TrimEnd('/');

        var body = new DallEGenerateRequest
        {
            Model = request.ModelId,
            Prompt = request.Prompt,
            N = request.Count,
            Size = MapSize(request.Size),
            ResponseFormat = "url",
        };

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/images/generations")
        {
            Content = JsonContent.Create(body, options: JsonOptions),
        };

        AddAuthHeader(httpRequest);

        using var response = await http.SendAsync(httpRequest, ct);
        if (!response.IsSuccessStatusCode)
        {
            var statusCode = response.StatusCode;
            var responseBody = await response.Content.ReadAsStringAsync(ct);
            logger.LogError(
                "DallE API error {StatusCode}: {ResponseBody}",
                statusCode,
                responseBody
            );
            return Result<GeneratedImage>.Failure(
                new Error("DALLE_GENERATE_FAILED", $"DallE image generation failed: {statusCode}")
            );
        }

        return await ParseImageResponseAsync(response, request.Size, ct);
    }

    public async Task<Result<GeneratedImage>> InpaintAsync(
        InpaintRequest request,
        CancellationToken ct = default
    )
    {
        if (request.ImageBytes is null || request.ImageBytes.Length == 0)
        {
            return Result<GeneratedImage>.Failure(
                new Error("DALLE_INPAINT_MISSING_INPUT", "ImageBytes is required for inpainting.")
            );
        }

        if (request.MaskBytes is null || request.MaskBytes.Length == 0)
        {
            return Result<GeneratedImage>.Failure(
                new Error("DALLE_INPAINT_MISSING_INPUT", "MaskBytes is required for inpainting.")
            );
        }

        var baseUrl = provider.BaseUrl.TrimEnd('/');

        var multipart = new MultipartFormDataContent();

        var imageContent = new ByteArrayContent(request.ImageBytes);
        imageContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        multipart.Add(imageContent, "image", "image.png");

        var maskContent = new ByteArrayContent(request.MaskBytes);
        maskContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        multipart.Add(maskContent, "mask", "mask.png");

        multipart.Add(new StringContent(request.Prompt), "prompt");
        multipart.Add(new StringContent(request.ModelId), "model");
        multipart.Add(new StringContent(request.Count.ToString()), "n");
        multipart.Add(new StringContent(MapSize(request.Size)), "size");

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/images/edits")
        {
            Content = multipart,
        };

        AddAuthHeader(httpRequest);

        using var response = await http.SendAsync(httpRequest, ct);
        if (!response.IsSuccessStatusCode)
        {
            var statusCode = response.StatusCode;
            var responseBody = await response.Content.ReadAsStringAsync(ct);
            logger.LogError(
                "DallE API error {StatusCode}: {ResponseBody}",
                statusCode,
                responseBody
            );
            return Result<GeneratedImage>.Failure(
                new Error("DALLE_INPAINT_FAILED", $"DallE inpaint failed: {statusCode}")
            );
        }

        return await ParseImageResponseAsync(response, request.Size, ct);
    }

    private void AddAuthHeader(HttpRequestMessage httpRequest)
    {
        if (!string.IsNullOrWhiteSpace(provider.ApiKeyEncrypted))
        {
            var apiKey = keyVault.Decrypt(provider.ApiKeyEncrypted);
            httpRequest.Headers.Add("Authorization", $"Bearer {apiKey}");
        }
    }

    private static string MapSize(ImageSize size) =>
        size switch
        {
            ImageSize.Square1024 => "1024x1024",
            ImageSize.Landscape1792 => "1792x1024",
            ImageSize.Portrait1024 => "1024x1792",
            _ => "1024x1024",
        };

    private static async Task<Result<GeneratedImage>> ParseImageResponseAsync(
        HttpResponseMessage response,
        ImageSize size,
        CancellationToken ct
    )
    {
        DallEImageResponse? imageResponse;
        try
        {
            imageResponse = await response.Content.ReadFromJsonAsync<DallEImageResponse>(
                JsonOptions,
                ct
            );
        }
        catch (JsonException ex)
        {
            return Result<GeneratedImage>.Failure(
                new Error("DALLE_PARSE_FAILED", $"Failed to parse DallE response: {ex.Message}")
            );
        }

        if (imageResponse?.Data is null || imageResponse.Data.Count == 0)
        {
            return Result<GeneratedImage>.Failure(
                new Error("DALLE_EMPTY_RESPONSE", "DallE response contained no image data.")
            );
        }

        var urls = new List<string>();
        foreach (var item in imageResponse.Data)
        {
            if (!string.IsNullOrWhiteSpace(item.B64Json))
            {
                urls.Add(item.B64Json);
            }
            else if (!string.IsNullOrWhiteSpace(item.Url))
            {
                urls.Add(item.Url);
            }
        }

        if (urls.Count == 0)
        {
            return Result<GeneratedImage>.Failure(
                new Error(
                    "DALLE_NO_IMAGE_DATA",
                    "DallE response contained no b64_json or url fields."
                )
            );
        }

        var resolution = MapSize(size);
        return Result<GeneratedImage>.Success(new GeneratedImage(urls, resolution));
    }

    private sealed class DallEGenerateRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = "";

        [JsonPropertyName("prompt")]
        public string Prompt { get; set; } = "";

        [JsonPropertyName("n")]
        public int N { get; set; }

        [JsonPropertyName("size")]
        public string Size { get; set; } = "";

        [JsonPropertyName("response_format")]
        public string ResponseFormat { get; set; } = "url";
    }

    private sealed class DallEImageResponse
    {
        [JsonPropertyName("data")]
        public List<DallEImageData> Data { get; set; } = [];
    }

    private sealed class DallEImageData
    {
        [JsonPropertyName("b64_json")]
        public string? B64Json { get; set; }

        [JsonPropertyName("url")]
        public string? Url { get; set; }

        [JsonPropertyName("revised_prompt")]
        public string? RevisedPrompt { get; set; }
    }
}
