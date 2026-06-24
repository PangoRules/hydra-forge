namespace HydraForge.Application.Checklist;

public record CreateChecklistItemCommand(
    Guid ProjectId,
    Guid CardId,
    Guid ActorId,
    string Text,
    Guid? AssignedTo,
    int? Position
);

public record UpdateChecklistItemCommand(
    Guid ProjectId,
    Guid CardId,
    Guid ItemId,
    Guid ActorId,
    string Text,
    Guid? AssignedTo
);

public record ToggleChecklistItemCommand(
    Guid ProjectId,
    Guid CardId,
    Guid ItemId,
    Guid ActorId
);

public record ReorderChecklistItemCommand(
    Guid ProjectId,
    Guid CardId,
    Guid ItemId,
    Guid ActorId,
    int NewPosition
);

public record DeleteChecklistItemCommand(
    Guid ProjectId,
    Guid CardId,
    Guid ItemId,
    Guid ActorId
);

public record ChecklistItemDto(
    Guid Id,
    Guid CardId,
    string Text,
    bool IsCompleted,
    int Position,
    Guid? AssignedTo,
    string? AssignedToUsername,
    DateTime CreatedAt
);

public record ChecklistItemResponse(
    Guid Id,
    Guid CardId,
    string Text,
    bool IsCompleted,
    int Position,
    Guid? AssignedTo,
    string? AssignedToUsername,
    DateTime CreatedAt
);

public record CreateChecklistItemRequest(
    string Text,
    Guid? AssignedTo = null,
    int? Position = null
);

public record UpdateChecklistItemRequest(
    string Text,
    Guid? AssignedTo = null
);

public record ReorderChecklistItemRequest(
    int NewPosition
);

public record ChecklistItemListResponse(
    IReadOnlyList<ChecklistItemResponse> Items
);