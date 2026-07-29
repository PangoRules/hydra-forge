using HydraForge.Tui.Generated;

namespace HydraForge.Tui.Models;

public record RelationBadge(RelationshipType Type, bool IsSource, int OtherCardNumber);

public static class CardRelationshipIndicatorHelper
{
    // Urgency order: blocking relationships surface first, informational ones last.
    private static readonly Dictionary<RelationshipType, int> TypeOrder = new()
    {
        [RelationshipType.BlockedBy] = 0,
        [RelationshipType.Precedes] = 1,
        [RelationshipType.SpawnedFrom] = 2,
        [RelationshipType.Relates] = 3,
    };

    public static List<RelationBadge> GetRelationBadges(
        Guid cardId,
        IEnumerable<CardRelationshipDto> relationships
    )
    {
        var badges = new List<RelationBadge>();
        foreach (var rel in relationships)
        {
            if (rel.SourceCardId == cardId)
                badges.Add(new RelationBadge(rel.Type, IsSource: true, rel.TargetCardNumber));
            else if (rel.TargetCardId == cardId)
                badges.Add(new RelationBadge(rel.Type, IsSource: false, rel.SourceCardNumber));
        }

        return [.. badges.OrderBy(b => TypeOrder[b.Type])];
    }
}
