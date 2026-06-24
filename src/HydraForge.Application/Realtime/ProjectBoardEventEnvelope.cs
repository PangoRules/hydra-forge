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
    object Payload
);
