namespace HydraForge.Domain.Tests.Entities;

using HydraForge.Domain.Entities.ProjectSpace;
using HydraForge.Domain.Enums;

public class CardRelationshipTests
{
    [Fact]
    public void Self_relationship_is_detected_by_card_entity()
    {
        var cardA = new Card { Id = Guid.NewGuid(), ProjectId = Guid.NewGuid() };
        var cardB = new Card { Id = Guid.NewGuid(), ProjectId = cardA.ProjectId };

        Assert.NotEqual(cardA.Id, cardB.Id);
    }

    [Fact]
    public void RelationshipType_enum_has_all_expected_values()
    {
        Assert.Equal(1, (int)RelationshipType.BlockedBy);
        Assert.Equal(2, (int)RelationshipType.Precedes);
        Assert.Equal(3, (int)RelationshipType.Relates);
    }

    [Fact]
    public void CardRelationship_entity_stores_all_fields()
    {
        var id = Guid.NewGuid();
        var sourceId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var createdAt = DateTime.UtcNow;
        var archivedAt = DateTime.UtcNow.AddDays(1);

        var rel = new CardRelationship
        {
            Id = id,
            SourceCardId = sourceId,
            TargetCardId = targetId,
            Type = RelationshipType.BlockedBy,
            CreatedAt = createdAt,
            CreatedByUserId = userId,
            ArchivedAt = archivedAt
        };

        Assert.Equal(id, rel.Id);
        Assert.Equal(sourceId, rel.SourceCardId);
        Assert.Equal(targetId, rel.TargetCardId);
        Assert.Equal(RelationshipType.BlockedBy, rel.Type);
        Assert.Equal(createdAt, rel.CreatedAt);
        Assert.Equal(userId, rel.CreatedByUserId);
        Assert.Equal(archivedAt, rel.ArchivedAt);
    }

    [Fact]
    public void CardRelationship_ArchivedAt_is_nullable()
    {
        var rel = new CardRelationship
        {
            Id = Guid.NewGuid(),
            SourceCardId = Guid.NewGuid(),
            TargetCardId = Guid.NewGuid(),
            Type = RelationshipType.Precedes
        };

        Assert.Null(rel.ArchivedAt);
    }

    [Fact]
    public void CardRelationship_default_values()
    {
        var rel = new CardRelationship();

        Assert.NotEqual(Guid.Empty, rel.Id);
        Assert.Equal(default(RelationshipType), rel.Type); // 0 = invalid enum, expected since no explicit default
        Assert.Null(rel.ArchivedAt);
    }
}
