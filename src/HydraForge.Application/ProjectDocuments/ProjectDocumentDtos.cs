using HydraForge.Domain.Enums;

namespace HydraForge.Application.ProjectDocuments;

public record CreateProjectDocumentCommand(
    Guid ProjectId,
    ProjectDocType DocType,
    string Title,
    string? Description,
    string Content,
    Guid ActorId
);

public record UpdateProjectDocumentCommand(
    Guid ProjectId,
    Guid DocumentId,
    string Title,
    string? Description,
    string Content,
    Guid ActorId
);

public record RestoreProjectDocumentVersionCommand(
    Guid ProjectId,
    Guid DocumentId,
    int Version,
    Guid ActorId
);

public record ProjectDocumentDto(
    Guid Id,
    Guid ProjectId,
    ProjectDocType DocType,
    string Title,
    string? Description,
    string Content,
    int Version,
    Guid CreatedByUserId,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? ArchivedAt
);

public record ProjectDocumentVersionDto(
    Guid Id,
    Guid ProjectDocumentId,
    int Version,
    string Title,
    string? Description,
    string Content,
    DateTime CreatedAt,
    Guid CreatedByUserId
);
