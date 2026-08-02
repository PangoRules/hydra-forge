using HydraForge.Tui.Generated;
using HydraForge.Tui.Models;
using HydraForge.Tui.Rendering;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace HydraForge.Tui.Renderers;

public class BoardRenderer
{
    public record ColumnData(
        Guid Id,
        string Name,
        int Position,
        int? WipLimit,
        string? Color,
        List<CardData> Cards
    );

    public record CardData(
        Guid Id,
        int CardNumber,
        string Title,
        string Type,
        List<RelationBadge> Badges,
        List<string> AssigneeInitials,
        int Version,
        int? ParentCardNumber = null,
        string? ParentCardType = null,
        int ChildCount = 0,
        DateTimeOffset? DueAt = null,
        bool IsWatching = false
    );

    public static Layout BuildLayout(
        List<ColumnData> columns,
        int selectedColumn,
        int selectedCard,
        string projectName,
        int totalCards,
        ConnectionStatus connection = ConnectionStatus.Disconnected,
        int onlineCount = 0,
        int errorCount = 0,
        Guid? reorderCardId = null,
        int unreadCount = 0,
        bool reorderMode = false,
        IEnumerable<string>? hints = null
    )
    {
        // Status region folds the connection line + reorder banner + key hints all into
        // ONE sized Layout slot instead of the caller printing them as trailing lines below
        // this (already screen-height) Layout — extra lines below a full-height Layout push
        // the whole frame up and off the top of the terminal, forcing the user to scroll to
        // see the title bar. Everything the screen shows must live inside this Layout.
        var hintLines = KeyHintBar.WrapLines(hints ?? [], AnsiConsole.Profile.Width);
        var statusContentLines = 1 + (reorderMode ? 1 : 0) + hintLines.Count;
        var statusHeight = statusContentLines + 2; // + panel border top/bottom

        // When Title(3) + Status leave less than MinBoardRows for the board itself, or the
        // terminal is narrower than any board layout is usable at, Spectre's own Panel/Table
        // renderer throws ArgumentOutOfRangeException deep inside Segment.SplitLines instead
        // of clamping — a negative/zero render region isn't handled upstream. Bail out to a
        // plain "too small" message before that ever happens, rather than crashing the TUI.
        const int minBoardRows = 6;
        const int minWidth = 40;
        if (
            AnsiConsole.Profile.Width < minWidth
            || AnsiConsole.Profile.Height < 3 + statusHeight + minBoardRows
        )
            return BuildTooSmallLayout(AnsiConsole.Profile.Width, AnsiConsole.Profile.Height);

        var layout = new Layout("Root").SplitRows(
            new Layout("Title").Size(3),
            new Layout("Board"),
            new Layout("Status").Size(statusHeight)
        );

        // Title bar
        layout["Title"]
            .Update(
                new Panel(
                    new Markup(
                        $"[blue bold]{Markup.Escape(projectName)}[/]  "
                            + $"[grey]{columns.Count} columns  {totalCards} cards[/]"
                    )
                ).Expand()
            );

        // Board area is whatever's left after the 3-row title bar and the (variable-height)
        // status bar; each column eats 2 more for its own border — what remains is the card
        // viewport.
        var innerHeight = Math.Max(0, AnsiConsole.Profile.Height - 3 - statusHeight - 2);

        // Board area — split into columns
        var columnLayouts = columns
            .Select(
                (col, i) =>
                {
                    var isSelected = i == selectedColumn;
                    var color = ParseColor(col.Color) ?? Color.Grey;

                    var cardHeights = col
                        .Cards.Select(c =>
                            ColumnScrollCalculator.CardBoxHeight(c.Badges.Count, ExtraLineCount(c))
                        )
                        .ToList();
                    var window = ColumnScrollCalculator.ComputeVisibleRange(
                        cardHeights,
                        isSelected ? selectedCard : null,
                        innerHeight
                    );

                    var cardPanels = new List<IRenderable>();

                    if (window.HasMoreAbove)
                        cardPanels.Add(CardMargin(ScrollIndicator($"▲ {window.Start} more above")));

                    for (var j = window.Start; j < window.End; j++)
                    {
                        var card = col.Cards[j];
                        var isCardSelected = isSelected && j == selectedCard;
                        var isBeingReordered =
                            reorderCardId.HasValue && card.Id == reorderCardId.Value;
                        var typeBadge = TypeBadge(card.Type);

                        var assignees =
                            card.AssigneeInitials.Count > 0
                                ? " "
                                    + string.Join(
                                        "",
                                        card.AssigneeInitials.Select(a => $"[grey]{a}[/]")
                                    )
                                : "";

                        var watching = card.IsWatching ? " 👀" : "";

                        var title = card.Title.Length > 25 ? card.Title[..22] + "..." : card.Title;

                        var titleLine =
                            $"{typeBadge} #{card.CardNumber} {Markup.Escape(title)}{assignees}{watching}";
                        var shownBadges = card
                            .Badges.Take(ColumnScrollCalculator.MaxBadgesPerCard)
                            .Select(FormatBadgeLine);
                        var overflowCount =
                            card.Badges.Count - ColumnScrollCalculator.MaxBadgesPerCard;
                        var overflowLine =
                            overflowCount > 0
                                ? new[] { $"[grey]+{overflowCount} more (open card)[/]" }
                                : [];
                        var extraLines = FormatExtraLines(card);
                        var cardMarkup = string.Join(
                            "\n",
                            new[] { titleLine }
                                .Concat(shownBadges)
                                .Concat(overflowLine)
                                .Concat(extraLines)
                        );

                        var panel = new Panel(new Markup(cardMarkup))
                        {
                            Border =
                                isCardSelected || isBeingReordered
                                    ? BoxBorder.Double
                                    : BoxBorder.Rounded,
                            BorderStyle =
                                isBeingReordered ? new Style(foreground: Color.Yellow)
                                : isCardSelected ? new Style(foreground: Color.Blue)
                                : new Style(foreground: Color.Grey35),
                            // Expand fills the column's width; without pinning Height back to its natural
                            // content size, Expand also stretches the panel to fill leftover column height.
                            Expand = true,
                            Height = ColumnScrollCalculator.CardBoxHeight(
                                card.Badges.Count,
                                ExtraLineCount(card)
                            ),
                        };

                        cardPanels.Add(CardMargin(panel));
                    }

                    if (window.HasMoreBelow)
                        cardPanels.Add(
                            CardMargin(
                                ScrollIndicator($"▼ {col.Cards.Count - window.End} more below")
                            )
                        );

                    var content = new Rows(cardPanels);
                    var wipText = col.WipLimit.HasValue
                        ? $" ({col.Cards.Count}/{col.WipLimit})"
                        : $" ({col.Cards.Count})";

                    return new Panel(content)
                    {
                        Header = new PanelHeader($" {Markup.Escape(col.Name)}{wipText} "),
                        Border = BoxBorder.Rounded,
                        BorderStyle = isSelected
                            ? new Style(foreground: Color.Blue)
                            : new Style(foreground: color),
                        Expand = true,
                    };
                }
            )
            .ToList();

        var columnsRow = new Columns(columnLayouts);
        layout["Board"].Update(columnsRow);

        // Status bar — connection/online/error counts are live; richer surfacing (notifications, dismiss) is Task 17
        var (dotColor, statusText) = connection switch
        {
            ConnectionStatus.Connected => ("green", "Connected"),
            ConnectionStatus.Reconnecting => ("yellow", "Reconnecting"),
            _ => ("red", "Disconnected"),
        };

        var statusLines = new List<IRenderable>
        {
            new Markup(
                $"[{dotColor}]●[/] [grey]{statusText}    |    {onlineCount} online    |    {unreadCount} unread    |    {errorCount} errors[/]"
            ),
        };
        if (reorderMode)
            statusLines.Add(
                new Markup(
                    "[yellow]Reorder mode: j/k to place the highlighted card, Enter to confirm, Esc to cancel[/]"
                )
            );
        statusLines.AddRange(hintLines.Select(l => new Markup(l)));

        layout["Status"].Update(new Panel(new Rows(statusLines)).Expand());

        return layout;
    }

    // A single unsized Layout region — deliberately as simple as possible, since this is
    // the fallback for terminal sizes too small to safely render anything richer.
    private static Layout BuildTooSmallLayout(int width, int height) =>
        new Layout("Root").Update(
            new Panel(
                new Markup(
                    $"[yellow]Terminal too small ({width}x{height}).[/]\n[grey]Resize wider/taller to view the board.[/]"
                )
            ).Expand()
        );

    // Symmetric 1-col left/right margin, no vertical gap — cards stack border-to-border,
    // each box's own frame is what separates it from its neighbor (a terminal row is the
    // smallest unit available, so a blank-row gap is the next size up, not "half a row").
    // Padder's own default padding is (1,1,1,1), so every side must be given explicitly
    // or top/bottom pick up an unwanted extra row.
    private static Padder CardMargin(IRenderable content) => new(content, new(1, 0, 1, 0));

    private static Markup ScrollIndicator(string text) => new($"[grey italic]{text}[/]");

    private static string TypeBadge(string type) =>
        $"[{CardTypeMapper.ToColorName(type)}]{CardTypeMapper.ToShortDisplayString(type)}[/]";

    // Parent/children/due-date lines are conditional — most cards show 0 or 1 of them,
    // keeping the common case cheap. CardBoxHeight must stay in sync with this count.
    private static int ExtraLineCount(CardData card) =>
        (card.ParentCardNumber.HasValue ? 1 : 0)
        + (card.ChildCount > 0 ? 1 : 0)
        + (card.DueAt.HasValue ? 1 : 0);

    private static IEnumerable<string> FormatExtraLines(CardData card)
    {
        if (card.ParentCardNumber.HasValue)
            yield return $"📌 {TypeBadge(card.ParentCardType ?? "")} #{card.ParentCardNumber}";

        if (card.ChildCount > 0)
            yield return $"🌿 [grey]{card.ChildCount} child{(card.ChildCount == 1 ? "" : "ren")}[/]";

        if (card.DueAt.HasValue)
        {
            var overdue = card.DueAt.Value.UtcDateTime < DateTime.UtcNow;
            yield return overdue
                ? $"📅 [red]{card.DueAt.Value:yyyy-MM-dd} (overdue)[/]"
                : $"📅 [grey]{card.DueAt.Value:yyyy-MM-dd}[/]";
        }
    }

    private static string FormatBadgeLine(RelationBadge badge)
    {
        var (glyph, color, verb) = badge.Type switch
        {
            RelationshipType.BlockedBy => ("🔴", "red", badge.IsSource ? "blocks" : "blocked by"),
            RelationshipType.Precedes => (
                "⏩",
                "yellow",
                badge.IsSource ? "precedes" : "preceded by"
            ),
            RelationshipType.SpawnedFrom => (
                "🌱",
                "cyan1",
                badge.IsSource ? "spawned from" : "spawned"
            ),
            _ => ("🔗", "grey", "relates"),
        };

        return $"{glyph} [{color}]{verb} #{badge.OtherCardNumber}[/]";
    }

    private static Color? ParseColor(string? colorName) =>
        colorName?.ToLower() switch
        {
            "red" => Color.Red,
            "green" => Color.Green,
            "blue" => Color.Blue,
            "yellow" => Color.Yellow,
            "purple" => Color.Purple,
            "orange" => Color.Orange1,
            "cyan" => Color.Cyan1,
            _ => null,
        };
}
