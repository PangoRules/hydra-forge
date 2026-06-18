namespace HydraForge.Application.Cards;

using HydraForge.Domain.Entities.ProjectSpace;
using HydraForge.Domain.Enums;

public static class CardDependencyGraph
{
    /// Returns true if adding an edge (source→target, type) would create a cycle.
    /// Only BlockedBy and Precedes participate in cycle detection; Relates does not.
    public static bool WouldCreateCycle(
        Guid sourceId,
        Guid targetId,
        RelationshipType type,
        IReadOnlyDictionary<Guid, Card> cardsById,
        IReadOnlyList<CardRelationship> existingRelationships)
    {
        if (type == RelationshipType.Relates)
            return false;

        var deps = new Dictionary<Guid, List<Guid>>();
        foreach (var r in existingRelationships.Where(r => r.ArchivedAt == null && r.Type != RelationshipType.Relates))
        {
            if (!deps.ContainsKey(r.SourceCardId))
                deps[r.SourceCardId] = new List<Guid>();
            deps[r.SourceCardId].Add(r.TargetCardId);
        }

        var visited = new HashSet<Guid>();
        var stack = new Stack<Guid>();
        stack.Push(targetId);

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (current == sourceId)
                return true;
            if (!visited.Add(current))
                continue;
            if (deps.TryGetValue(current, out var outgoing))
            {
                foreach (var next in outgoing)
                    stack.Push(next);
            }
        }
        return false;
    }

    /// Returns all cards that depend on (directly or transitively) the given card.
    public static List<Guid> GetDependentCards(
        Guid cardId,
        RelationshipType type,
        IReadOnlyDictionary<Guid, Card> cardsById,
        IReadOnlyList<CardRelationship> existingRelationships)
    {
        var deps = new Dictionary<Guid, List<Guid>>();
        foreach (var r in existingRelationships.Where(r => r.ArchivedAt == null && r.Type == type))
        {
            if (!deps.ContainsKey(r.SourceCardId))
                deps[r.SourceCardId] = new List<Guid>();
            deps[r.SourceCardId].Add(r.TargetCardId);
        }

        var result = new List<Guid>();
        var visited = new HashSet<Guid>();
        var stack = new Stack<Guid>();
        stack.Push(cardId);

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (!visited.Add(current))
                continue;
            result.Add(current);
            if (deps.TryGetValue(current, out var outgoing))
            {
                foreach (var next in outgoing)
                    stack.Push(next);
            }
        }
        result.Remove(cardId);
        return result;
    }
}
