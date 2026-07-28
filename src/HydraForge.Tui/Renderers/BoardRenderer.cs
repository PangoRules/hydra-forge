using Spectre.Console;
using Spectre.Console.Rendering;
using System.Collections.Generic;
using HydraForge.Tui.Generated;
using HydraForge.Tui.Models;

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

    public Layout BuildLayout(
        List<ColumnData> columns,
        int selectedColumn,
        int selectedCard,
        string projectName,
        int totalCards,
        ConnectionStatus connection = ConnectionStatus.Disconnected,
        int onlineCount = 0,
        int errorCount = 0,
        Guid? reorderCardId = null,
        int unreadCount = 0)
    {
        var layout = new Layout("Root")
            .SplitRows(
                new Layout("Title").Size(3),
                new Layout("Board"),
                new Layout("Status").Size(3)
            );

        // Title bar
        layout["Title"].Update(
            new Panel(
                new Markup($"[blue bold]{Markup.Escape(projectName)}[/]  " +
                           $"[grey]{columns.Count} columns  {totalCards} cards[/]")
            ).Expand()
        );

        // Board area is whatever's left after the 3-row title bar and 3-row status bar;
        // each column eats 2 more for its own border — what remains is the card viewport.
        var innerHeight = Math.Max(0, AnsiConsole.Profile.Height - 3 - 3 - 2);

        // Board area — split into columns
        var columnLayouts = columns.Select((col, i) =>
        {
            var isSelected = i == selectedColumn;
            var color = ParseColor(col.Color) ?? Color.Grey;

            var cardHeights = col.Cards
                .Select(c => ColumnScrollCalculator.CardBoxHeight(c.Badges.Count, ExtraLineCount(c)))
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
                var isBeingReordered = reorderCardId.HasValue && card.Id == reorderCardId.Value;
                var typeBadge = TypeBadge(card.Type);

                var assignees = card.AssigneeInitials.Count > 0
                    ? " " + string.Join("", card.AssigneeInitials.Select(a => $"[grey]{a}[/]"))
                    : "";

                var watching = card.IsWatching ? " 👀" : "";

                var title = card.Title.Length > 25
                    ? card.Title[..22] + "..."
                    : card.Title;

                var titleLine = $"{typeBadge} #{card.CardNumber} {Markup.Escape(title)}{assignees}{watching}";
                var shownBadges = card.Badges.Take(ColumnScrollCalculator.MaxBadgesPerCard).Select(FormatBadgeLine);
                var overflowCount = card.Badges.Count - ColumnScrollCalculator.MaxBadgesPerCard;
                var overflowLine = overflowCount > 0
                    ? new[] { $"[grey]+{overflowCount} more (open card)[/]" }
                    : [];
                var extraLines = FormatExtraLines(card);
                var cardMarkup = string.Join("\n", new[] { titleLine }.Concat(shownBadges).Concat(overflowLine).Concat(extraLines));

                var panel = new Panel(new Markup(cardMarkup))
                {
                    Border = isCardSelected || isBeingReordered ? BoxBorder.Double : BoxBorder.Rounded,
                    BorderStyle = isBeingReordered
                        ? new Style(foreground: Color.Yellow)
                        : isCardSelected ? new Style(foreground: Color.Blue) : new Style(foreground: Color.Grey35),
                    // Expand fills the column's width; without pinning Height back to its natural
                    // content size, Expand also stretches the panel to fill leftover column height.
                    Expand = true,
                    Height = ColumnScrollCalculator.CardBoxHeight(card.Badges.Count, ExtraLineCount(card)),
                };

                cardPanels.Add(CardMargin(panel));
            }

            if (window.HasMoreBelow)
                cardPanels.Add(CardMargin(ScrollIndicator($"▼ {col.Cards.Count - window.End} more below")));

            var content = new Rows(cardPanels);
            var wipText = col.WipLimit.HasValue
                ? $" ({col.Cards.Count}/{col.WipLimit})"
                : $" ({col.Cards.Count})";

            return new Panel(content)
            {
                Header = new PanelHeader($" {Markup.Escape(col.Name)}{wipText} "),
                Border = BoxBorder.Rounded,
                BorderStyle = isSelected ? new Style(foreground: Color.Blue) : new Style(foreground: color),
                Expand = true,
            };
        }).ToList();

        var columnsRow = new Columns(columnLayouts);
        layout["Board"].Update(columnsRow);

        // Status bar — connection/online/error counts are live; richer surfacing (notifications, dismiss) is Task 17
        var (dotColor, statusText) = connection switch
        {
            ConnectionStatus.Connected => ("green", "Connected"),
            ConnectionStatus.Reconnecting => ("yellow", "Reconnecting"),
            _ => ("red", "Disconnected")
        };

        layout["Status"].Update(
            new Panel(
                new Markup($"[{dotColor}]●[/] [grey]{statusText}    |    {onlineCount} online    |    {unreadCount} unread    |    {errorCount} errors[/]")
            ).Expand()
        );

        return layout;
    }

    // Symmetric 1-col left/right margin, no vertical gap — cards stack border-to-border,
    // each box's own frame is what separates it from its neighbor (a terminal row is the
    // smallest unit available, so a blank-row gap is the next size up, not "half a row").
    // Padder's own default padding is (1,1,1,1), so every side must be given explicitly
    // or top/bottom pick up an unwanted extra row.
    private static IRenderable CardMargin(IRenderable content) =>
        new Padder(content, new Padding(1, 0, 1, 0));

    private static IRenderable ScrollIndicator(string text) =>
        new Markup($"[grey italic]{text}[/]");

    private static string TypeBadge(string type) => type switch
    {
        "Task" => "[cyan1]T[/]",
        "Issue" => "[red]I[/]",
        "Goal" => "[yellow]G[/]",
        "Idea" => "[green]D[/]",
        _ => "[grey]?[/]"
    };

    // Parent/children/due-date lines are conditional — most cards show 0 or 1 of them,
    // keeping the common case cheap. CardBoxHeight must stay in sync with this count.
    private static int ExtraLineCount(CardData card) =>
        (card.ParentCardNumber.HasValue ? 1 : 0) +
        (card.ChildCount > 0 ? 1 : 0) +
        (card.DueAt.HasValue ? 1 : 0);

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
            RelationshipType.Precedes => ("⏩", "yellow", badge.IsSource ? "precedes" : "preceded by"),
            RelationshipType.SpawnedFrom => ("🌱", "cyan1", badge.IsSource ? "spawned from" : "spawned"),
            _ => ("🔗", "grey", "relates"),
        };

        return $"{glyph} [{color}]{verb} #{badge.OtherCardNumber}[/]";
    }

    private static Color? ParseColor(string? colorName) => colorName?.ToLower() switch
    {
        "red" => Color.Red,
        "green" => Color.Green,
        "blue" => Color.Blue,
        "yellow" => Color.Yellow,
        "purple" => Color.Purple,
        "orange" => Color.Orange1,
        "cyan" => Color.Cyan1,
        _ => null
    };
}