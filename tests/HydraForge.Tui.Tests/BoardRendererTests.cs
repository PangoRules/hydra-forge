using HydraForge.Tui.Models;
using HydraForge.Tui.Renderers;
using Spectre.Console;
using Spectre.Console.Testing;

namespace HydraForge.Tui.Tests;

public class BoardRendererTests
{
    private static string RenderLayout(Layout layout)
    {
        var console = new TestConsole();
        console.Profile.Width = 80;
        console.Profile.Height = 24;
        console.Write(layout);
        return console.Output;
    }

    [Fact]
    public void BuildLayout_StatusBar_ShowsUnreadCount()
    {
        var renderer = new BoardRenderer();
        var columns = new List<BoardRenderer.ColumnData>();

        var layout = renderer.BuildLayout(
            columns,
            selectedColumn: 0,
            selectedCard: 0,
            projectName: "Test Project",
            totalCards: 0,
            connection: ConnectionStatus.Connected,
            onlineCount: 5,
            errorCount: 2,
            reorderCardId: null,
            unreadCount: 42
        );

        var output = RenderLayout(layout);
        Assert.Contains("42 unread", output);
    }

    [Fact]
    public void BuildLayout_StatusBar_ShowsZeroUnreadCount()
    {
        var renderer = new BoardRenderer();
        var columns = new List<BoardRenderer.ColumnData>();

        var layout = renderer.BuildLayout(
            columns,
            selectedColumn: 0,
            selectedCard: 0,
            projectName: "Test Project",
            totalCards: 0,
            connection: ConnectionStatus.Connected,
            onlineCount: 0,
            errorCount: 0,
            reorderCardId: null,
            unreadCount: 0
        );

        var output = RenderLayout(layout);
        Assert.Contains("0 unread", output);
    }

    [Fact]
    public void BuildLayout_StatusBar_IncludesOnlineAndErrorCounts()
    {
        var renderer = new BoardRenderer();
        var columns = new List<BoardRenderer.ColumnData>();

        var layout = renderer.BuildLayout(
            columns,
            selectedColumn: 0,
            selectedCard: 0,
            projectName: "Test Project",
            totalCards: 0,
            connection: ConnectionStatus.Connected,
            onlineCount: 3,
            errorCount: 7,
            reorderCardId: null,
            unreadCount: 10
        );

        var output = RenderLayout(layout);
        Assert.Contains("3 online", output);
        Assert.Contains("7 errors", output);
        Assert.Contains("10 unread", output);
    }
}
