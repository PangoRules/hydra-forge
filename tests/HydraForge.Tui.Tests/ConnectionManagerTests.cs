using System.Net;
using HydraForge.Tui.Models;
using HydraForge.Tui.Screens;
using HydraForge.Tui.Services;

namespace HydraForge.Tui.Tests;

public class ConnectionManagerTests : IDisposable
{
    private readonly string _configDir;
    private readonly ConfigStore _configStore;
    private readonly AppState _appState;

    public ConnectionManagerTests()
    {
        _configDir = Path.Combine(Path.GetTempPath(), "hydraforge-connectionmanager-tests-" + Guid.NewGuid());
        _configStore = new ConfigStore(_configDir);
        _configStore.Save(new TuiConfig { ServerUrl = "https://example.test/" });
        _appState = new AppState();
    }

    public void Dispose()
    {
        if (Directory.Exists(_configDir))
            Directory.Delete(_configDir, recursive: true);
    }

    private ConnectionManager CreateManager(Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        var fake = new FakeHttpMessageHandler(responder);
        var factory = new ApiClientFactory(_configStore, new ErrorCollector(), fake);
        return new ConnectionManager(_appState, factory, new ErrorCollector());
    }

    [Fact]
    public async Task CheckHealthAsync_WhenServerReturns200_ReturnsTrue()
    {
        var manager = CreateManager(_ => new HttpResponseMessage(HttpStatusCode.OK));

        var healthy = await manager.CheckHealthAsync();

        Assert.True(healthy);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenServerReturnsNon200_ReturnsFalse()
    {
        var manager = CreateManager(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

        var healthy = await manager.CheckHealthAsync();

        Assert.False(healthy);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenTransportThrows_ReturnsFalseWithoutLeakingException()
    {
        var manager = CreateManager(_ => throw new HttpRequestException("connection refused"));

        var healthy = await manager.CheckHealthAsync();

        Assert.False(healthy);
    }

    [Fact]
    public void CreateLockScreen_ReturnsLockScreenWiredToAppState()
    {
        var manager = CreateManager(_ => new HttpResponseMessage(HttpStatusCode.OK));

        var lockScreen = manager.CreateLockScreen();

        Assert.IsType<LockScreen>(lockScreen);
    }

    [Fact]
    public async Task WaitForConnectionAsync_WhenServerHealthyImmediately_ReturnsTrueAndSetsConnected()
    {
        var manager = CreateManager(_ => new HttpResponseMessage(HttpStatusCode.OK));

        var connected = await manager.WaitForConnectionAsync(timeoutMs: 5000);

        Assert.True(connected);
        Assert.Equal(ConnectionStatus.Connected, _appState.Connection);
    }

    [Fact]
    public async Task WaitForConnectionAsync_WithZeroTimeout_ReturnsFalseImmediatelyAndSetsDisconnected()
    {
        var manager = CreateManager(_ => throw new InvalidOperationException("must not be called"));

        var connected = await manager.WaitForConnectionAsync(timeoutMs: 0);

        Assert.False(connected);
        Assert.Equal(ConnectionStatus.Disconnected, _appState.Connection);
    }

    [Fact]
    public async Task WaitForConnectionAsync_WhenServerUnreachable_ReturnsFalseAfterTimeoutAndSetsDisconnected()
    {
        var manager = CreateManager(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

        var connected = await manager.WaitForConnectionAsync(timeoutMs: 500);

        Assert.False(connected);
        Assert.Equal(ConnectionStatus.Disconnected, _appState.Connection);
    }
}
