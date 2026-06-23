using System.Text.Json;
using HydraForge.Domain.Entities.ProjectSpace;
using HydraForge.Domain.Enums;

namespace HydraForge.Application.ProjectSnapshots;

public static class ProjectContextSnapshotRenderer
{
    public static string Render(
        IReadOnlyList<Column> columns,
        IReadOnlyList<Card> cards,
        IReadOnlyList<CardRelationship> relationships
    )
    {
        var activeRelationships = relationships
            .Where(r => r.ArchivedAt == null)
            .ToList();

        var blockerRelationships = activeRelationships
            .Where(r => r.Type == RelationshipType.BlockedBy)
            .ToLookup(r => r.TargetCardId);

        var orderedColumns = columns.OrderBy(c => c.Position).ToList();
        var columnMap = columns.ToDictionary(c => c.Id);

        var recentMoved = cards
            .Where(c => c.ArchivedAt == null && c.MovedAt != default)
            .OrderByDescending(c => c.MovedAt)
            .Take(5)
            .Select(c => new { c.Id, c.CardNumber, c.Title, c.MovedAt })
            .ToList();

        var board = new
        {
            columns = orderedColumns.Select(col =>
            {
                var colCards = cards
                    .Where(c => c.ColumnId == col.Id && c.ArchivedAt == null)
                    .OrderBy(c => c.Position)
                    .ThenBy(c => c.CardNumber)
                    .Select(card =>
                    {
                        var blockers = blockerRelationships[card.Id]
                            .Select(r =>
                            {
                                var blockerCard = cards.FirstOrDefault(c => c.Id == r.SourceCardId);
                                return blockerCard != null && blockerCard.ArchivedAt == null
                                    ? $"#{blockerCard.CardNumber}"
                                    : null;
                            })
                            .Where(x => x != null)
                            .Cast<string>()
                            .ToList();

                        return new
                        {
                            id = card.Id,
                            cardNumber = $"#{card.CardNumber}",
                            title = card.Title,
                            column = col.Name,
                            type = card.Type.ToString(),
                            blockers
                        };
                    })
                    .ToList();

                return new
                {
                    name = col.Name,
                    position = col.Position,
                    cards = colCards
                };
            }).ToList(),
            recentMoved
        };

        return JsonSerializer.Serialize(board, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        });
    }
}