using System.Net.Http.Json;
using HydraForge.Application.Notifications;
using Microsoft.Extensions.Options;

namespace HydraForge.Infrastructure.Notifications;

public class NtfyClient : INtfyClient
{
    private readonly HttpClient _http;
    private readonly NtfyOptions _options;
    private readonly string? _serverUrl;

    public NtfyClient(HttpClient http, IOptions<NtfyOptions> options, string? serverUrl)
    {
        _http = http;
        _options = options.Value;
        _serverUrl = serverUrl;
    }

    public async Task PublishAsync(Guid userId, string title, string? body, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_serverUrl))
            return;

        var topic = $"hydraforge-{userId}";
        var url = $"{_serverUrl.TrimEnd('/')}/{topic}";

        var payload = new
        {
            topic,
            title,
            message = body ?? title,
            priority = _options.DefaultPriority,
            tags = new[] { "hydraforge" }
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