using System.Text.Json;
using HydraForge.Application.Llm;

namespace HydraForge.Application.Chat;

public static class ChatMessageMapper
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static string ToDomainImagesJson(IReadOnlyList<ImageBlock>? images)
    {
        if (images is null || images.Count == 0)
            return "[]";

        return JsonSerializer.Serialize(images, JsonOptions);
    }

    public static ImageBlock[] ToApplicationImages(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];

        try
        {
            return JsonSerializer.Deserialize<ImageBlock[]>(json, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
