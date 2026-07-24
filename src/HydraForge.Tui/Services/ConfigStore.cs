using System.Runtime.InteropServices;
using System.Text.Json;
using HydraForge.Tui.Models;

namespace HydraForge.Tui.Services;

public class ConfigStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _configDir;
    private readonly string _configPath;

    public ConfigStore() : this(Path.Combine(
        AppContext.BaseDirectory,
        "..", "..", "..",
        ".hydraforge"))
    {
    }

    // Internal ctor lets tests point at a throwaway directory instead of the real ~/.config/hydraforge.
    internal ConfigStore(string configDir)
    {
        _configDir = configDir;
        _configPath = Path.Combine(configDir, "config.json");
    }

    public virtual TuiConfig Load()
    {
        if (!File.Exists(_configPath))
            return new TuiConfig();

        var json = File.ReadAllText(_configPath);
        return JsonSerializer.Deserialize<TuiConfig>(json, JsonOptions) ?? new TuiConfig();
    }

    public virtual void Save(TuiConfig config)
    {
        Directory.CreateDirectory(_configDir);

        var json = JsonSerializer.Serialize(config, JsonOptions);
        File.WriteAllText(_configPath, json);

        // Set 0600 permissions on POSIX
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            File.SetUnixFileMode(_configPath,
                UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }

    public virtual void Clear()
    {
        if (File.Exists(_configPath))
            File.Delete(_configPath);
    }
}