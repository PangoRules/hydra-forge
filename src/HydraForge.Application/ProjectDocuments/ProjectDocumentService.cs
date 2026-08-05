using HydraForge.Application.Audit;
using HydraForge.Application.Auth;
using HydraForge.Application.Projects;
using HydraForge.Application.ProjectSnapshots;
using HydraForge.Application.Realtime;
using HydraForge.Application.Shared;
using HydraForge.Domain.Common;
using HydraForge.Domain.Entities.ProjectSpace;
using HydraForge.Domain.Enums;

namespace HydraForge.Application.ProjectDocuments;

public class ProjectDocumentService(
    IProjectDocumentRepository docRepo,
    IProjectMemberRepository memberRepo,
    IUserRepository userRepo,
    IAuditLogWriter auditLogWriter,
    IProjectSnapshotRefresher snapshotRefresher,
    IProjectBoardEventPublisher publisher
)
{
    private readonly IProjectDocumentRepository _docRepo = docRepo;
    private readonly IProjectMemberRepository _memberRepo = memberRepo;
    private readonly IUserRepository _userRepo = userRepo;
    private readonly IAuditLogWriter _auditLogWriter = auditLogWriter;
    private readonly IProjectSnapshotRefresher _snapshotRefresher = snapshotRefresher;
    private readonly IProjectBoardEventPublisher _publisher = publisher;

    private static readonly HashSet<ProjectDocType> UniqueDocTypes = new()
    {
        ProjectDocType.Scope,
        ProjectDocType.Glossary,
        ProjectDocType.DataModel,
        ProjectDocType.Architecture,
        ProjectDocType.FunctionalSpec,
        ProjectDocType.Decisions,
    };

    private sealed record DocAuditSnapshot(string Title, string? Description, string Content);

    private static DocAuditSnapshot BuildSnapshot(ProjectDocument doc) =>
        new(doc.Title, doc.Description, doc.Content);

    public async Task<Result<ProjectDocumentDto>> CreateAsync(
        CreateProjectDocumentCommand cmd,
        CancellationToken ct = default
    )
    {
        if (!await MembershipGuard.HasAccessAsync(_userRepo, _memberRepo, cmd.ProjectId, cmd.ActorId, ct))
            return Result<ProjectDocumentDto>.Failure(
                new Error(DomainErrorCodes.Projects.MembershipDenied, "Access denied."));

        if (cmd.Content.Length > DocumentMarkdownLimits.MaxMarkdownPayloadBytes)
            return Result<ProjectDocumentDto>.Failure(
                new Error(DomainErrorCodes.ProjectDocuments.MarkdownPayloadTooLarge,
                    "Markdown payload exceeds limit."));

        if (UniqueDocTypes.Contains(cmd.DocType))
        {
            var existing = await _docRepo.GetByDocTypeAsync(cmd.ProjectId, cmd.DocType, ct);
            if (existing != null && existing.ArchivedAt == null)
                return Result<ProjectDocumentDto>.Failure(
                    new Error(DomainErrorCodes.ProjectDocuments.DuplicateDocType,
                        $"A {cmd.DocType} document already exists for this project."));
        }

        var doc = new ProjectDocument
        {
            Id = Guid.NewGuid(),
            ProjectId = cmd.ProjectId,
            DocType = cmd.DocType,
            Title = cmd.Title,
            Description = cmd.Description,
            Content = cmd.Content,
            Version = 1,
            CreatedByUserId = cmd.ActorId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        var version = new ProjectDocumentVersion
        {
            Id = Guid.NewGuid(),
            ProjectDocumentId = doc.Id,
            Version = 1,
            Title = cmd.Title,
            Description = cmd.Description,
            Content = cmd.Content,
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = cmd.ActorId,
        };

        await _docRepo.AddAsync(doc, ct);
        await _docRepo.AddVersionAsync(version, ct);
        await _docRepo.SaveChangesAsync(ct);
        await _snapshotRefresher.RefreshAsync(cmd.ProjectId, ct);

        await _auditLogWriter.WriteAsync(new AuditLogRequest(
            cmd.ActorId, AuditLogScope.Project, "ProjectDocument", doc.Id, "Created",
            cmd.ProjectId, null, AuditSnapshot.Serialize(BuildSnapshot(doc))), ct);

        await PublishAsync(cmd.ProjectId, doc.Id, BoardAction.Created, ct);

        return Result<ProjectDocumentDto>.Success(MapToDto(doc));
    }

    public async Task<Result<ProjectDocumentDto>> GetByIdAsync(
        Guid projectId,
        Guid documentId,
        Guid actorId,
        CancellationToken ct = default
    )
    {
        if (!await MembershipGuard.HasAccessAsync(_userRepo, _memberRepo, projectId, actorId, ct))
            return Result<ProjectDocumentDto>.Failure(
                new Error(DomainErrorCodes.Projects.MembershipDenied, "Access denied."));

        var doc = await _docRepo.GetByIdAsync(documentId, ct);
        if (doc == null || doc.ProjectId != projectId)
            return Result<ProjectDocumentDto>.Failure(
                new Error(DomainErrorCodes.ProjectDocuments.NotFound, "Document not found."));

        return Result<ProjectDocumentDto>.Success(MapToDto(doc));
    }

    public async Task<Result<IReadOnlyList<ProjectDocumentDto>>> ListAsync(
        Guid projectId,
        Guid actorId,
        CancellationToken ct = default
    )
    {
        if (!await MembershipGuard.HasAccessAsync(_userRepo, _memberRepo, projectId, actorId, ct))
            return Result<IReadOnlyList<ProjectDocumentDto>>.Failure(
                new Error(DomainErrorCodes.Projects.MembershipDenied, "Access denied."));

        var docs = await _docRepo.ListByProjectAsync(projectId, ct);
        var dtos = docs.Select(MapToDto).ToList();
        return Result<IReadOnlyList<ProjectDocumentDto>>.Success(dtos);
    }

    public async Task<Result<ProjectDocumentDto>> UpdateAsync(
        UpdateProjectDocumentCommand cmd,
        CancellationToken ct = default
    )
    {
        if (!await MembershipGuard.HasAccessAsync(_userRepo, _memberRepo, cmd.ProjectId, cmd.ActorId, ct))
            return Result<ProjectDocumentDto>.Failure(
                new Error(DomainErrorCodes.Projects.MembershipDenied, "Access denied."));

        var doc = await _docRepo.GetByIdAsync(cmd.DocumentId, ct);
        if (doc == null || doc.ProjectId != cmd.ProjectId)
            return Result<ProjectDocumentDto>.Failure(
                new Error(DomainErrorCodes.ProjectDocuments.NotFound, "Document not found."));

        if (cmd.Content.Length > DocumentMarkdownLimits.MaxMarkdownPayloadBytes)
            return Result<ProjectDocumentDto>.Failure(
                new Error(DomainErrorCodes.ProjectDocuments.MarkdownPayloadTooLarge,
                    "Markdown payload exceeds limit."));

        var oldSnapshot = BuildSnapshot(doc);
        doc.Title = cmd.Title;
        doc.Description = cmd.Description;
        doc.Content = cmd.Content;
        doc.Version += 1;
        doc.UpdatedAt = DateTime.UtcNow;

        var version = new ProjectDocumentVersion
        {
            Id = Guid.NewGuid(),
            ProjectDocumentId = doc.Id,
            Version = doc.Version,
            Title = cmd.Title,
            Description = cmd.Description,
            Content = cmd.Content,
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = cmd.ActorId,
        };

        await _docRepo.UpdateAsync(doc, ct);
        await _docRepo.AddVersionAsync(version, ct);
        await _docRepo.SaveChangesAsync(ct);
        await _snapshotRefresher.RefreshAsync(cmd.ProjectId, ct);

        await _auditLogWriter.WriteAsync(new AuditLogRequest(
            cmd.ActorId, AuditLogScope.Project, "ProjectDocument", doc.Id, "Updated",
            cmd.ProjectId, AuditSnapshot.Serialize(oldSnapshot),
            AuditSnapshot.Serialize(BuildSnapshot(doc))), ct);

        await PublishAsync(cmd.ProjectId, doc.Id, BoardAction.Updated, ct);

        return Result<ProjectDocumentDto>.Success(MapToDto(doc));
    }

    public async Task<Result<IReadOnlyList<ProjectDocumentVersionDto>>> ListVersionsAsync(
        Guid projectId,
        Guid documentId,
        Guid actorId,
        CancellationToken ct = default
    )
    {
        if (!await MembershipGuard.HasAccessAsync(_userRepo, _memberRepo, projectId, actorId, ct))
            return Result<IReadOnlyList<ProjectDocumentVersionDto>>.Failure(
                new Error(DomainErrorCodes.Projects.MembershipDenied, "Access denied."));

        var doc = await _docRepo.GetByIdAsync(documentId, ct);
        if (doc == null || doc.ProjectId != projectId)
            return Result<IReadOnlyList<ProjectDocumentVersionDto>>.Failure(
                new Error(DomainErrorCodes.ProjectDocuments.NotFound, "Document not found."));

        var versions = await _docRepo.ListVersionsAsync(documentId, ct);
        return Result<IReadOnlyList<ProjectDocumentVersionDto>>.Success([
            .. versions.Select(v => new ProjectDocumentVersionDto(
                v.Id,
                v.ProjectDocumentId,
                v.Version,
                v.Title,
                v.Description,
                v.Content,
                v.CreatedAt,
                v.CreatedByUserId
            )),
        ]);
    }

    public async Task<Result<ProjectDocumentDto>> RestoreVersionAsync(
        RestoreProjectDocumentVersionCommand cmd,
        CancellationToken ct = default
    )
    {
        if (!await MembershipGuard.HasAccessAsync(_userRepo, _memberRepo, cmd.ProjectId, cmd.ActorId, ct))
            return Result<ProjectDocumentDto>.Failure(
                new Error(DomainErrorCodes.Projects.MembershipDenied, "Access denied."));

        var doc = await _docRepo.GetByIdAsync(cmd.DocumentId, ct);
        if (doc == null || doc.ProjectId != cmd.ProjectId)
            return Result<ProjectDocumentDto>.Failure(
                new Error(DomainErrorCodes.ProjectDocuments.NotFound, "Document not found."));

        var oldVersion = await _docRepo.GetVersionAsync(cmd.DocumentId, cmd.Version, ct);
        if (oldVersion == null)
            return Result<ProjectDocumentDto>.Failure(
                new Error(DomainErrorCodes.ProjectDocuments.DocumentVersionNotFound,
                    "Document version not found."));

        var oldSnapshot = BuildSnapshot(doc);
        doc.Title = oldVersion.Title;
        doc.Description = oldVersion.Description;
        doc.Content = oldVersion.Content;
        doc.Version += 1;
        doc.UpdatedAt = DateTime.UtcNow;

        var newVersion = new ProjectDocumentVersion
        {
            Id = Guid.NewGuid(),
            ProjectDocumentId = doc.Id,
            Version = doc.Version,
            Title = oldVersion.Title,
            Description = oldVersion.Description,
            Content = oldVersion.Content,
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = cmd.ActorId,
        };

        await _docRepo.UpdateAsync(doc, ct);
        await _docRepo.AddVersionAsync(newVersion, ct);
        await _docRepo.SaveChangesAsync(ct);
        await _snapshotRefresher.RefreshAsync(cmd.ProjectId, ct);

        await _auditLogWriter.WriteAsync(new AuditLogRequest(
            cmd.ActorId, AuditLogScope.Project, "ProjectDocument", doc.Id, "Restored",
            cmd.ProjectId, AuditSnapshot.Serialize(oldSnapshot),
            AuditSnapshot.Serialize(BuildSnapshot(doc))), ct);

        await PublishAsync(cmd.ProjectId, doc.Id, BoardAction.Restored, ct);

        return Result<ProjectDocumentDto>.Success(MapToDto(doc));
    }

    private async Task PublishAsync(Guid projectId, Guid docId, BoardAction action, CancellationToken ct)
    {
        var envelope = new ProjectBoardEventEnvelope(
            Guid.NewGuid(), projectId, BoardEntityType.ProjectDocument, docId,
            action, 1, DateTime.UtcNow, null!, null);
        await _publisher.PublishAsync(envelope, ct);
    }

    private static ProjectDocumentDto MapToDto(ProjectDocument doc) =>
        new(doc.Id, doc.ProjectId, doc.DocType, doc.Title, doc.Description, doc.Content,
            doc.Version, doc.CreatedByUserId, doc.CreatedAt, doc.UpdatedAt, doc.ArchivedAt);
}
