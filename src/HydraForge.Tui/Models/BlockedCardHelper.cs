using HydraForge.Tui.Generated;

namespace HydraForge.Tui.Models;

public static class BlockedCardHelper
{
    public static HashSet<Guid> GetBlockedCardIds(IEnumerable<CardRelationshipDto> relationships)
    {
        var blocked = new HashSet<Guid>();
        foreach (var rel in relationships)
        {
            if (rel.Type == RelationshipType.BlockedBy)
                blocked.Add(rel.SourceCardId);
        }
        return blocked;
    }
}
