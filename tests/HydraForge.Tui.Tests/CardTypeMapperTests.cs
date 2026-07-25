using HydraForge.Tui.Models;
using HydraForge.Tui.Generated;

namespace HydraForge.Tui.Tests;

public class CardTypeMapperTests
{
    [Fact]
    public void ToDisplayString_Should_Return_Correct_Display_Strings()
    {
        // Arrange
        var expectedMappings = new Dictionary<CardType, string>
        {
            { CardType.Task, "Task" },
            { CardType.Issue, "Issue" },
            { CardType.Idea, "Idea" },
            { CardType.Goal, "Goal" }
        };

        // Act & Assert
        foreach (var kvp in expectedMappings)
        {
            var result = CardTypeMapper.ToDisplayString(kvp.Key);
            Assert.Equal(kvp.Value, result);
        }
    }

    [Fact]
    public void ToShortDisplayString_Should_Return_Correct_Short_Strings()
    {
        // Arrange
        var expectedMappings = new Dictionary<CardType, string>
        {
            { CardType.Task, "T" },
            { CardType.Issue, "I" },
            { CardType.Idea, "ID" },
            { CardType.Goal, "G" }
        };

        // Act & Assert
        foreach (var kvp in expectedMappings)
        {
            var result = CardTypeMapper.ToShortDisplayString(kvp.Key);
            Assert.Equal(kvp.Value, result);
        }
    }

    [Fact]
    public void FromDisplayString_Should_Return_Correct_Enum_Values()
    {
        // Arrange
        var expectedMappings = new Dictionary<string, CardType>
        {
            { "TASK", CardType.Task },
            { "ISSUE", CardType.Issue },
            { "IDEA", CardType.Idea },
            { "GOAL", CardType.Goal }
        };

        // Act & Assert
        foreach (var kvp in expectedMappings)
        {
            var result = CardTypeMapper.FromDisplayString(kvp.Key);
            Assert.Equal(kvp.Value, result);
        }
    }

    [Fact]
    public void FromDisplayString_Should_Throw_When_Invalid_String()
    {
        Assert.Throws<ArgumentException>(() => CardTypeMapper.FromDisplayString("INVALID"));
    }
}