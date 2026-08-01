using HydraForge.Tui.Models;
using HydraForge.Tui.Renderers;
using Spectre.Console;
using Spectre.Console.Testing;

namespace HydraForge.Tui.Tests;

public class BoardRendererTests
{
    // BuildLayout reads AnsiConsole.Profile.Width/Height directly, so the TestConsole must
    // be installed as AnsiConsole.Console BEFORE calling BuildLayout — not after, which is
    // how these tests originally worked (built against whatever ambient profile the test
    // runner happened to have, then rendered into an unrelated 80x24 console).
    private static string RenderLayout(Func<Layout> buildLayout, int width = 80, int height = 24)
    {
        var console = new TestConsole();
        console.Profile.Width = width;
        console.Profile.Height = height;
        AnsiConsole.Console = console;

        console.Write(buildLayout());
        return console.Output;
    }

    private static readonly List<BoardRenderer.ColumnData> DefaultColumns = [];

    private static readonly string[] BoardHints =
    [
        "[h/l] Columns",
        "[j/k] Cards",
        "[Enter] Detail",
        "[n] New",
        "[e] Edit",
        "[m] Move",
        "[r] Reorder",
        "[u] Notifications",
        "[Del] Archive",
        "[x] Errors",
        "[?] Help",
        "[Esc] Back",
        "[q] Quit",
    ];

    [Fact]
    public void BuildLayout_StatusBar_ShowsUnreadCount()
    {
        var output = RenderLayout(() =>
            BoardRenderer.BuildLayout(
                DefaultColumns,
                selectedColumn: 0,
                selectedCard: 0,
                projectName: "Test Project",
                totalCards: 0,
                connection: ConnectionStatus.Connected,
                onlineCount: 5,
                errorCount: 2,
                reorderCardId: null,
                unreadCount: 42
            )
        );

        Assert.Contains("42 unread", output);
    }

    [Fact]
    public void BuildLayout_StatusBar_ShowsZeroUnreadCount()
    {
        var output = RenderLayout(() =>
            BoardRenderer.BuildLayout(
                DefaultColumns,
                selectedColumn: 0,
                selectedCard: 0,
                projectName: "Test Project",
                totalCards: 0,
                connection: ConnectionStatus.Connected,
                onlineCount: 0,
                errorCount: 0,
                reorderCardId: null,
                unreadCount: 0
            )
        );

        Assert.Contains("0 unread", output);
    }

    [Fact]
    public void BuildLayout_StatusBar_IncludesOnlineAndErrorCounts()
    {
        var output = RenderLayout(() =>
            BoardRenderer.BuildLayout(
                DefaultColumns,
                selectedColumn: 0,
                selectedCard: 0,
                projectName: "Test Project",
                totalCards: 0,
                connection: ConnectionStatus.Connected,
                onlineCount: 3,
                errorCount: 7,
                reorderCardId: null,
                unreadCount: 10
            )
        );

        Assert.Contains("3 online", output);
        Assert.Contains("7 errors", output);
        Assert.Contains("10 unread", output);
    }

    // Regression test for the height-overflow bug: the board Layout is sized to exactly
    // fill the terminal, so the rendered output must never exceed the declared height —
    // any extra line pushes the title bar off the top, forcing the user to scroll.
    [Theory]
    [InlineData(80, 24)]
    [InlineData(252, 62)]
    [InlineData(40, 15)]
    public void BuildLayout_NeverExceedsDeclaredTerminalHeight(int width, int height)
    {
        var columns = new List<BoardRenderer.ColumnData>
        {
            new(
                Guid.NewGuid(),
                "Backlog",
                0,
                null,
                "blue",
                [new BoardRenderer.CardData(Guid.NewGuid(), 1, "Some card", "Task", [], [], 1)]
            ),
            new(Guid.NewGuid(), "In Progress", 1, null, null, []),
        };

        var console = new TestConsole();
        console.Profile.Width = width;
        console.Profile.Height = height;
        AnsiConsole.Console = console;

        console.Write(
            BoardRenderer.BuildLayout(
                columns,
                selectedColumn: 0,
                selectedCard: 0,
                projectName: "Harumi1",
                totalCards: 1,
                connection: ConnectionStatus.Connected,
                onlineCount: 1,
                errorCount: 0,
                reorderCardId: null,
                unreadCount: 0,
                reorderMode: false,
                hints: BoardHints
            )
        );

        Assert.Equal(height, console.Lines.Count);
    }

    // Regression test for the crash this exposed: below some size, Title(3) + Status left
    // zero or negative rows for the board itself, and Spectre's Panel/Table renderer throws
    // ArgumentOutOfRangeException on a negative render region instead of clamping. Any
    // terminal size must degrade to the "too small" message, never throw.
    [Theory]
    [InlineData(80, 24)]
    [InlineData(40, 10)]
    [InlineData(20, 8)]
    [InlineData(20, 5)]
    [InlineData(20, 3)]
    [InlineData(10, 3)]
    [InlineData(5, 5)]
    [InlineData(6, 15)]
    public void BuildLayout_NeverThrows_AtAnyRealisticTerminalSize(int width, int height)
    {
        var columns = new List<BoardRenderer.ColumnData>
        {
            new(
                Guid.NewGuid(),
                "Backlog",
                0,
                null,
                "blue",
                [new BoardRenderer.CardData(Guid.NewGuid(), 1, "Some card", "Task", [], [], 1)]
            ),
            new(Guid.NewGuid(), "In Progress", 1, null, null, []),
            new(Guid.NewGuid(), "Review", 2, null, null, []),
            new(Guid.NewGuid(), "Done", 3, null, null, []),
        };

        var console = new TestConsole();
        console.Profile.Width = width;
        console.Profile.Height = height;
        AnsiConsole.Console = console;

        var layout = BoardRenderer.BuildLayout(
            columns,
            selectedColumn: 0,
            selectedCard: 0,
            projectName: "Harumi1",
            totalCards: 1,
            connection: ConnectionStatus.Connected,
            onlineCount: 1,
            errorCount: 0,
            reorderCardId: null,
            unreadCount: 0,
            reorderMode: false,
            hints: BoardHints
        );

        console.Write(layout); // must not throw
    }
}
