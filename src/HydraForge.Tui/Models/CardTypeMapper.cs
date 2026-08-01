using HydraForge.Tui.Generated;

namespace HydraForge.Tui.Models;

public static class CardTypeMapper
{
    public static string ToDisplayString(CardType cardType)
    {
        return cardType switch
        {
            CardType.Task => "Task",
            CardType.Issue => "Issue",
            CardType.Idea => "Idea",
            CardType.Goal => "Goal",
            _ => cardType.ToString(),
        };
    }

    public static CardType FromDisplayString(string displayString)
    {
        return displayString.ToUpperInvariant() switch
        {
            "TASK" => CardType.Task,
            "ISSUE" => CardType.Issue,
            "IDEA" => CardType.Idea,
            "GOAL" => CardType.Goal,
            _ => throw new ArgumentException(
                $"Unknown card type: {displayString}",
                nameof(displayString)
            ),
        };
    }

    // Single-char badge for tight board-tile space — Idea gets "D" (i-D-ea) since
    // "I" is already taken by Issue.
    public static string ToShortDisplayString(string displayString) =>
        displayString switch
        {
            "Task" => "T",
            "Issue" => "I",
            "Goal" => "G",
            "Idea" => "D",
            _ => "?",
        };

    public static string ToShortDisplayString(CardType cardType) =>
        ToShortDisplayString(ToDisplayString(cardType));

    public static string ToColorName(string displayString) =>
        displayString switch
        {
            "Task" => "cyan1",
            "Issue" => "red",
            "Goal" => "yellow",
            "Idea" => "green",
            _ => "grey",
        };

    public static string ToColorName(CardType cardType) => ToColorName(ToDisplayString(cardType));

    // Mirrors CardModal.vue's SPEC_CARD_TYPES/PLAN_CARD_TYPES/CARD_TYPE_TO_DOC_TYPE —
    // keep in sync with that file if the card-type doc rules change (see D-44).
    public static bool AllowsSpec(CardType cardType) =>
        cardType is CardType.Goal or CardType.Idea or CardType.Issue;

    public static bool AllowsPlan(CardType cardType) =>
        cardType is CardType.Goal or CardType.Issue or CardType.Task;

    public static DocType ToDocType(CardType cardType) =>
        cardType switch
        {
            CardType.Goal => DocType.Specification,
            CardType.Idea => DocType.Concept,
            CardType.Issue => DocType.Report,
            _ => throw new ArgumentException(
                $"Card type {cardType} has no Spec.",
                nameof(cardType)
            ),
        };
}
