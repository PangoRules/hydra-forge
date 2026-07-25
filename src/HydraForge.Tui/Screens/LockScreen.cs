using HydraForge.Tui.Models;
using HydraForge.Tui.Rendering;
using Spectre.Console;

namespace HydraForge.Tui.Screens;

public class LockScreen : IScreen
{
    private readonly AppState _appState;
    private readonly Func<Task<bool>> _healthCheck;
    private CancellationTokenSource? _retryCts;
    private int _retryAttempt;

    public LockScreen(AppState appState, Func<Task<bool>> healthCheck)
    {
        _appState = appState;
        _healthCheck = healthCheck;
    }

    public Task OnEnterAsync()
    {
        _retryAttempt = 0;
        _retryCts = new CancellationTokenSource();
        _ = RetryLoopAsync(_retryCts.Token);
        return Task.CompletedTask;
    }

    public Task OnExitAsync()
    {
        _retryCts?.Cancel();
        return Task.CompletedTask;
    }

    public Task RenderAsync()
    {
        AnsiConsole.Clear();

        var panel = new Panel(
            Align.Center(
                new Rows(
                    new Markup("[yellow]⚠ Server Unreachable[/]"),
                    new Markup("[grey]Retrying...[/]"),
                    new Markup($"[grey]Attempt {_retryAttempt}[/]")
                )
            )
        )
        {
            Border = BoxBorder.Heavy,
            BorderStyle = new Style(foreground: Color.Yellow),
            Header = new PanelHeader(" HydraForge "),
        };

        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[grey]Press [bold]q[/] to quit, [bold]?[/] for help[/]");

        return Task.CompletedTask;
    }

    public async Task HandleKeyAsync(ConsoleKeyInfo key)
    {
        if (key.Key == ConsoleKey.Q)
        {
            var confirm = AnsiConsole.Confirm("Quit HydraForge?");
            if (confirm)
            {
                _retryCts?.Cancel();
                Environment.Exit(0);
            }
            return;
        }

        if (key.KeyChar == '?')
        {
            ShowHelp();
            await RenderAsync();
        }
    }

    private static void ShowHelp() =>
        HelpOverlay.Show(
            "Server Unreachable",
            [
                ("q", "Quit"),
                ("?", "This help"),
            ]
        );

    private async Task RetryLoopAsync(CancellationToken ct)
    {
        int[] delays = [5000, 10000, 30000, 60000];

        while (!ct.IsCancellationRequested)
        {
            _retryAttempt++;

            try
            {
                var healthy = await _healthCheck();
                if (healthy)
                {
                    _appState.Connection = ConnectionStatus.Connected;
                    return; // Lock screen dismissed by caller
                }
            }
            catch
            {
                // Still unreachable
            }

            _appState.Connection = ConnectionStatus.Reconnecting;

            var delay = _retryAttempt <= delays.Length
                ? delays[_retryAttempt - 1]
                : 60000;

            await Task.Delay(delay, ct);
        }
    }
}
