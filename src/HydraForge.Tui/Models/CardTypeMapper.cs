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
        try
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
        catch (ArgumentException)
        {
            // Fallback to Task if mapping fails
            return CardType.Task;
        }
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
}