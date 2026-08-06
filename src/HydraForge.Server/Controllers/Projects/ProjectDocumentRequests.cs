using HydraForge.Domain.Enums;

namespace HydraForge.Server.Controllers.Projects;

public record CreateProjectDocumentRequest(
    ProjectDocType DocType,
    string Title,
    string? Description,
    string Content
);

public record UpdateProjectDocumentRequest(string Title, string? Description, string Content);

public record RestoreProjectDocumentVersionRequest(int Version);

public record ProjectDocumentResponse(
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

public record ProjectDocumentListResponse(IReadOnlyList<ProjectDocumentResponse> Documents);

public record ProjectDocumentVersionResponse(
    Guid Id,
    Guid ProjectDocumentId,
    int Version,
    string Title,
    string? Description,
    string Content,
    DateTime CreatedAt,
    Guid CreatedByUserId
);

public record ProjectDocumentVersionListResponse(
    IReadOnlyList<ProjectDocumentVersionResponse> Versions
);
