using HydraForge.Domain.Common;
using HydraForge.Domain.Enums;

namespace HydraForge.Domain.Entities.ProjectSpace;

public class Card
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public Guid ColumnId { get; set; }
    public Guid? ParentCardId { get; set; }
    public int CardNumber { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public CardType Type { get; set; } = CardType.Task;
    public int Position { get; set; }
    public DateTime? DueAt { get; set; }
    public int Version { get; set; } = 1;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime MovedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ArchivedAt { get; set; }

    public void UpdateDetails(
        string title,
        string description,
        CardType type,
        Guid? parentCardId,
        DateTime? dueAt
    )
    {
        Title = title;
        Description = description;
        Type = type;
        ParentCardId = parentCardId;
        DueAt = dueAt;
        UpdatedAt = DateTime.UtcNow;
        Version += 1;
    }

    public void MoveTo(Guid columnId, int position)
    {
        ColumnId = columnId;
        Position = position;
        MovedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
        Version += 1;
    }

    public void ShiftPosition(int delta)
    {
        Position += delta;
    }

    public void Archive()
    {
        ArchivedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
        Version += 1;
    }

    public void Restore()
    {
        ArchivedAt = null;
        UpdatedAt = DateTime.UtcNow;
        Version += 1;
    }

    public static Error? ValidateParent(
        Card child,
        Card parent,
        IReadOnlyDictionary<Guid, Card>? cardMap = null
    )
    {
        if (child.Id == parent.Id)
            return new Error(DomainErrorCodes.Cards.ParentCycle, "Card cannot be its own parent.");

        if (child.ProjectId != parent.ProjectId)
            return new Error(
                DomainErrorCodes.Cards.InvalidParent,
                "Parent card must be in the same project."
            );

        if (cardMap != null)
        {
            var cycleError = ValidateNoCycle(child.Id, parent.Id, cardMap);
            if (cycleError != null)
                return cycleError;
        }

        return null;
    }

    // Spec is allowed on Goal/Idea/Issue/Security/Task cards (see D-44, D-XX2, D-XX3).
    public static Error? ValidateAllowsSpec(CardType type) =>
        type is CardType.Goal or CardType.Idea or CardType.Issue or CardType.Security or CardType.Task
            ? null
            : new Error(
                DomainErrorCodes.Specs.InvalidCardType,
                $"{type} cards cannot have a Spec."
            );

    // Plan is direct on Issue/Task only (see D-XX1).
    public static Error? ValidateAllowsPlan(CardType type) =>
        type is CardType.Issue or CardType.Task
            ? null
            : new Error(
                DomainErrorCodes.Plans.InvalidCardType,
                $"{type} cards cannot have a Plan."
            );

    // Which DocTypes are valid for a given CardType (N Specs per card allowed — see D-XX6).
    public static bool IsValidSpecDocType(CardType cardType, DocType docType) =>
        cardType switch
        {
            CardType.Goal => docType is DocType.Specification or DocType.ValidationMatrix,
            CardType.Task => docType is DocType.ValidationMatrix,
            CardType.Idea => docType is DocType.Concept,
            CardType.Issue => docType is DocType.Report,
            CardType.Security => docType is DocType.Report,
            _ => false,
        };

    public static Error? ValidateNoCycle(
        Guid childId,
        Guid? parentId,
        IReadOnlyDictionary<Guid, Card> cardMap
    )
    {
        if (parentId == null)
            return null;

        var ancestorId = parentId;
        var visited = new HashSet<Guid> { childId };

        while (ancestorId != null)
        {
            if (!visited.Add(ancestorId.Value))
                return new Error(DomainErrorCodes.Cards.ParentCycle, "Parent cycle detected.");

            if (!cardMap.TryGetValue(ancestorId.Value, out var ancestorCard))
                break;

            ancestorId = ancestorCard.ParentCardId;
        }

        return null;
    }
}
