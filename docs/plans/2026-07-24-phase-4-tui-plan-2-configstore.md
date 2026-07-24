# Plan 2: ConfigStore

**Branch:** `task/tui-configstore`
**Parent branch:** `feat/phase-4-tui`
**Parent spec:** `2026-07-24-phase-4-tui.md` — Task 2

**Goal:** Read/write `~/.config/hydraforge/config.json` with 0600 permissions. Single JSON file for server URL, JWT, expiry, refresh token.

**Depends on:** Task 1 (TuiConfig model exists).

---

## Step 1: Create `ConfigStore` service

Create `src/HydraForge.Tui/Services/ConfigStore.cs`:

```csharp
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
```

## Step 2: Build verification

```bash
dotnet build src/HydraForge.Tui/HydraForge.Tui.csproj
```

Expected: build succeeds.

## Step 3: Commit

```bash
git add src/HydraForge.Tui/Services/ConfigStore.cs
git commit -m "feat(tui): add ConfigStore for ~/.config/hydraforge/config.json with 0600 perms"
```