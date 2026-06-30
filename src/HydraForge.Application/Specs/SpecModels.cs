namespace HydraForge.Application.Specs;

using HydraForge.Domain.Enums;

public record CreateSpecCommand(
    Guid ProjectId,
    Guid CardId,
    Guid ActorId,
    DocType DocType,
    string Title,
    string? Description,
    string Content
);

public record UpdateSpecCommand(
    Guid ProjectId,
    Guid SpecId,
    Guid ActorId,
    string Title,
    string? Description,
    string Content
);

public record RestoreSpecVersionCommand(
    Guid ProjectId,
    Guid SpecId,
    int Version,
    Guid ActorId
);

public record SpecDto(
    Guid Id,
    Guid ProjectId,
    Guid CardId,
    DocType DocType,
    string Title,
    string? Description,
    string Content,
    int Version,
    Guid CreatedByUserId,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record SpecVersionDto(
    Guid Id,
    Guid SpecId,
    int Version,
    string Title,
    string? Description,
    string Content,
    DateTime CreatedAt,
    Guid CreatedByUserId
);

public record SpecListFilter(bool IncludeArchived = false);

public record CreateSpecRequest(
    DocType DocType,
    string Title,
    string? Description,
    string Content
);

public record UpdateSpecRequest(
    string Title,
    string? Description,
    string Content
);

public record RestoreSpecVersionRequest(
    int Version
);

public record SpecResponse(
    Guid Id,
    Guid ProjectId,
    Guid CardId,
    DocType DocType,
    string Title,
    string? Description,
    string Content,
    int Version,
    Guid CreatedByUserId,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record SpecVersionResponse(
    Guid Id,
    Guid SpecId,
    int Version,
    string Title,
    string? Description,
    string Content,
    DateTime CreatedAt,
    Guid CreatedByUserId
);

public record SpecListResponse(IReadOnlyList<SpecResponse> Specs);

public record SpecVersionListResponse(IReadOnlyList<SpecVersionResponse> Versions);