using System.Net.Http.Json;
using HydraForge.Application.Notifications;
using HydraForge.Application.Settings;
using Microsoft.Extensions.Options;

namespace HydraForge.Infrastructure.Notifications;

public class NtfyClient(
    HttpClient http,
    IOptions<NtfyOptions> options,
    ISettingsProvider settingsProvider
) : INtfyClient
{
    private readonly HttpClient _http = http;
    private readonly NtfyOptions _options = options.Value;
    private readonly ISettingsProvider _settingsProvider = settingsProvider;

    public async Task PublishAsync(
        Guid userId,
        string title,
        string? body,
        CancellationToken ct = default
    )
    {
        var settings = await _settingsProvider.GetAsync(ct);
        if (string.IsNullOrWhiteSpace(settings.NtfyServerUrl))
            return;

        // Validate URL scheme
        if (!Uri.TryCreate(settings.NtfyServerUrl, UriKind.Absolute, out var serverUri))
        {
            return;
        }

        if (serverUri.Scheme != "https" && serverUri.Scheme != "http")
        {
            return;
        }

        // In production, require HTTPS (allow http://localhost for dev)
        if (
            serverUri.Scheme == "http"
            && serverUri.Host != "localhost"
            && serverUri.Host != "127.0.0.1"
        )
        {
            return;
        }

        var topic = $"hydraforge-{userId}";
        var url = $"{settings.NtfyServerUrl.TrimEnd('/')}/{topic}";

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
            await _http.PostAsJsonAsync(url, payload, ct);
        }
        catch
        {
            // ntfy is best-effort — never throw on push failure
        }
    }
}
