using System.Net.Http.Json;
using HydraForge.Application.Notifications;
using HydraForge.Application.Settings;
using Microsoft.Extensions.Options;

namespace HydraForge.Infrastructure.Notifications;

public class NtfyClient : INtfyClient
{
    private readonly HttpClient _http;
    private readonly NtfyOptions _options;
    private readonly ISettingsProvider _settingsProvider;

    public NtfyClient(HttpClient http, IOptions<NtfyOptions> options, ISettingsProvider settingsProvider)
    {
        _http = http;
        _options = options.Value;
        _settingsProvider = settingsProvider;
    }

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
