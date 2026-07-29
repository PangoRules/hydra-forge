using System.Net.Http.Json;
using HydraForge.Application.Notifications;
using Microsoft.Extensions.Options;

namespace HydraForge.Infrastructure.Notifications;

public class NtfyClient(HttpClient http, IOptions<NtfyOptions> options, string? serverUrl = null)
    : INtfyClient
{
    private readonly NtfyOptions _options = options.Value;

    public async Task PublishAsync(
        Guid userId,
        string title,
        string? body,
        CancellationToken ct = default
    )
    {
        if (string.IsNullOrWhiteSpace(serverUrl))
            return;

        var topic = $"hydraforge-{userId}";
        var url = $"{serverUrl.TrimEnd('/')}/{topic}";

        var payload = new
        {
            topic,
            title,
            message = body ?? title,
            priority = _options.DefaultPriority,
            tags = new[] { "hydraforge" },
        };

        try
        {
            await http.PostAsJsonAsync(url, payload, ct);
        }
        catch
        {
            // ntfy is best-effort — never throw on push failure
        }
    }
}
