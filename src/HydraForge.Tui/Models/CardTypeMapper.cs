using System.ComponentModel;
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
            _ => cardType.ToString()
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
            _ => throw new ArgumentException($"Unknown card type: {displayString}", nameof(displayString))
        };
    }

    public static string ToShortDisplayString(CardType cardType)
    {
        return cardType switch
        {
            CardType.Task => "T",
            CardType.Issue => "I",
            CardType.Idea => "ID",
            CardType.Goal => "G",
            _ => cardType.ToString()[0].ToString().ToUpper()
        };
    }

    // Mirrors CardModal.vue's SPEC_CARD_TYPES/PLAN_CARD_TYPES/CARD_TYPE_TO_DOC_TYPE —
    // keep in sync with that file if the card-type doc rules change (see D-44).
    public static bool AllowsSpec(CardType cardType) =>
        cardType is CardType.Goal or CardType.Idea or CardType.Issue;

    public static bool AllowsPlan(CardType cardType) =>
        cardType is CardType.Goal or CardType.Issue or CardType.Task;

    public static DocType ToDocType(CardType cardType) => cardType switch
    {
        CardType.Goal => DocType.Specification,
        CardType.Idea => DocType.Concept,
        CardType.Issue => DocType.Report,
        _ => throw new ArgumentException($"Card type {cardType} has no Spec.", nameof(cardType))
    };
}