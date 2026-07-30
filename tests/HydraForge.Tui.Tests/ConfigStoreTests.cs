using System.Runtime.InteropServices;
using HydraForge.Tui.Models;
using HydraForge.Tui.Services;

namespace HydraForge.Tui.Tests;

public class ConfigStoreTests : IDisposable
{
    private readonly string _configDir;
    private readonly string _configPath;
    private readonly ConfigStore _store;

    public ConfigStoreTests()
    {
        _configDir = Path.Combine(
            Path.GetTempPath(),
            "hydraforge-configstore-tests-" + Guid.NewGuid()
        );
        _configPath = Path.Combine(_configDir, "config.json");
        _store = new ConfigStore(_configDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_configDir))
            Directory.Delete(_configDir, recursive: true);
    }

    [Fact]
    public void Load_WhenFileDoesNotExist_ReturnsDefaultConfig()
    {
        var config = _store.Load();

        Assert.Equal("http://localhost:5000", config.ServerUrl);
        Assert.Null(config.JwtToken);
        Assert.Null(config.ExpiresAt);
        Assert.Null(config.RefreshToken);
    }

    [Fact]
    public void Save_CreatesDirectoryAndFile()
    {
        _store.Save(new TuiConfig());

        Assert.True(Directory.Exists(_configDir));
        Assert.True(File.Exists(_configPath));
    }

    [Fact]
    public void Save_WritesCamelCaseIndentedJson()
    {
        _store.Save(new TuiConfig { ServerUrl = "https://example.com" });

        var json = File.ReadAllText(_configPath);

        Assert.Contains("\"serverUrl\"", json);
        Assert.Contains("\n", json);
    }

    [Fact]
    public void Save_OnPosix_SetsFileModeTo0600()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return;

        _store.Save(new TuiConfig());

        var mode = File.GetUnixFileMode(_configPath);
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, mode);
    }

    [Fact]
    public void Load_AfterSave_RoundTripsAllValues()
    {
        var original = new TuiConfig
        {
            ServerUrl = "https://hydraforge.example.com",
            JwtToken = "eyJhbGciOiJIUzI1NiJ9.test.token",
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1),
            RefreshToken = "refresh-abc-123",
        };

        _store.Save(original);
        var loaded = _store.Load();

        Assert.Equal(original.ServerUrl, loaded.ServerUrl);
        Assert.Equal(original.JwtToken, loaded.JwtToken);
        Assert.Equal(original.ExpiresAt, loaded.ExpiresAt);
        Assert.Equal(original.RefreshToken, loaded.RefreshToken);
    }

    [Fact]
    public void Clear_DeletesConfigFile()
    {
        _store.Save(new TuiConfig());

        _store.Clear();

        Assert.False(File.Exists(_configPath));
    }

    [Fact]
    public void Clear_WhenFileDoesNotExist_DoesNotThrowAndFileRemainsAbsent()
    {
        var exception = Record.Exception(() => _store.Clear());

        Assert.Null(exception);
        Assert.False(File.Exists(_configPath));
    }

    [Fact]
    public void Load_WithInvalidJson_ThrowsInsteadOfReturningDefault()
    {
        Directory.CreateDirectory(_configDir);
        File.WriteAllText(_configPath, "{ this is not valid json ");

        Assert.Throws<System.Text.Json.JsonException>(() => _store.Load());
    }

    // D-49: default ctor must resolve to the repo root's .hydraforge/, found by
    // walking up from the test assembly's bin/ dir looking for HydraForge.slnx —
    // not ~/.config or the raw bin/ output directory.
    [Fact]
    public void DefaultConstructor_ResolvesToRepoRootHydraforgeDirectory()
    {
        var repoRoot = FindRepoRootFromTestAssembly();
        var expectedDir = Path.Combine(repoRoot, ".hydraforge");
        var expectedPath = Path.Combine(expectedDir, "config.json");
        var alreadyExisted = File.Exists(expectedPath);
        var backup = alreadyExisted ? File.ReadAllText(expectedPath) : null;

        try
        {
            var store = new ConfigStore();
            store.Save(new TuiConfig { ServerUrl = "https://repo-root-check.test" });

            Assert.True(File.Exists(expectedPath));
            Assert.Equal("https://repo-root-check.test", store.Load().ServerUrl);
        }
        finally
        {
            if (alreadyExisted)
                File.WriteAllText(expectedPath, backup!);
            else
                File.Delete(expectedPath);
        }
    }

    private static string FindRepoRootFromTestAssembly()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "HydraForge.slnx")))
                return dir.FullName;
            dir = dir.Parent;
        }
        throw new InvalidOperationException(
            "HydraForge.slnx not found above test assembly — repo layout assumption broken."
        );
    }
}
