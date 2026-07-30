using System.Net.Http.Json;
using HydraForge.Application.Notifications;
using HydraForge.Application.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HydraForge.Infrastructure.Notifications;

public class NtfyClient(
    HttpClient http,
    IOptions<NtfyOptions> options,
    ISettingsProvider settingsProvider,
    ILogger<NtfyClient> logger
) : INtfyClient
{
    private readonly HttpClient _http = http;
    private readonly NtfyOptions _options = options.Value;
    private readonly ISettingsProvider _settingsProvider = settingsProvider;
    private readonly ILogger<NtfyClient> _logger = logger;

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
            _logger.LogWarning(
                "Ntfy notification skipped: NtfyServerUrl {Url} is not a valid absolute URL.",
                settings.NtfyServerUrl
            );
            return;
        }

        if (serverUri.Scheme != "https" && serverUri.Scheme != "http")
        {
            _logger.LogWarning(
                "Ntfy notification skipped: scheme {Scheme} in NtfyServerUrl {Url} is not http or https.",
                serverUri.Scheme,
                settings.NtfyServerUrl
            );
            return;
        }

        // Require HTTPS in general. http:// is allowed only for hosts that cannot resolve on
        // the public internet: localhost, 127.0.0.1, and single-label hostnames (no '.') —
        // the latter covers Docker Compose service-name resolution generically, including
        // this repo's own shipped `ntfy` service (reachable at http://ntfy on the compose
        // network), which a same-host dotted hostname could never be by DNS convention.
        var isLoopbackHost = serverUri.Host is "localhost" or "127.0.0.1";
        var isSingleLabelHost = !serverUri.Host.Contains('.');
        if (serverUri.Scheme == "http" && !isLoopbackHost && !isSingleLabelHost)
        {
            _logger.LogWarning(
                "Ntfy notification skipped: http:// is only allowed for localhost, 127.0.0.1, "
                    + "or single-label hostnames. NtfyServerUrl {Url} was rejected.",
                settings.NtfyServerUrl
            );
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
