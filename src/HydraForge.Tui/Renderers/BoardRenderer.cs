using Spectre.Console;
using Spectre.Console.Rendering;
using System.Collections.Generic;

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
        bool IsBlocked,
        List<string> AssigneeInitials,
        int Version
    );

    public Layout BuildLayout(
        List<ColumnData> columns,
        int selectedColumn,
        int selectedCard,
        string projectName,
        int totalCards)
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

        // Board area — split into columns
        var columnLayouts = columns.Select((col, i) =>
        {
            var isSelected = i == selectedColumn;
            var color = ParseColor(col.Color) ?? Color.Grey;

            var cardPanels = col.Cards.Select((card, j) =>
            {
                var isCardSelected = isSelected && j == selectedCard;
                var prefix = card.IsBlocked ? "🔴 " : "";
                var typeBadge = card.Type switch
                {
                    "Task" => "[cyan1]T[/]",
                    "Issue" => "[red]I[/]",
                    "Goal" => "[yellow]G[/]",
                    "Idea" => "[green]D[/]",
                    _ => "[grey]?[/]"
                };

                var assignees = card.AssigneeInitials.Count > 0
                    ? " " + string.Join("", card.AssigneeInitials.Select(a => $"[grey]{a}[/]"))
                    : "";

                var title = card.Title.Length > 25
                    ? card.Title[..22] + "..."
                    : card.Title;

                var cardMarkup = $"{prefix}{typeBadge} #{card.CardNumber} {Markup.Escape(title)}{assignees}";

                return new Panel(new Markup(cardMarkup))
                {
                    Border = isCardSelected ? BoxBorder.Double : BoxBorder.None,
                    BorderStyle = isCardSelected ? new Style(foreground: Color.Blue) : null,
                };
            }).ToList();

var content = new Rows(new List<IRenderable>(cardPanels));
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

        // Status bar placeholder (full impl in Task 17)
        layout["Status"].Update(
            new Panel(
                new Markup("[grey]● Connected    |    0 online    |    0 errors[/]")
            ).Expand()
        );

        return layout;
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