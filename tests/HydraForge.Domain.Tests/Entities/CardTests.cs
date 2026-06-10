namespace HydraForge.Domain.Tests.Entities;

using HydraForge.Domain.Common;
using HydraForge.Domain.Entities.ProjectSpace;
using HydraForge.Domain.Enums;

public class CardTests
{
    [Theory]
    [InlineData(CardType.Task)]
    [InlineData(CardType.Bug)]
    [InlineData(CardType.Spec)]
    [InlineData(CardType.Idea)]
    [InlineData(CardType.Epic)]
    public void CardType_AllValues_AreDefined(CardType type)
    {
        Assert.True(Enum.IsDefined(typeof(CardType), type));
    }

    [Fact]
    public void CardType_Epic_HasCorrectValue()
    {
        Assert.Equal(5, (int)CardType.Epic);
    }

    [Fact]
    public void Card_NewInstance_HasDefaultValues()
    {
        var card = new Card();

        Assert.NotEqual(Guid.Empty, card.Id);
        Assert.Equal(CardType.Task, card.Type);
        Assert.Equal(1, card.Version);
        Assert.Null(card.ArchivedAt);
        Assert.Null(card.ParentCardId);
        Assert.Null(card.SpecId);
        Assert.Null(card.PlanId);
        Assert.Equal(string.Empty, card.Title);
        Assert.Equal(string.Empty, card.Description);
    }

    [Fact]
    public void Card_SetParentCardId_StoresValue()
    {
        var card = new Card();
        var parentId = Guid.NewGuid();

        card.ParentCardId = parentId;

        Assert.Equal(parentId, card.ParentCardId);
    }

    [Fact]
    public void Card_ArchivedAt_WhenSet_IsNotNull()
    {
        var card = new Card();
        var archivedAt = DateTime.UtcNow;

        card.ArchivedAt = archivedAt;

        Assert.NotNull(card.ArchivedAt);
        Assert.Equal(archivedAt, card.ArchivedAt);
    }

    [Fact]
    public void Card_Version_StartsAtOne()
    {
        var card = new Card();

        Assert.Equal(1, card.Version);
    }

    [Fact]
    public void Card_MovedAt_DefaultsToUtcNow()
    {
        var before = DateTime.UtcNow;
        var card = new Card();
        var after = DateTime.UtcNow;

        Assert.True(card.MovedAt >= before && card.MovedAt <= after);
    }

    [Fact]
    public void Card_CreatedAt_DefaultsToUtcNow()
    {
        var before = DateTime.UtcNow;
        var card = new Card();
        var after = DateTime.UtcNow;

        Assert.True(card.CreatedAt >= before && card.CreatedAt <= after);
    }

    [Fact]
    public void Card_ValidateParentEpic_SameProject_Succeeds()
    {
        var projectId = Guid.NewGuid();
        var parentCard = new Card { Id = Guid.NewGuid(), ProjectId = projectId, Type = CardType.Epic };
        var childCard = new Card { Id = Guid.NewGuid(), ProjectId = projectId, Type = CardType.Task };

        var result = Card.ValidateParentEpic(childCard, parentCard);

        Assert.Null(result);
    }

    [Fact]
    public void Card_ValidateParentEpic_DifferentProject_ReturnsInvalidParentEpic()
    {
        var parentCard = new Card { Id = Guid.NewGuid(), ProjectId = Guid.NewGuid(), Type = CardType.Epic };
        var childCard = new Card { Id = Guid.NewGuid(), ProjectId = Guid.NewGuid(), Type = CardType.Task };

        var result = Card.ValidateParentEpic(childCard, parentCard);

        Assert.NotNull(result);
        Assert.Equal(DomainErrorCodes.Cards.InvalidParentEpic, result.Code);
    }

    [Fact]
    public void Card_ValidateParentEpic_ParentNotEpic_ReturnsInvalidParentEpic()
    {
        var projectId = Guid.NewGuid();
        var parentCard = new Card { Id = Guid.NewGuid(), ProjectId = projectId, Type = CardType.Task };
        var childCard = new Card { Id = Guid.NewGuid(), ProjectId = projectId, Type = CardType.Task };

        var result = Card.ValidateParentEpic(childCard, parentCard);

        Assert.NotNull(result);
        Assert.Equal(DomainErrorCodes.Cards.InvalidParentEpic, result.Code);
    }

    [Fact]
    public void Card_ValidateParentEpic_SelfReference_ReturnsParentCycle()
    {
        var projectId = Guid.NewGuid();
        var card = new Card { Id = Guid.NewGuid(), ProjectId = projectId, Type = CardType.Task };

        var result = Card.ValidateParentEpic(card, card);

        Assert.NotNull(result);
        Assert.Equal(DomainErrorCodes.Cards.ParentCycle, result.Code);
    }

    [Fact]
    public void Card_ValidateParentEpic_AncestorCycle_ReturnsParentCycle()
    {
        var projectId = Guid.NewGuid();
        var grandchild = new Card { Id = Guid.NewGuid(), ProjectId = projectId, Type = CardType.Task };
        var child = new Card { Id = Guid.NewGuid(), ProjectId = projectId, Type = CardType.Task, ParentCardId = grandchild.Id };
        var parent = new Card { Id = Guid.NewGuid(), ProjectId = projectId, Type = CardType.Epic, ParentCardId = child.Id };
        var cardMap = new Dictionary<Guid, Card>
        {
            { grandchild.Id, grandchild },
            { child.Id, child },
            { parent.Id, parent }
        };

        var result = Card.ValidateParentEpic(grandchild, parent, cardMap);

        Assert.NotNull(result);
        Assert.Equal(DomainErrorCodes.Cards.ParentCycle, result.Code);
    }

    [Fact]
    public void Card_ValidateParentEpic_ValidAncestorChain_Succeeds()
    {
        var projectId = Guid.NewGuid();
        var epic = new Card { Id = Guid.NewGuid(), ProjectId = projectId, Type = CardType.Epic };
        var child = new Card { Id = Guid.NewGuid(), ProjectId = projectId, Type = CardType.Task, ParentCardId = epic.Id };
        var grandchild = new Card { Id = Guid.NewGuid(), ProjectId = projectId, Type = CardType.Task, ParentCardId = child.Id };

        var result = Card.ValidateParentEpic(grandchild, epic);

        Assert.Null(result);
    }

    [Fact]
    public void Card_UpdateDetails_SetsFieldsAndIncrementsVersion()
    {
        var card = new Card { Version = 1 };

        card.UpdateDetails("Updated Title", "Updated Desc", CardType.Bug, Guid.NewGuid(), DateTime.UtcNow.AddDays(1));

        Assert.Equal("Updated Title", card.Title);
        Assert.Equal("Updated Desc", card.Description);
        Assert.Equal(CardType.Bug, card.Type);
        Assert.NotNull(card.ParentCardId);
        Assert.NotNull(card.DueAt);
        Assert.Equal(2, card.Version);
        Assert.True(card.UpdatedAt <= DateTime.UtcNow);
    }

    [Fact]
    public void Card_MoveTo_UpdatesPositionAndTimestamp()
    {
        var card = new Card { Version = 1, ColumnId = Guid.NewGuid(), Position = 0 };
        var newColumnId = Guid.NewGuid();
        var beforeMove = DateTime.UtcNow;

        card.MoveTo(newColumnId, 5);

        Assert.Equal(newColumnId, card.ColumnId);
        Assert.Equal(5, card.Position);
        Assert.Equal(2, card.Version);
        Assert.True(card.MovedAt >= beforeMove);
        Assert.True(card.MovedAt <= DateTime.UtcNow);
        Assert.True(card.UpdatedAt >= beforeMove);
    }

    [Fact]
    public void Card_ShiftPosition_AppliesDelta()
    {
        var card = new Card { Position = 3 };

        card.ShiftPosition(2);

        Assert.Equal(5, card.Position);
    }

    [Fact]
    public void Card_ShiftPosition_NegativeDelta()
    {
        var card = new Card { Position = 5 };

        card.ShiftPosition(-2);

        Assert.Equal(3, card.Position);
    }

    [Fact]
    public void Card_Archive_SetsTimestampAndIncrementsVersion()
    {
        var card = new Card { Version = 3 };
        var beforeArchive = DateTime.UtcNow;

        card.Archive();

        Assert.NotNull(card.ArchivedAt);
        Assert.True(card.ArchivedAt >= beforeArchive);
        Assert.True(card.ArchivedAt <= DateTime.UtcNow);
        Assert.Equal(4, card.Version);
    }
}
