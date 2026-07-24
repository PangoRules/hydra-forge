using System.Runtime.InteropServices;
using System.Text.Json;
using HydraForge.Tui.Models;

namespace HydraForge.Tui.Services;

public class ConfigStore
{
    private static readonly string ConfigDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".config",
        "hydraforge"
    );

    private static readonly string ConfigPath = Path.Combine(ConfigDir, "config.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public TuiConfig Load()
    {
        if (!File.Exists(ConfigPath))
            return new TuiConfig();

        var json = File.ReadAllText(ConfigPath);
        return JsonSerializer.Deserialize<TuiConfig>(json, JsonOptions) ?? new TuiConfig();
    }

    public void Save(TuiConfig config)
    {
        Directory.CreateDirectory(ConfigDir);

        var json = JsonSerializer.Serialize(config, JsonOptions);
        File.WriteAllText(ConfigPath, json);

        // Set 0600 permissions on POSIX
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            File.SetUnixFileMode(ConfigPath,
                UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }

    public void Clear()
    {
        if (File.Exists(ConfigPath))
            File.Delete(ConfigPath);
    }
}