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

public sealed class StabilityAiAdapter(
    HttpClient http,
    IKeyVault keyVault,
    LlmProvider provider,
    ILogger<StabilityAiAdapter> logger
) : IImageClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public AdapterType AdapterType => AdapterType.StabilityAi;

    public async Task<Result<GeneratedImage>> GenerateImageAsync(
        ImageRequest request,
        CancellationToken ct = default
    )
    {
        var baseUrl = provider.BaseUrl.TrimEnd('/');
        var engine = request.ModelId;
        var url = $"{baseUrl}/v2beta/stable-image/generate/{engine}";

        var images = new List<string>();

        for (var i = 0; i < request.Count; i++)
        {
            var multipart = new MultipartFormDataContent();

            multipart.Add(new StringContent(request.Prompt), "prompt");
            multipart.Add(new StringContent(string.Empty), "negative_prompt");
            multipart.Add(new StringContent(MapAspectRatio(request.Size)), "aspect_ratio");
            multipart.Add(new StringContent("png"), "output_format");
            multipart.Add(new StringContent("0"), "seed");

            var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = multipart,
            };

            AddAuthHeader(httpRequest);
            httpRequest.Headers.Accept.ParseAdd("application/json");

            using var response = await http.SendAsync(httpRequest, ct);
            if (!response.IsSuccessStatusCode)
            {
                var statusCode = response.StatusCode;
                var responseBody = await response.Content.ReadAsStringAsync(ct);
                logger.LogError(
                    "StabilityAI API error {StatusCode}: {ResponseBody}",
                    statusCode,
                    responseBody
                );
                return Result<GeneratedImage>.Failure(
                    new Error("STABILITY_GENERATE_FAILED", $"StabilityAI image generation failed: {statusCode}")
                );
            }

            var imageResult = await ParseSingleImageResponseAsync(response, ct);
            if (!imageResult.IsSuccess)
            {
                return imageResult;
            }

            images.AddRange(imageResult.Value.ImageDataUrlsOrKeys);
        }

        return Result<GeneratedImage>.Success(new GeneratedImage(images, "generated"));
    }

    public async Task<Result<GeneratedImage>> InpaintAsync(
        InpaintRequest request,
        CancellationToken ct = default
    )
    {
        if (request.ImageBytes is null || request.ImageBytes.Length == 0)
        {
            return Result<GeneratedImage>.Failure(
                new Error("STABILITY_INPAINT_MISSING_INPUT", "ImageBytes is required for inpainting.")
            );
        }

        if (request.MaskBytes is null || request.MaskBytes.Length == 0)
        {
            return Result<GeneratedImage>.Failure(
                new Error("STABILITY_INPAINT_MISSING_INPUT", "MaskBytes is required for inpainting.")
            );
        }

        var baseUrl = provider.BaseUrl.TrimEnd('/');
        var url = $"{baseUrl}/v2beta/stable-image/edit/inpaint";

        var multipart = new MultipartFormDataContent();

        var imageContent = new ByteArrayContent(request.ImageBytes);
        imageContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        multipart.Add(imageContent, "image", "image.png");

        var maskContent = new ByteArrayContent(request.MaskBytes);
        maskContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        multipart.Add(maskContent, "mask", "mask.png");

        multipart.Add(new StringContent(request.Prompt), "prompt");
        multipart.Add(new StringContent("png"), "output_format");

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = multipart,
        };

        AddAuthHeader(httpRequest);
        httpRequest.Headers.Accept.ParseAdd("application/json");

        using var response = await http.SendAsync(httpRequest, ct);
        if (!response.IsSuccessStatusCode)
        {
            var statusCode = response.StatusCode;
            var responseBody = await response.Content.ReadAsStringAsync(ct);
            logger.LogError(
                "StabilityAI API error {StatusCode}: {ResponseBody}",
                statusCode,
                responseBody
            );
            return Result<GeneratedImage>.Failure(
                new Error("STABILITY_INPAINT_FAILED", $"StabilityAI inpaint failed: {statusCode}")
            );
        }

        return await ParseSingleImageResponseAsync(response, ct);
    }

    private void AddAuthHeader(HttpRequestMessage httpRequest)
    {
        if (!string.IsNullOrWhiteSpace(provider.ApiKeyEncrypted))
        {
            var apiKey = keyVault.Decrypt(provider.ApiKeyEncrypted);
            httpRequest.Headers.Add("Authorization", $"Bearer {apiKey}");
        }
    }

    private static string MapAspectRatio(ImageSize size) =>
        size switch
        {
            ImageSize.Square1024 => "1:1",
            ImageSize.Landscape1792 => "16:9",
            ImageSize.Portrait1024 => "9:16",
            _ => "1:1",
        };

    private async Task<Result<GeneratedImage>> ParseSingleImageResponseAsync(
        HttpResponseMessage response,
        CancellationToken ct
    )
    {
        var contentType = response.Content.Headers.ContentType?.MediaType;

        if (contentType is not null && contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            var bytes = await response.Content.ReadAsByteArrayAsync(ct);
            var base64 = Convert.ToBase64String(bytes);
            return Result<GeneratedImage>.Success(
                new GeneratedImage([base64], "generated")
            );
        }

        StabilityAiImageResponse? imageResponse;
        try
        {
            imageResponse = await response.Content.ReadFromJsonAsync<StabilityAiImageResponse>(
                JsonOptions,
                ct
            );
        }
        catch (JsonException ex)
        {
            return Result<GeneratedImage>.Failure(
                new Error("STABILITY_PARSE_FAILED", $"Failed to parse StabilityAI response: {ex.Message}")
            );
        }

        if (imageResponse?.Image is null)
        {
            return Result<GeneratedImage>.Failure(
                new Error("STABILITY_EMPTY_RESPONSE", "StabilityAI response contained no image data.")
            );
        }

        return Result<GeneratedImage>.Success(
            new GeneratedImage([imageResponse.Image], "generated")
        );
    }

    private sealed class StabilityAiImageResponse
    {
        [JsonPropertyName("image")]
        public string? Image { get; set; }

        [JsonPropertyName("finish_reason")]
        public string? FinishReason { get; set; }
    }
}
