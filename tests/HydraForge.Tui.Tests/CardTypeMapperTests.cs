using HydraForge.Tui.Generated;
using HydraForge.Tui.Models;

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
            { CardType.Goal, "Goal" },
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
        // "D" for Idea, not "I" — Issue already owns that letter.
        var expectedMappings = new Dictionary<CardType, string>
        {
            { CardType.Task, "T" },
            { CardType.Issue, "I" },
            { CardType.Idea, "D" },
            { CardType.Goal, "G" },
        };

        // Act & Assert
        foreach (var kvp in expectedMappings)
        {
            var result = CardTypeMapper.ToShortDisplayString(kvp.Key);
            Assert.Equal(kvp.Value, result);
        }
    }

    [Fact]
    public void ToColorName_Should_Return_Correct_Colors()
    {
        var expectedMappings = new Dictionary<CardType, string>
        {
            { CardType.Task, "cyan1" },
            { CardType.Issue, "red" },
            { CardType.Goal, "yellow" },
            { CardType.Idea, "green" },
        };

        foreach (var kvp in expectedMappings)
        {
            Assert.Equal(kvp.Value, CardTypeMapper.ToColorName(kvp.Key));
            Assert.Equal(kvp.Value, CardTypeMapper.ToColorName(kvp.Key.ToString()));
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
            { "GOAL", CardType.Goal },
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

    [Theory]
    [InlineData(CardType.Goal, true)]
    [InlineData(CardType.Idea, true)]
    [InlineData(CardType.Issue, true)]
    [InlineData(CardType.Task, true)]
    [InlineData(CardType.Security, true)]
    public void AllowsSpec_Should_Match_Card_Type_Rules(CardType cardType, bool expected)
    {
        Assert.Equal(expected, CardTypeMapper.AllowsSpec(cardType));
    }

    [Theory]
    [InlineData(CardType.Goal, false)]
    [InlineData(CardType.Issue, true)]
    [InlineData(CardType.Task, true)]
    [InlineData(CardType.Idea, false)]
    [InlineData(CardType.Security, false)]
    public void AllowsPlan_Should_Match_Card_Type_Rules(CardType cardType, bool expected)
    {
        Assert.Equal(expected, CardTypeMapper.AllowsPlan(cardType));
    }

    [Theory]
    [InlineData(CardType.Goal, DocType.Specification)]
    [InlineData(CardType.Idea, DocType.Concept)]
    [InlineData(CardType.Issue, DocType.Report)]
    [InlineData(CardType.Security, DocType.Report)]
    [InlineData(CardType.Task, DocType.ValidationMatrix)]
    public void ToDocType_Should_Return_Correct_Doc_Type(CardType cardType, DocType expected)
    {
        Assert.Equal(expected, CardTypeMapper.ToDocType(cardType));
    }
}
