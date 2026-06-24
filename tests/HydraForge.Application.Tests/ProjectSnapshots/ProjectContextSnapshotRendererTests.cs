using System.Text.Json;
using HydraForge.Application.ProjectSnapshots;
using HydraForge.Domain.Entities.ProjectSpace;
using HydraForge.Domain.Enums;

namespace HydraForge.Application.Tests.ProjectSnapshots;

public class ProjectContextSnapshotRendererTests
{
    [Fact]
    public void Render_GroupsCardsByColumnPosition()
    {
        var col1Id = Guid.NewGuid();
        var col2Id = Guid.NewGuid();
        var projectId = Guid.NewGuid();

        var columns = new List<Column>
        {
            new() { Id = col1Id, ProjectId = projectId, Name = "Backlog", Position = 0 },
            new() { Id = col2Id, ProjectId = projectId, Name = "Done", Position = 1 },
        };

        var card1Id = Guid.NewGuid();
        var card2Id = Guid.NewGuid();
        var card3Id = Guid.NewGuid();

        var cards = new List<Card>
        {
            new() { Id = card1Id, ProjectId = projectId, ColumnId = col1Id, CardNumber = 1, Title = "Card 1", Position = 0 },
            new() { Id = card2Id, ProjectId = projectId, ColumnId = col2Id, CardNumber = 2, Title = "Card 2", Position = 0 },
            new() { Id = card3Id, ProjectId = projectId, ColumnId = col1Id, CardNumber = 3, Title = "Card 3", Position = 1 },
        };

        var result = ProjectContextSnapshotRenderer.Render(columns, cards, []);

        Assert.Contains("Backlog", result);
        Assert.Contains("Done", result);
        Assert.Contains("Card 1", result);
        Assert.Contains("Card 2", result);
        Assert.Contains("Card 3", result);
    }

    [Fact]
    public void Render_ExcludesArchivedCards()
    {
        var colId = Guid.NewGuid();
        var projectId = Guid.NewGuid();

        var columns = new List<Column>
        {
            new() { Id = colId, ProjectId = projectId, Name = "Backlog", Position = 0 },
        };

        var card1Id = Guid.NewGuid();
        var card2Id = Guid.NewGuid();

        var cards = new List<Card>
        {
            new() { Id = card1Id, ProjectId = projectId, ColumnId = colId, CardNumber = 1, Title = "Active Card", Position = 0 },
            new() { Id = card2Id, ProjectId = projectId, ColumnId = colId, CardNumber = 2, Title = "Archived Card", Position = 1, ArchivedAt = DateTime.UtcNow },
        };

        var result = ProjectContextSnapshotRenderer.Render(columns, cards, []);

        Assert.Contains("Active Card", result);
        Assert.DoesNotContain("Archived Card", result);
    }

    [Fact]
    public void Render_ExcludesArchivedRelationships()
    {
        var colId = Guid.NewGuid();
        var projectId = Guid.NewGuid();

        var columns = new List<Column>
        {
            new() { Id = colId, ProjectId = projectId, Name = "Backlog", Position = 0 },
        };

        var card1Id = Guid.NewGuid();
        var card2Id = Guid.NewGuid();

        var cards = new List<Card>
        {
            new() { Id = card1Id, ProjectId = projectId, ColumnId = colId, CardNumber = 1, Title = "Card 1", Position = 0 },
            new() { Id = card2Id, ProjectId = projectId, ColumnId = colId, CardNumber = 2, Title = "Card 2", Position = 1 },
        };

        var relationships = new List<CardRelationship>
        {
            new() { Id = Guid.NewGuid(), SourceCardId = card1Id, TargetCardId = card2Id, Type = RelationshipType.BlockedBy, ArchivedAt = DateTime.UtcNow },
        };

        var result = ProjectContextSnapshotRenderer.Render(columns, cards, relationships);

        using var doc = JsonDocument.Parse(result);
        // Card1 is blocked by Card2. Card2 is archived, so blockers should be empty.
        var card1Blockers = doc.RootElement
            .GetProperty("columns")[0]
            .GetProperty("cards")[0]
            .GetProperty("blockers");
        Assert.Equal(0, card1Blockers.GetArrayLength());
    }

    [Fact]
    public void Render_IncludesBlockers()
    {
        var colId = Guid.NewGuid();
        var projectId = Guid.NewGuid();

        var columns = new List<Column>
        {
            new() { Id = colId, ProjectId = projectId, Name = "Backlog", Position = 0 },
        };

        var card1Id = Guid.NewGuid();
        var card2Id = Guid.NewGuid();

        var cards = new List<Card>
        {
            new() { Id = card1Id, ProjectId = projectId, ColumnId = colId, CardNumber = 1, Title = "Blocker Card", Position = 0 },
            new() { Id = card2Id, ProjectId = projectId, ColumnId = colId, CardNumber = 2, Title = "Blocked Card", Position = 1 },
        };

        var relationships = new List<CardRelationship>
        {
            new() { Id = Guid.NewGuid(), SourceCardId = card1Id, TargetCardId = card2Id, Type = RelationshipType.BlockedBy },
        };

        var result = ProjectContextSnapshotRenderer.Render(columns, cards, relationships);

        Assert.Contains("#1", result);
    }

    [Fact]
    public void Render_IncludesRecentMovedCards()
    {
        var colId = Guid.NewGuid();
        var projectId = Guid.NewGuid();

        var columns = new List<Column>
        {
            new() { Id = colId, ProjectId = projectId, Name = "Backlog", Position = 0 },
        };

        var now = DateTime.UtcNow;
        var card1Id = Guid.NewGuid();
        var card2Id = Guid.NewGuid();

        var cards = new List<Card>
        {
            new() { Id = card1Id, ProjectId = projectId, ColumnId = colId, CardNumber = 1, Title = "Card 1", Position = 0, MovedAt = now.AddMinutes(-10) },
            new() { Id = card2Id, ProjectId = projectId, ColumnId = colId, CardNumber = 2, Title = "Card 2", Position = 1, MovedAt = now.AddMinutes(-5) },
        };

        var result = ProjectContextSnapshotRenderer.Render(columns, cards, []);

        Assert.Contains("recentMoved", result);
        Assert.Contains("Card 1", result);
        Assert.Contains("Card 2", result);
    }

    [Fact]
    public void Render_CardsOrderedByPosition()
    {
        var colId = Guid.NewGuid();
        var projectId = Guid.NewGuid();

        var columns = new List<Column>
        {
            new() { Id = colId, ProjectId = projectId, Name = "Backlog", Position = 0 },
        };

        var cards = new List<Card>
        {
            new() { Id = Guid.NewGuid(), ProjectId = projectId, ColumnId = colId, CardNumber = 3, Title = "Third", Position = 2 },
            new() { Id = Guid.NewGuid(), ProjectId = projectId, ColumnId = colId, CardNumber = 1, Title = "First", Position = 0 },
            new() { Id = Guid.NewGuid(), ProjectId = projectId, ColumnId = colId, CardNumber = 2, Title = "Second", Position = 1 },
        };

        var result = ProjectContextSnapshotRenderer.Render(columns, cards, []);

        var firstIdx = result.IndexOf("First");
        var secondIdx = result.IndexOf("Second");
        var thirdIdx = result.IndexOf("Third");

        Assert.True(firstIdx < secondIdx);
        Assert.True(secondIdx < thirdIdx);
    }

    [Fact]
    public void Render_IncludesCardIdCardNumberTitleColumnType()
    {
        var colId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var cardId = Guid.NewGuid();

        var columns = new List<Column>
        {
            new() { Id = colId, ProjectId = projectId, Name = "In Dev", Position = 0 },
        };

        var cards = new List<Card>
        {
            new() { Id = cardId, ProjectId = projectId, ColumnId = colId, CardNumber = 5, Title = "My Task", Position = 0, Type = CardType.Task },
        };

        var result = ProjectContextSnapshotRenderer.Render(columns, cards, []);

        Assert.Contains(cardId.ToString(), result);
        Assert.Contains("#5", result);
        Assert.Contains("My Task", result);
        Assert.Contains("In Dev", result);
        Assert.Contains("Task", result);
    }
}