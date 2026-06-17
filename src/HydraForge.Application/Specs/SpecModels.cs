namespace HydraForge.Application.Specs;

public record CreateSpecCommand(
    Guid ProjectId,
    Guid ActorId,
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

public record LinkSpecToCardCommand(
    Guid ProjectId,
    Guid SpecId,
    Guid CardId,
    Guid ActorId
);

public record UnlinkSpecFromCardCommand(
    Guid ProjectId,
    Guid SpecId,
    Guid CardId,
    Guid ActorId
);

public record SpecDto(
    Guid Id,
    Guid ProjectId,
    string Title,
    string? Description,
    string Content,
    int Version,
    Guid CreatedByUserId,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    Guid? LinkedCardId
);

public record SpecVersionDto(
    Guid Id,
    Guid SpecId,
    int Version,
    string Content,
    DateTime CreatedAt,
    Guid CreatedByUserId
);

public record SpecListFilter(bool IncludeArchived = false);

public record CreateSpecRequest(
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

public record LinkSpecToCardRequest(
    Guid CardId
);

public record SpecResponse(
    Guid Id,
    Guid ProjectId,
    string Title,
    string? Description,
    string Content,
    int Version,
    Guid CreatedByUserId,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    Guid? LinkedCardId
);

public record SpecVersionResponse(
    Guid Id,
    Guid SpecId,
    int Version,
    string Content,
    DateTime CreatedAt,
    Guid CreatedByUserId
);

public record SpecListResponse(IReadOnlyList<SpecResponse> Specs);

public record SpecVersionListResponse(IReadOnlyList<SpecVersionResponse> Versions);
