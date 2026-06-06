namespace HydraForge.Application.Columns;

public record CreateColumnCommand(
    Guid ProjectId,
    string Name,
    string? Color,
    int? WipLimit,
    Guid ActorId
);

public record UpdateColumnCommand(
    Guid ProjectId,
    Guid ColumnId,
    string Name,
    string? Color,
    int? WipLimit,
    Guid ActorId
);

public record DeleteColumnCommand(
    Guid ProjectId,
    Guid ColumnId,
    Guid ActorId
);

public record ReorderColumnsCommand(
    Guid ProjectId,
    IReadOnlyList<Guid> ColumnIds,
    Guid ActorId
);

public record ColumnDto(
    Guid Id,
    string Name,
    int Position,
    int? WipLimit,
    string? Color
);

public record CreateColumnRequest(
    string Name,
    string? Color,
    int? WipLimit
);

public record UpdateColumnRequest(
    string Name,
    string? Color,
    int? WipLimit
);

public record ReorderColumnsRequest(
    IReadOnlyList<Guid> ColumnIds
);

public record ColumnResponse(
    Guid Id,
    string Name,
    int Position,
    int? WipLimit,
    string? Color
);