namespace HydraForge.Application.Realtime;

public enum BoardEntityType
{
    Project,
    Column,
    Card,
    ChecklistItem,
    Comment,
    Attachment,
    Spec,
    Plan,
    CardRelationship,
    ProjectDocument,
}

public enum BoardAction
{
    Created,
    Updated,
    Moved,
    Deleted,
    Archived,
    Restored,
    Assigned,
    Unassigned,
}

public record ProjectBoardEventEnvelope(
    Guid EventId,
    Guid ProjectId,
    BoardEntityType EntityType,
    Guid EntityId,
    BoardAction Action,
    int Version,
    DateTime OccurredAt,
    object Payload,
    // Populated for card-scoped sub-entities (Comment, ChecklistItem, Attachment, Spec,
    // Plan, CardRelationship) so a client with a specific card open can tell "does this
    // event belong to what I'm looking at" without EntityId alone — EntityId is always
    // the sub-entity's own id (the comment's id, not the card it's on). Null for entity
    // types that don't hang off a single card (Project, Column) or where EntityId already
    // IS the card id (Card itself).
    Guid? CardId = null
);
