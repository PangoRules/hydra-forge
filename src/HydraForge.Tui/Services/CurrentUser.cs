using System.Text.Json;

namespace HydraForge.Tui.Services;

// JWT `sub` claim decode — the TUI doesn't persist a decoded user id anywhere
// (ConfigStore only stores the raw token), so this mirrors the Web UI's
// useAuthStore.restoreToken() base64-decode approach on demand.
public static class CurrentUser
{
    public static Guid? GetId()
    {
        var token = new ConfigStore().Load().JwtToken;
        if (string.IsNullOrEmpty(token))
            return null;

        var parts = token.Split('.');
        if (parts.Length < 2)
            return null;

        var payload = parts[1].Replace('-', '+').Replace('_', '/');
        payload = payload.PadRight(payload.Length + ((4 - (payload.Length % 4)) % 4), '=');

        try
        {
            var json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(payload));
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty("sub", out var sub)
                && Guid.TryParse(sub.GetString(), out var id)
                ? id
                : null;
        }
        catch
        {
            return null;
        }
    }
}
