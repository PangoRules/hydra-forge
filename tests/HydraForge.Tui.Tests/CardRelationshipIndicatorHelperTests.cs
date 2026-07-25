using HydraForge.Tui.Generated;
using HydraForge.Tui.Models;

namespace HydraForge.Tui.Tests;

public class CardRelationshipIndicatorHelperTests
{
    private static CardRelationshipDto Relationship(
        Guid sourceId,
        int sourceNumber,
        Guid targetId,
        int targetNumber,
        RelationshipType type
    ) =>
        new()
        {
            Id = Guid.NewGuid(),
            SourceCardId = sourceId,
            TargetCardId = targetId,
            SourceCardNumber = sourceNumber,
            SourceCardTitle = $"Card {sourceNumber}",
            TargetCardNumber = targetNumber,
            TargetCardTitle = $"Card {targetNumber}",
            Type = type,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedByUserId = Guid.NewGuid(),
        };

    [Fact]
    public void GetRelationBadges_CardIsSource_BadgeIsSourceTrue()
    {
        var cardA = Guid.NewGuid();
        var cardB = Guid.NewGuid();
        var relationships = new List<CardRelationshipDto>
        {
            Relationship(cardA, 1, cardB, 2, RelationshipType.BlockedBy),
        };

        var badges = CardRelationshipIndicatorHelper.GetRelationBadges(cardA, relationships);

        var badge = Assert.Single(badges);
        Assert.Equal(RelationshipType.BlockedBy, badge.Type);
        Assert.True(badge.IsSource);
        Assert.Equal(2, badge.OtherCardNumber);
    }

    [Fact]
    public void GetRelationBadges_CardIsTarget_BadgeIsSourceFalse()
    {
        var cardA = Guid.NewGuid();
        var cardB = Guid.NewGuid();
        var relationships = new List<CardRelationshipDto>
        {
            Relationship(cardA, 1, cardB, 2, RelationshipType.BlockedBy),
        };

        var badges = CardRelationshipIndicatorHelper.GetRelationBadges(cardB, relationships);

        var badge = Assert.Single(badges);
        Assert.Equal(RelationshipType.BlockedBy, badge.Type);
        Assert.False(badge.IsSource);
        Assert.Equal(1, badge.OtherCardNumber);
    }

    [Fact]
    public void GetRelationBadges_UnrelatedRelationship_Ignored()
    {
        var cardA = Guid.NewGuid();
        var cardB = Guid.NewGuid();
        var cardC = Guid.NewGuid();
        var relationships = new List<CardRelationshipDto>
        {
            Relationship(cardA, 1, cardB, 2, RelationshipType.Relates),
        };

        var badges = CardRelationshipIndicatorHelper.GetRelationBadges(cardC, relationships);

        Assert.Empty(badges);
    }

    [Fact]
    public void GetRelationBadges_MultipleTypes_SortedByUrgency()
    {
        var card = Guid.NewGuid();
        var other1 = Guid.NewGuid();
        var other2 = Guid.NewGuid();
        var other3 = Guid.NewGuid();
        var other4 = Guid.NewGuid();

        var relationships = new List<CardRelationshipDto>
        {
            Relationship(card, 1, other1, 2, RelationshipType.Relates),
            Relationship(card, 1, other2, 3, RelationshipType.SpawnedFrom),
            Relationship(other3, 4, card, 1, RelationshipType.BlockedBy),
            Relationship(card, 1, other4, 5, RelationshipType.Precedes),
        };

        var badges = CardRelationshipIndicatorHelper.GetRelationBadges(card, relationships);

        Assert.Equal(4, badges.Count);
        Assert.Equal(RelationshipType.BlockedBy, badges[0].Type);
        Assert.Equal(RelationshipType.Precedes, badges[1].Type);
        Assert.Equal(RelationshipType.SpawnedFrom, badges[2].Type);
        Assert.Equal(RelationshipType.Relates, badges[3].Type);
    }

    [Fact]
    public void GetRelationBadges_MultipleBlockers_AllCollected()
    {
        var card = Guid.NewGuid();
        var blockerA = Guid.NewGuid();
        var blockerB = Guid.NewGuid();

        var relationships = new List<CardRelationshipDto>
        {
            Relationship(blockerA, 2, card, 1, RelationshipType.BlockedBy),
            Relationship(blockerB, 3, card, 1, RelationshipType.BlockedBy),
        };

        var badges = CardRelationshipIndicatorHelper.GetRelationBadges(card, relationships);

        Assert.Equal(2, badges.Count);
        Assert.All(badges, b => Assert.False(b.IsSource));
        Assert.Contains(badges, b => b.OtherCardNumber == 2);
        Assert.Contains(badges, b => b.OtherCardNumber == 3);
    }
}
