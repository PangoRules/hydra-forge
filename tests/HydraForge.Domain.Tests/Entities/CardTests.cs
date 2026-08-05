namespace HydraForge.Domain.Tests.Entities;

using HydraForge.Domain.Common;
using HydraForge.Domain.Entities.ProjectSpace;
using HydraForge.Domain.Enums;

public class CardTests
{
    [Theory]
    [InlineData(CardType.Task)]
    [InlineData(CardType.Issue)]
    [InlineData(CardType.Idea)]
    [InlineData(CardType.Goal)]
    public void CardType_AllValues_AreDefined(CardType type)
    {
        Assert.True(Enum.IsDefined(typeof(CardType), type));
    }

    [Fact]
    public void CardType_Goal_HasCorrectValue()
    {
        Assert.Equal(5, (int)CardType.Goal);
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
    public void Card_ValidateParent_SameProject_Succeeds()
    {
        var projectId = Guid.NewGuid();
        var parentCard = new Card
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Type = CardType.Goal,
        };
        var childCard = new Card
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Type = CardType.Task,
        };

        var result = Card.ValidateParent(childCard, parentCard);

        Assert.Null(result);
    }

    [Fact]
    public void Card_ValidateParent_DifferentProject_ReturnsInvalidParent()
    {
        var parentCard = new Card
        {
            Id = Guid.NewGuid(),
            ProjectId = Guid.NewGuid(),
            Type = CardType.Goal,
        };
        var childCard = new Card
        {
            Id = Guid.NewGuid(),
            ProjectId = Guid.NewGuid(),
            Type = CardType.Task,
        };

        var result = Card.ValidateParent(childCard, parentCard);

        Assert.NotNull(result);
        Assert.Equal(DomainErrorCodes.Cards.InvalidParent, result.Code);
    }

    [Fact]
    public void Card_ValidateParent_AnyCardType_CanBeParent()
    {
        var projectId = Guid.NewGuid();
        var parentCard = new Card
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Type = CardType.Task,
        };
        var childCard = new Card
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Type = CardType.Task,
        };

        var result = Card.ValidateParent(childCard, parentCard);

        Assert.Null(result);
    }

    [Fact]
    public void Card_ValidateParent_SelfReference_ReturnsParentCycle()
    {
        var projectId = Guid.NewGuid();
        var card = new Card
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Type = CardType.Task,
        };

        var result = Card.ValidateParent(card, card);

        Assert.NotNull(result);
        Assert.Equal(DomainErrorCodes.Cards.ParentCycle, result.Code);
    }

    [Fact]
    public void Card_ValidateParent_AncestorCycle_ReturnsParentCycle()
    {
        var projectId = Guid.NewGuid();
        var grandchild = new Card
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Type = CardType.Task,
        };
        var child = new Card
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Type = CardType.Task,
            ParentCardId = grandchild.Id,
        };
        var parent = new Card
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Type = CardType.Goal,
            ParentCardId = child.Id,
        };
        var cardMap = new Dictionary<Guid, Card>
        {
            { grandchild.Id, grandchild },
            { child.Id, child },
            { parent.Id, parent },
        };

        var result = Card.ValidateParent(grandchild, parent, cardMap);

        Assert.NotNull(result);
        Assert.Equal(DomainErrorCodes.Cards.ParentCycle, result.Code);
    }

    [Fact]
    public void Card_ValidateParent_ValidAncestorChain_Succeeds()
    {
        var projectId = Guid.NewGuid();
        var goal = new Card
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Type = CardType.Goal,
        };
        var child = new Card
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Type = CardType.Task,
            ParentCardId = goal.Id,
        };
        var grandchild = new Card
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Type = CardType.Task,
            ParentCardId = child.Id,
        };

        var result = Card.ValidateParent(grandchild, goal);

        Assert.Null(result);
    }

    [Fact]
    public void Card_UpdateDetails_SetsFieldsAndIncrementsVersion()
    {
        var card = new Card { Version = 1 };

        card.UpdateDetails(
            "Updated Title",
            "Updated Desc",
            CardType.Issue,
            Guid.NewGuid(),
            DateTime.UtcNow.AddDays(1)
        );

        Assert.Equal("Updated Title", card.Title);
        Assert.Equal("Updated Desc", card.Description);
        Assert.Equal(CardType.Issue, card.Type);
        Assert.NotNull(card.ParentCardId);
        Assert.NotNull(card.DueAt);
        Assert.Equal(2, card.Version);
        Assert.True(card.UpdatedAt <= DateTime.UtcNow);
    }

    [Fact]
    public void Card_MoveTo_UpdatesPositionAndTimestamp()
    {
        var card = new Card
        {
            Version = 1,
            ColumnId = Guid.NewGuid(),
            Position = 0,
        };
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

    // ─── Card-type → doc-type validation (D-44, D-XX1, D-XX2, D-XX3) ─────────

    [Theory]
    [InlineData(CardType.Goal)]
    [InlineData(CardType.Idea)]
    [InlineData(CardType.Issue)]
    [InlineData(CardType.Security)]
    [InlineData(CardType.Task)]
    public void ValidateAllowsSpec_AllowedTypes_ReturnsNull(CardType type)
    {
        var result = Card.ValidateAllowsSpec(type);

        Assert.Null(result);
    }

    [Fact]
    public void ValidateAllowsSpec_Task_ReturnsNull()
    {
        var result = Card.ValidateAllowsSpec(CardType.Task);

        Assert.Null(result);
    }

    [Theory]
    [InlineData(CardType.Issue)]
    [InlineData(CardType.Task)]
    public void ValidateAllowsPlan_AllowedTypes_ReturnsNull(CardType type)
    {
        var result = Card.ValidateAllowsPlan(type);

        Assert.Null(result);
    }

    [Theory]
    [InlineData(CardType.Goal)]
    [InlineData(CardType.Idea)]
    [InlineData(CardType.Security)]
    public void ValidateAllowsPlan_DisallowedTypes_ReturnsError(CardType type)
    {
        var result = Card.ValidateAllowsPlan(type);

        Assert.NotNull(result);
        Assert.Equal(DomainErrorCodes.Plans.InvalidCardType, result.Code);
    }

    [Theory]
    [InlineData(CardType.Goal, DocType.Specification, true)]
    [InlineData(CardType.Goal, DocType.ValidationMatrix, true)]
    [InlineData(CardType.Goal, DocType.Concept, false)]
    [InlineData(CardType.Goal, DocType.Report, false)]
    [InlineData(CardType.Task, DocType.ValidationMatrix, true)]
    [InlineData(CardType.Task, DocType.Specification, false)]
    [InlineData(CardType.Task, DocType.Report, false)]
    [InlineData(CardType.Task, DocType.Concept, false)]
    [InlineData(CardType.Idea, DocType.Concept, true)]
    [InlineData(CardType.Idea, DocType.Specification, false)]
    [InlineData(CardType.Idea, DocType.Report, false)]
    [InlineData(CardType.Issue, DocType.Report, true)]
    [InlineData(CardType.Issue, DocType.Specification, false)]
    [InlineData(CardType.Security, DocType.Report, true)]
    [InlineData(CardType.Security, DocType.Concept, false)]
    public void IsValidSpecDocType_ReturnsExpectedResult(
        CardType cardType,
        DocType docType,
        bool expected
    )
    {
        var result = Card.IsValidSpecDocType(cardType, docType);

        Assert.Equal(expected, result);
    }
}
