namespace HydraForge.Infrastructure.Llm.Adapters;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using HydraForge.Application.Llm;
using HydraForge.Domain.Common;
using HydraForge.Domain.Entities.Admin;
using HydraForge.Domain.Enums;
using Microsoft.Extensions.Logging;

public sealed class ComfyUiAdapter(
    HttpClient http,
    IKeyVault keyVault,
    LlmProvider provider,
    ILogger<ComfyUiAdapter> logger
) : IImageClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public AdapterType AdapterType => provider.AdapterType;

    public async Task<Result<GeneratedImage>> GenerateImageAsync(
        ImageRequest request,
        CancellationToken ct = default
    )
    {
        var baseUrl = provider.BaseUrl.TrimEnd('/');
        var (width, height) = MapSize(request.Size);

        var workflow = BuildTextToImageWorkflow(request.ModelId, request.Prompt, width, height);

        var submitResult = await SubmitPromptAsync(baseUrl, workflow, ct);
        if (submitResult.IsFailure)
            return Result<GeneratedImage>.Failure(submitResult.Error);

        var historyResult = await PollHistoryUntilCompleteAsync(baseUrl, submitResult.Value, ct);
        if (historyResult.IsFailure)
            return Result<GeneratedImage>.Failure(historyResult.Error);

        var imagesResult = await ExtractImagesFromHistory(historyResult.Value, baseUrl, ct);
        if (imagesResult.IsFailure)
            return Result<GeneratedImage>.Failure(imagesResult.Error);

        var resolution = $"{width}x{height}";
        return Result<GeneratedImage>.Success(new GeneratedImage(imagesResult.Value, resolution));
    }

    public async Task<Result<GeneratedImage>> InpaintAsync(
        InpaintRequest request,
        CancellationToken ct = default
    )
    {
        if (request.ImageBytes is null || request.ImageBytes.Length == 0)
            return Result<GeneratedImage>.Failure(
                new Error("COMFYUI_INPAINT_MISSING_INPUT", "ImageBytes is required for inpainting.")
            );

        if (request.MaskBytes is null || request.MaskBytes.Length == 0)
            return Result<GeneratedImage>.Failure(
                new Error("COMFYUI_INPAINT_MISSING_INPUT", "MaskBytes is required for inpainting.")
            );

        var baseUrl = provider.BaseUrl.TrimEnd('/');
        var (width, height) = MapSize(request.Size);

        var imageUpload = await UploadSingleImageAsync(
            baseUrl,
            request.ImageBytes,
            "image.png",
            ct
        );
        if (imageUpload.IsFailure)
            return Result<GeneratedImage>.Failure(imageUpload.Error);

        var maskUpload = await UploadSingleImageAsync(baseUrl, request.MaskBytes, "mask.png", ct);
        if (maskUpload.IsFailure)
            return Result<GeneratedImage>.Failure(maskUpload.Error);

        var workflow = BuildInpaintWorkflow(
            request.ModelId,
            request.Prompt,
            width,
            height,
            imageUpload.Value.Name,
            imageUpload.Value.Subfolder,
            maskUpload.Value.Name,
            maskUpload.Value.Subfolder
        );

        var submitResult = await SubmitPromptAsync(baseUrl, workflow, ct);
        if (submitResult.IsFailure)
            return Result<GeneratedImage>.Failure(submitResult.Error);

        var historyResult = await PollHistoryUntilCompleteAsync(baseUrl, submitResult.Value, ct);
        if (historyResult.IsFailure)
            return Result<GeneratedImage>.Failure(historyResult.Error);

        var imagesResult = await ExtractImagesFromHistory(historyResult.Value, baseUrl, ct);
        if (imagesResult.IsFailure)
            return Result<GeneratedImage>.Failure(imagesResult.Error);

        var resolution = $"{width}x{height}";
        return Result<GeneratedImage>.Success(new GeneratedImage(imagesResult.Value, resolution));
    }

    private async Task<Result<string>> SubmitPromptAsync(
        string baseUrl,
        Dictionary<string, object> workflow,
        CancellationToken ct
    )
    {
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/prompt")
        {
            Content = JsonContent.Create(new { prompt = workflow }, options: JsonOptions),
        };

        AddAuthHeader(httpRequest);

        using var response = await http.SendAsync(httpRequest, ct);
        if (!response.IsSuccessStatusCode)
        {
            var statusCode = response.StatusCode;
            var responseBody = await response.Content.ReadAsStringAsync(ct);
            logger.LogError(
                "ComfyUI prompt submission failed {StatusCode}: {ResponseBody}",
                statusCode,
                responseBody
            );
            return Result<string>.Failure(
                new Error(
                    "COMFYUI_SUBMIT_FAILED",
                    $"ComfyUI prompt submission failed: {statusCode}"
                )
            );
        }

        ComfyUiPromptResponse? promptResponse;
        try
        {
            promptResponse = await response.Content.ReadFromJsonAsync<ComfyUiPromptResponse>(
                JsonOptions,
                ct
            );
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Failed to parse ComfyUI prompt response");
            return Result<string>.Failure(
                new Error(
                    "COMFYUI_PARSE_FAILED",
                    $"Failed to parse ComfyUI prompt response: {ex.Message}"
                )
            );
        }

        if (string.IsNullOrWhiteSpace(promptResponse?.PromptId))
        {
            logger.LogError("ComfyUI prompt response missing prompt_id");
            return Result<string>.Failure(
                new Error("COMFYUI_EMPTY_RESPONSE", "ComfyUI prompt response missing prompt_id.")
            );
        }

        return Result<string>.Success(promptResponse.PromptId);
    }

    private async Task<Result<ComfyUiHistoryResponse>> PollHistoryUntilCompleteAsync(
        string baseUrl,
        string promptId,
        CancellationToken ct
    )
    {
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

        var pollInterval = TimeSpan.FromSeconds(2);

        while (!linkedCts.Token.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(pollInterval, linkedCts.Token);
            }
            catch (OperationCanceledException) when (timeoutCts.Token.IsCancellationRequested)
            {
                return Result<ComfyUiHistoryResponse>.Failure(
                    new Error(
                        "COMFYUI_HISTORY_TIMEOUT",
                        "ComfyUI image generation timed out after 5 minutes."
                    )
                );
            }
            catch (OperationCanceledException)
            {
                throw;
            }

            var historyRequest = new HttpRequestMessage(
                HttpMethod.Get,
                $"{baseUrl}/history/{promptId}"
            );
            AddAuthHeader(historyRequest);

            using var historyResponse = await http.SendAsync(historyRequest, linkedCts.Token);
            if (!historyResponse.IsSuccessStatusCode)
            {
                var body = await historyResponse.Content.ReadAsStringAsync(linkedCts.Token);
                logger.LogError(
                    "ComfyUI history request failed {StatusCode}: {Body}",
                    historyResponse.StatusCode,
                    body
                );
                return Result<ComfyUiHistoryResponse>.Failure(
                    new Error(
                        "COMFYUI_HISTORY_FAILED",
                        $"ComfyUI history request failed: {historyResponse.StatusCode}"
                    )
                );
            }

            ComfyUiHistoryResponse? history;
            try
            {
                history = await historyResponse.Content.ReadFromJsonAsync<ComfyUiHistoryResponse>(
                    JsonOptions,
                    linkedCts.Token
                );
            }
            catch (JsonException ex)
            {
                logger.LogError(ex, "Failed to parse ComfyUI history response");
                return Result<ComfyUiHistoryResponse>.Failure(
                    new Error(
                        "COMFYUI_PARSE_FAILED",
                        $"Failed to parse ComfyUI history: {ex.Message}"
                    )
                );
            }

            if (history is null || !history.TryGetValue(promptId, out var promptHistory))
            {
                continue;
            }

            if (promptHistory.Status?.Error is not null)
            {
                logger.LogError(
                    "ComfyUI generation error: {Error}",
                    promptHistory.Status.Error.Message
                );
                return Result<ComfyUiHistoryResponse>.Failure(
                    new Error(
                        "COMFYUI_GENERATION_ERROR",
                        $"ComfyUI generation error: {promptHistory.Status.Error.Message}"
                    )
                );
            }

            if (promptHistory.Status?.Executed == true || promptHistory.Outputs.Count > 0)
            {
                return Result<ComfyUiHistoryResponse>.Success(history);
            }
        }

        return Result<ComfyUiHistoryResponse>.Failure(
            new Error(
                "COMFYUI_HISTORY_TIMEOUT",
                "ComfyUI image generation timed out after 5 minutes."
            )
        );
    }

    private async Task<Result<IReadOnlyList<string>>> ExtractImagesFromHistory(
        ComfyUiHistoryResponse history,
        string baseUrl,
        CancellationToken ct
    )
    {
        var images = new List<string>();

        foreach (var pid in history.Keys)
        {
            var promptHistory = history[pid];
            foreach (var nodeOutput in promptHistory.Outputs.Values)
            {
                foreach (var imageInfo in nodeOutput.Images)
                {
                    var downloadUrl =
                        $"{baseUrl}/view?filename={Uri.EscapeDataString(imageInfo.Filename)}&subfolder={Uri.EscapeDataString(imageInfo.Subfolder)}&type={Uri.EscapeDataString(imageInfo.Type)}";

                    var downloadRequest = new HttpRequestMessage(HttpMethod.Get, downloadUrl);
                    AddAuthHeader(downloadRequest);

                    using var imgResponse = await http.SendAsync(downloadRequest, ct);
                    if (!imgResponse.IsSuccessStatusCode)
                    {
                        logger.LogWarning(
                            "Failed to download image {Filename}: {StatusCode}",
                            imageInfo.Filename,
                            imgResponse.StatusCode
                        );
                        continue;
                    }

                    var imageBytes = await imgResponse.Content.ReadAsByteArrayAsync(ct);
                    var base64 = Convert.ToBase64String(imageBytes);
                    images.Add(base64);
                }
            }
        }

        if (images.Count == 0)
        {
            return Result<IReadOnlyList<string>>.Failure(
                new Error("COMFYUI_NO_IMAGES", "ComfyUI history contained no output images.")
            );
        }

        return Result<IReadOnlyList<string>>.Success(images);
    }

    private async Task<Result<(string Name, string Subfolder)>> UploadSingleImageAsync(
        string baseUrl,
        byte[] bytes,
        string fileName,
        CancellationToken ct
    )
    {
        var content = new MultipartFormDataContent();
        var imageContent = new ByteArrayContent(bytes);
        imageContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(imageContent, "image", fileName);
        content.Add(new StringContent("true"), "overwrite");

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/upload/image")
        {
            Content = content,
        };
        AddAuthHeader(httpRequest);

        using var response = await http.SendAsync(httpRequest, ct);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            logger.LogError(
                "ComfyUI image upload failed {StatusCode}: {Body}",
                response.StatusCode,
                body
            );
            return Result<(string, string)>.Failure(
                new Error(
                    "COMFYUI_UPLOAD_FAILED",
                    $"ComfyUI image upload failed: {response.StatusCode}"
                )
            );
        }

        ComfyUiUploadResponse? uploadResult;
        try
        {
            uploadResult = await response.Content.ReadFromJsonAsync<ComfyUiUploadResponse>(
                JsonOptions,
                ct
            );
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Failed to parse ComfyUI upload response");
            return Result<(string, string)>.Failure(
                new Error(
                    "COMFYUI_PARSE_FAILED",
                    $"Failed to parse ComfyUI upload response: {ex.Message}"
                )
            );
        }

        if (uploadResult is null)
        {
            return Result<(string, string)>.Failure(
                new Error("COMFYUI_EMPTY_RESPONSE", "ComfyUI upload response was empty.")
            );
        }

        return Result<(string, string)>.Success((uploadResult.Name, uploadResult.Subfolder ?? ""));
    }

    private void AddAuthHeader(HttpRequestMessage httpRequest)
    {
        if (!string.IsNullOrWhiteSpace(provider.ApiKeyEncrypted))
        {
            var apiKey = keyVault.Decrypt(provider.ApiKeyEncrypted);
            httpRequest.Headers.Add("Authorization", $"Bearer {apiKey}");
        }
    }

    private static (int Width, int Height) MapSize(ImageSize size) =>
        size switch
        {
            ImageSize.Square1024 => (1024, 1024),
            ImageSize.Landscape1792 => (1792, 1024),
            ImageSize.Portrait1024 => (1024, 1792),
            _ => (1024, 1024),
        };

    private static Dictionary<string, object> BuildTextToImageWorkflow(
        string modelId,
        string prompt,
        int width,
        int height
    )
    {
        var workflow = new Dictionary<string, object>();

        workflow["3"] = new Dictionary<string, object>
        {
            ["class_type"] = "CheckpointLoaderSimple",
            ["inputs"] = new Dictionary<string, object> { ["ckpt_name"] = modelId },
        };

        workflow["4"] = new Dictionary<string, object>
        {
            ["class_type"] = "CLIPTextEncode",
            ["inputs"] = new Dictionary<string, object>
            {
                ["text"] = prompt,
                ["clip"] = new Dictionary<string, object> { ["node_id"] = "3" },
            },
        };

        workflow["5"] = new Dictionary<string, object>
        {
            ["class_type"] = "CLIPTextEncode",
            ["inputs"] = new Dictionary<string, object>
            {
                ["text"] = "bad quality",
                ["clip"] = new Dictionary<string, object> { ["node_id"] = "3" },
            },
        };

        workflow["6"] = new Dictionary<string, object>
        {
            ["class_type"] = "EmptyLatentImage",
            ["inputs"] = new Dictionary<string, object>
            {
                ["batch_size"] = 1,
                ["height"] = height,
                ["width"] = width,
            },
        };

        workflow["7"] = new Dictionary<string, object>
        {
            ["class_type"] = "KSampler",
            ["inputs"] = new Dictionary<string, object>
            {
                ["cfg"] = 8.0,
                ["denoise"] = 1.0,
                ["model"] = new Dictionary<string, object> { ["node_id"] = "3" },
                ["negative"] = new Dictionary<string, object> { ["node_id"] = "5" },
                ["positive"] = new Dictionary<string, object> { ["node_id"] = "4" },
                ["seed"] = 0,
                ["steps"] = 20,
                ["sampler_name"] = "euler",
            },
        };

        workflow["8"] = new Dictionary<string, object>
        {
            ["class_type"] = "VAEDecode",
            ["inputs"] = new Dictionary<string, object>
            {
                ["samples"] = new Dictionary<string, object> { ["node_id"] = "7" },
                ["vae"] = new Dictionary<string, object> { ["node_id"] = "3" },
            },
        };

        workflow["9"] = new Dictionary<string, object>
        {
            ["class_type"] = "SaveImage",
            ["inputs"] = new Dictionary<string, object>
            {
                ["filename_prefix"] = "hydraforge",
                ["images"] = new Dictionary<string, object> { ["node_id"] = "8" },
            },
        };

        return workflow;
    }

    private static Dictionary<string, object> BuildInpaintWorkflow(
        string modelId,
        string prompt,
        int width,
        int height,
        string imageName,
        string imageSubfolder,
        string maskName,
        string maskSubfolder
    )
    {
        var workflow = new Dictionary<string, object>();

        workflow["3"] = new Dictionary<string, object>
        {
            ["class_type"] = "CheckpointLoaderSimple",
            ["inputs"] = new Dictionary<string, object> { ["ckpt_name"] = modelId },
        };

        workflow["4"] = new Dictionary<string, object>
        {
            ["class_type"] = "LoadImage",
            ["inputs"] = new Dictionary<string, object> { ["image"] = $"{imageName}:{maskName}" },
        };

        workflow["5"] = new Dictionary<string, object>
        {
            ["class_type"] = "CLIPTextEncode",
            ["inputs"] = new Dictionary<string, object>
            {
                ["text"] = prompt,
                ["clip"] = new Dictionary<string, object> { ["node_id"] = "3" },
            },
        };

        workflow["6"] = new Dictionary<string, object>
        {
            ["class_type"] = "CLIPTextEncode",
            ["inputs"] = new Dictionary<string, object>
            {
                ["text"] = "bad quality",
                ["clip"] = new Dictionary<string, object> { ["node_id"] = "3" },
            },
        };

        workflow["7"] = new Dictionary<string, object>
        {
            ["class_type"] = "VAEEncodeForInpaint",
            ["inputs"] = new Dictionary<string, object>
            {
                ["bbox"] = new Dictionary<string, object> { ["node_id"] = "4" },
                ["conditioning"] = new Dictionary<string, object> { ["node_id"] = "5" },
                ["latent"] = new Dictionary<string, object> { ["node_id"] = "10" },
                ["mask"] = new Dictionary<string, object> { ["node_id"] = "4" },
                ["vae"] = new Dictionary<string, object> { ["node_id"] = "3" },
            },
        };

        workflow["10"] = new Dictionary<string, object>
        {
            ["class_type"] = "EmptyLatentImage",
            ["inputs"] = new Dictionary<string, object>
            {
                ["batch_size"] = 1,
                ["height"] = height,
                ["width"] = width,
            },
        };

        workflow["11"] = new Dictionary<string, object>
        {
            ["class_type"] = "KSampler",
            ["inputs"] = new Dictionary<string, object>
            {
                ["cfg"] = 8.0,
                ["denoise"] = 0.9,
                ["model"] = new Dictionary<string, object> { ["node_id"] = "3" },
                ["negative"] = new Dictionary<string, object> { ["node_id"] = "6" },
                ["positive"] = new Dictionary<string, object> { ["node_id"] = "7" },
                ["seed"] = 0,
                ["steps"] = 20,
                ["sampler_name"] = "euler",
            },
        };

        workflow["12"] = new Dictionary<string, object>
        {
            ["class_type"] = "VAEDecode",
            ["inputs"] = new Dictionary<string, object>
            {
                ["samples"] = new Dictionary<string, object> { ["node_id"] = "11" },
                ["vae"] = new Dictionary<string, object> { ["node_id"] = "3" },
            },
        };

        workflow["13"] = new Dictionary<string, object>
        {
            ["class_type"] = "SaveImage",
            ["inputs"] = new Dictionary<string, object>
            {
                ["filename_prefix"] = "hydraforge_inpaint",
                ["images"] = new Dictionary<string, object> { ["node_id"] = "12" },
            },
        };

        return workflow;
    }

    private sealed class ComfyUiPromptResponse
    {
        [JsonPropertyName("prompt_id")]
        public string PromptId { get; set; } = "";
    }

    private sealed class ComfyUiHistoryResponse : Dictionary<string, ComfyUiPromptHistory>;

    private sealed class ComfyUiPromptHistory
    {
        [JsonPropertyName("status")]
        public ComfyUiStatus? Status { get; set; }

        [JsonPropertyName("outputs")]
        public Dictionary<string, ComfyUiNodeOutput> Outputs { get; set; } = new();
    }

    private sealed class ComfyUiStatus
    {
        [JsonPropertyName("error")]
        public ComfyUiError? Error { get; set; }

        [JsonPropertyName("executing")]
        public bool Executed { get; set; }
    }

    private sealed class ComfyUiError
    {
        [JsonPropertyName("message")]
        public string Message { get; set; } = "";
    }

    private sealed class ComfyUiNodeOutput
    {
        [JsonPropertyName("images")]
        public List<ComfyUiImageInfo> Images { get; set; } = new();
    }

    private sealed class ComfyUiImageInfo
    {
        [JsonPropertyName("filename")]
        public string Filename { get; set; } = "";

        [JsonPropertyName("subfolder")]
        public string Subfolder { get; set; } = "";

        [JsonPropertyName("type")]
        public string Type { get; set; } = "";
    }

    private sealed class ComfyUiUploadResponse
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("subfolder")]
        public string? Subfolder { get; set; }
    }
}
