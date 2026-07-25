using HydraForge.Tui.Models;
using HydraForge.Tui.Screens;

namespace HydraForge.Tui.Tests;

public class LockScreenTests
{
    [Fact]
    public async Task OnEnterAsync_WhenHealthCheckSucceedsImmediately_SetsConnectionConnected()
    {
        var appState = new AppState();
        var lockScreen = new LockScreen(appState, () => Task.FromResult(true));

        await lockScreen.OnEnterAsync();
        await WaitUntilAsync(() => appState.Connection == ConnectionStatus.Connected);

        Assert.Equal(ConnectionStatus.Connected, appState.Connection);
    }

    [Fact]
    public async Task OnEnterAsync_WhenHealthCheckThrows_SwallowsExceptionAndSetsReconnecting()
    {
        var appState = new AppState();
        var lockScreen = new LockScreen(appState, () => throw new HttpRequestException("connection refused"));

        await lockScreen.OnEnterAsync();
        await WaitUntilAsync(() => appState.Connection == ConnectionStatus.Reconnecting);

        Assert.Equal(ConnectionStatus.Reconnecting, appState.Connection);

        await lockScreen.OnExitAsync();
    }

    [Fact]
    public async Task OnExitAsync_WhileRetryLoopRunning_CancelsWithoutThrowing()
    {
        var appState = new AppState();
        var lockScreen = new LockScreen(appState, () => Task.FromResult(false));

        await lockScreen.OnEnterAsync();
        await WaitUntilAsync(() => appState.Connection == ConnectionStatus.Reconnecting);

        var exception = await Record.ExceptionAsync(() => lockScreen.OnExitAsync());

        Assert.Null(exception);
    }

    [Fact]
    public async Task RenderAsync_DoesNotThrow()
    {
        var appState = new AppState();
        var lockScreen = new LockScreen(appState, () => Task.FromResult(false));

        var exception = await Record.ExceptionAsync(lockScreen.RenderAsync);

        Assert.Null(exception);
    }

    [Fact]
    public async Task HandleKeyAsync_WithNonQuitKey_IsNoOp()
    {
        var appState = new AppState();
        var lockScreen = new LockScreen(appState, () => Task.FromResult(false));
        var key = new ConsoleKeyInfo('a', ConsoleKey.A, false, false, false);

        var exception = await Record.ExceptionAsync(() => lockScreen.HandleKeyAsync(key));

        Assert.Null(exception);
    }

    private static async Task WaitUntilAsync(Func<bool> condition, int timeoutMs = 2000)
    {
        var start = DateTime.UtcNow;
        while (!condition() && (DateTime.UtcNow - start).TotalMilliseconds < timeoutMs)
            await Task.Delay(10);
    }
}
