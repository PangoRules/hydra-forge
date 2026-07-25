using HydraForge.Tui.Generated;
using HydraForge.Tui.Models;

namespace HydraForge.Tui.Tests;

public class BlockedCardHelperTests
{
    [Fact]
    public void GetBlockedCardIds_SourceCardIsBlocked_TargetCardIsNot()
    {
        var cardA = Guid.NewGuid();
        var cardB = Guid.NewGuid();

        var relationships = new List<CardRelationshipDto>
        {
            new CardRelationshipDto
            {
                Id = Guid.NewGuid(),
                SourceCardId = cardA,
                TargetCardId = cardB,
                SourceCardNumber = 1,
                SourceCardTitle = "Card A",
                TargetCardNumber = 2,
                TargetCardTitle = "Card B",
                Type = RelationshipType.BlockedBy,
                CreatedAt = DateTimeOffset.UtcNow,
                CreatedByUserId = Guid.NewGuid(),
            }
        };

        var blocked = BlockedCardHelper.GetBlockedCardIds(relationships);

        Assert.Contains(cardA, blocked);
        Assert.DoesNotContain(cardB, blocked);
    }

    [Fact]
    public void GetBlockedCardIds_MultipleBlockedBy_CollectsAllBlockedCards()
    {
        var cardA = Guid.NewGuid();
        var cardB = Guid.NewGuid();
        var cardC = Guid.NewGuid();
        var blockerD = Guid.NewGuid();

        var relationships = new List<CardRelationshipDto>
        {
            new CardRelationshipDto
            {
                Id = Guid.NewGuid(),
                SourceCardId = cardA,
                TargetCardId = blockerD,
                SourceCardNumber = 1,
                SourceCardTitle = "Card A",
                TargetCardNumber = 4,
                TargetCardTitle = "Blocker D",
                Type = RelationshipType.BlockedBy,
                CreatedAt = DateTimeOffset.UtcNow,
                CreatedByUserId = Guid.NewGuid(),
            },
            new CardRelationshipDto
            {
                Id = Guid.NewGuid(),
                SourceCardId = cardB,
                TargetCardId = blockerD,
                SourceCardNumber = 2,
                SourceCardTitle = "Card B",
                TargetCardNumber = 4,
                TargetCardTitle = "Blocker D",
                Type = RelationshipType.BlockedBy,
                CreatedAt = DateTimeOffset.UtcNow,
                CreatedByUserId = Guid.NewGuid(),
            },
            new CardRelationshipDto
            {
                Id = Guid.NewGuid(),
                SourceCardId = cardC,
                TargetCardId = cardA,
                SourceCardNumber = 3,
                SourceCardTitle = "Card C",
                TargetCardNumber = 1,
                TargetCardTitle = "Card A",
                Type = RelationshipType.BlockedBy,
                CreatedAt = DateTimeOffset.UtcNow,
                CreatedByUserId = Guid.NewGuid(),
            },
        };

        var blocked = BlockedCardHelper.GetBlockedCardIds(relationships);

        Assert.Contains(cardA, blocked);
        Assert.Contains(cardB, blocked);
        Assert.Contains(cardC, blocked);
        Assert.DoesNotContain(blockerD, blocked);
    }

    [Fact]
    public void GetBlockedCardIds_RelatesType_Ignored()
    {
        var cardA = Guid.NewGuid();
        var cardB = Guid.NewGuid();

        var relationships = new List<CardRelationshipDto>
        {
            new CardRelationshipDto
            {
                Id = Guid.NewGuid(),
                SourceCardId = cardA,
                TargetCardId = cardB,
                SourceCardNumber = 1,
                SourceCardTitle = "Card A",
                TargetCardNumber = 2,
                TargetCardTitle = "Card B",
                Type = RelationshipType.Relates,
                CreatedAt = DateTimeOffset.UtcNow,
                CreatedByUserId = Guid.NewGuid(),
            }
        };

        var blocked = BlockedCardHelper.GetBlockedCardIds(relationships);

        Assert.Empty(blocked);
    }
}
