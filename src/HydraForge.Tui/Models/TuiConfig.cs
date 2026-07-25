namespace HydraForge.Tui.Models;

public class TuiConfig
{
    public string ServerUrl { get; set; } = "http://localhost:5000";
    public string? JwtToken { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public string? RefreshToken { get; set; }
}