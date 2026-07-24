using System.Text.Json;
using HydraForge.Tui.Models;

namespace HydraForge.Tui.Services;

public interface IConfigStore
{
    Task<Config> LoadAsync();
    Task SaveAsync(Config config);
}

public class ConfigStore : IConfigStore
{
    private readonly string _configPath;

    public ConfigStore()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _configPath = Path.Combine(appDataPath, "HydraForge", "config.json");
    }

    public async Task<Config> LoadAsync()
    {
        if (!File.Exists(_configPath))
        {
            return new Config();
        }

        var json = await File.ReadAllTextAsync(_configPath);
        var config = JsonSerializer.Deserialize<Config>(json, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        return config ?? new Config();
    }

    public async Task SaveAsync(Config config)
    {
        var directory = Path.GetDirectoryName(_configPath);
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        });

        await File.WriteAllTextAsync(_configPath, json);
    }
}