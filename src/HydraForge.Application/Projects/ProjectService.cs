using HydraForge.Application.Audit;
using HydraForge.Application.ProjectSnapshots;
using HydraForge.Application.Realtime;
using HydraForge.Domain.Common;
using HydraForge.Domain.Entities.ProjectSpace;
using HydraForge.Domain.Enums;

namespace HydraForge.Application.Projects;

public class ProjectService(
    IProjectRepository projectRepo,
    IColumnRepository columnRepo,
    IProjectMemberRepository memberRepo,
    IProjectContextSnapshotRepository snapshotRepo,
    IChatArchiveService chatArchiveService,
    IProjectSnapshotRefresher snapshotRefresher,
    IProjectBoardEventPublisher publisher,
    IAuditLogWriter auditLogWriter
)
{
    private readonly IProjectSnapshotRefresher _snapshotRefresher = snapshotRefresher;
    private readonly IProjectBoardEventPublisher _publisher = publisher;
    private readonly IAuditLogWriter _auditLogWriter = auditLogWriter;
    private static readonly string[] DefaultColumnNames =
    [
        "Backlog",
        "Spec-ing",
        "Planned",
        "In Dev",
        "In Review",
        "Done",
    ];

    public async Task<Result<ProjectDto>> CreateAsync(
        CreateProjectCommand cmd,
        CancellationToken ct = default
    )
    {
        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = cmd.Name,
            Description = cmd.Description,
            GitRemoteUrl = cmd.GitRemoteUrl,
            GitProvider = cmd.GitProvider,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        await projectRepo.AddAsync(project, ct);

        var ownerMember = new ProjectMember
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            UserId = cmd.OwnerId,
            Role = MemberRole.Owner,
            JoinedAt = DateTime.UtcNow,
        };
        await memberRepo.AddMemberAsync(ownerMember, ct);

        var columns = DefaultColumnNames
            .Select(
                (name, index) =>
                    new Column
                    {
                        Id = Guid.NewGuid(),
                        ProjectId = project.Id,
                        Name = name,
                        Position = index,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                    }
            )
            .ToList();

        await columnRepo.AddRangeAsync(columns, ct);

        var snapshot = new ProjectContextSnapshot
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            TemplateContent = "{}",
            TemplateGeneratedAt = DateTime.UtcNow,
        };
        await snapshotRepo.AddAsync(snapshot, ct);
        await _publisher.PublishAsync(new ProjectBoardEventEnvelope(
            Guid.NewGuid(), project.Id, BoardEntityType.Project, project.Id, BoardAction.Created, 1, DateTime.UtcNow, null!), ct);

        await _auditLogWriter.WriteAsync(
            new AuditLogRequest(
                cmd.OwnerId,
                AuditLogScope.Project,
                "Project",
                project.Id,
                "Created",
                project.Id,
                null,
                null
            ),
            ct
        );

        return Result<ProjectDto>.Success(MapToDto(project, columns, [ownerMember]));
    }

    public async Task<Result<ProjectDto>> GetByIdAsync(
        Guid projectId,
        Guid requestUserId,
        CancellationToken ct = default
    )
    {
        var project = await projectRepo.GetByIdAsync(projectId, ct);
        if (project == null)
            return Result<ProjectDto>.Failure(
                new Error(DomainErrorCodes.Projects.NotFound, "Project not found.")
            );

        var membership = await memberRepo.GetByProjectAndUserAsync(projectId, requestUserId, ct);
        if (membership == null)
            return Result<ProjectDto>.Failure(
                new Error(
                    DomainErrorCodes.Projects.MembershipDenied,
                    "Access denied."
                )
            );

        var columns = await columnRepo.GetByProjectIdAsync(projectId, ct);
        var members = await memberRepo.ListMembersAsync(projectId, ct);

        return Result<ProjectDto>.Success(MapToDto(project, columns, members));
    }

    public async Task<Result<IReadOnlyList<ProjectListDto>>> GetAllAsync(
        Guid requestUserId,
        bool includeArchived = false,
        CancellationToken ct = default
    )
    {
        var projects = await projectRepo.ListByUserIdAsync(requestUserId, ct);
        var filteredProjects = includeArchived
            ? projects.ToList()
            : projects.Where(p => p.ArchivedAt == null).ToList();

        var memberCounts = await memberRepo.GetMemberCountsAsync(
            filteredProjects.Select(p => p.Id),
            ct
        );

        var result = filteredProjects.Select(project => new ProjectListDto(
            project.Id,
            project.Name,
            project.Description,
            project.CreatedAt,
            project.ArchivedAt,
            memberCounts.GetValueOrDefault(project.Id, 0)
        )).ToList();

        return Result<IReadOnlyList<ProjectListDto>>.Success(result);
    }

    public async Task<Result<ProjectDto>> UpdateAsync(
        UpdateProjectCommand cmd,
        CancellationToken ct = default
    )
    {
        var project = await projectRepo.GetByIdAsync(cmd.ProjectId, ct);
        if (project == null)
            return Result<ProjectDto>.Failure(
                new Error(DomainErrorCodes.Projects.NotFound, "Project not found.")
            );

        if (project.ArchivedAt != null)
            return Result<ProjectDto>.Failure(
                new Error(
                    DomainErrorCodes.Projects.Archived,
                    "Cannot update archived project."
                )
            );

        var membership = await memberRepo.GetByProjectAndUserAsync(cmd.ProjectId, cmd.ActorId, ct);
        if (membership == null)
            return Result<ProjectDto>.Failure(
                new Error(
                    DomainErrorCodes.Projects.MembershipDenied,
                    "Access denied."
                )
            );

        if (membership.Role != MemberRole.Owner && membership.Role != MemberRole.Member)
            return Result<ProjectDto>.Failure(
                new Error(
                    DomainErrorCodes.Projects.OwnerRequired,
                    "Owner or Member role required."
                )
            );

        project.UpdateDetails(cmd.Name, cmd.Description, cmd.GitRemoteUrl, cmd.GitProvider);

        await projectRepo.UpdateAsync(project, ct);
        await _snapshotRefresher.RefreshAsync(cmd.ProjectId, ct);
        await _publisher.PublishAsync(new ProjectBoardEventEnvelope(
            Guid.NewGuid(), project.Id, BoardEntityType.Project, project.Id, BoardAction.Updated, 1, DateTime.UtcNow, null!), ct);

        await _auditLogWriter.WriteAsync(
            new AuditLogRequest(
                cmd.ActorId,
                AuditLogScope.Project,
                "Project",
                project.Id,
                "Updated",
                project.Id,
                null,
                null
            ),
            ct
        );

        var columns = await columnRepo.GetByProjectIdAsync(cmd.ProjectId, ct);
        var members = await memberRepo.ListMembersAsync(cmd.ProjectId, ct);

        return Result<ProjectDto>.Success(MapToDto(project, columns, members));
    }

    public async Task<Result> ArchiveAsync(
        ArchiveProjectCommand cmd,
        CancellationToken ct = default
    )
    {
        var project = await projectRepo.GetByIdAsync(cmd.ProjectId, ct);
        if (project == null)
            return Result.Failure(
                new Error(DomainErrorCodes.Projects.NotFound, "Project not found.")
            );

        var membership = await memberRepo.GetByProjectAndUserAsync(cmd.ProjectId, cmd.ActorId, ct);
        if (membership == null)
            return Result.Failure(
                new Error(
                    DomainErrorCodes.Projects.MembershipDenied,
                    "Access denied."
                )
            );

        if (membership.Role != MemberRole.Owner)
            return Result.Failure(
                new Error(
                    DomainErrorCodes.Projects.OwnerRequired,
                    "Owner role required."
                )
            );

        project.Archive();

        await projectRepo.UpdateAsync(project, ct);
        await chatArchiveService.ArchiveProjectAsync(cmd.ProjectId, ct);
        await _snapshotRefresher.RefreshAsync(cmd.ProjectId, ct);
        await _publisher.PublishAsync(new ProjectBoardEventEnvelope(
            Guid.NewGuid(), project.Id, BoardEntityType.Project, project.Id, BoardAction.Archived, 1, DateTime.UtcNow, null!), ct);

        await _auditLogWriter.WriteAsync(
            new AuditLogRequest(
                cmd.ActorId,
                AuditLogScope.Project,
                "Project",
                project.Id,
                "Archived",
                project.Id,
                null,
                null
            ),
            ct
        );

        return Result.Success();
    }

    public async Task<Result> RestoreAsync(
        RestoreProjectCommand cmd,
        CancellationToken ct = default
    )
    {
        var project = await projectRepo.GetByIdAsync(cmd.ProjectId, ct);
        if (project == null)
            return Result.Failure(
                new Error(DomainErrorCodes.Projects.NotFound, "Project not found.")
            );

        if (project.ArchivedAt == null)
            return Result.Failure(
                new Error(DomainErrorCodes.Projects.Archived, "Project is not archived.")
            );

        var membership = await memberRepo.GetByProjectAndUserAsync(cmd.ProjectId, cmd.ActorId, ct);
        if (membership == null)
            return Result.Failure(
                new Error(DomainErrorCodes.Projects.MembershipDenied, "Access denied.")
            );

        if (membership.Role != MemberRole.Owner)
            return Result.Failure(
                new Error(DomainErrorCodes.Projects.OwnerRequired, "Owner role required.")
            );

        project.Restore();

        await projectRepo.UpdateAsync(project, ct);
        await _snapshotRefresher.RefreshAsync(cmd.ProjectId, ct);
        await _publisher.PublishAsync(new ProjectBoardEventEnvelope(
            Guid.NewGuid(), project.Id, BoardEntityType.Project, project.Id, BoardAction.Restored, 1, DateTime.UtcNow, null!), ct);

        await _auditLogWriter.WriteAsync(
            new AuditLogRequest(cmd.ActorId, AuditLogScope.Project, "Project", project.Id, "Restored", project.Id, null, null), ct);

        return Result.Success();
    }

    public async Task<Result> DeleteAsync(DeleteProjectCommand cmd, CancellationToken ct = default)
    {
        var project = await projectRepo.GetByIdAsync(cmd.ProjectId, ct);
        if (project == null)
            return Result.Failure(
                new Error(DomainErrorCodes.Projects.NotFound, "Project not found.")
            );

        var membership = await memberRepo.GetByProjectAndUserAsync(cmd.ProjectId, cmd.ActorId, ct);
        if (membership == null)
            return Result.Failure(
                new Error(
                    DomainErrorCodes.Projects.MembershipDenied,
                    "Access denied."
                )
            );

        if (membership.Role != MemberRole.Owner)
            return Result.Failure(
                new Error(
                    DomainErrorCodes.Projects.OwnerRequired,
                    "Owner role required."
                )
            );

        await projectRepo.UpdateAsync(project, ct);
        await _publisher.PublishAsync(new ProjectBoardEventEnvelope(
            Guid.NewGuid(), project.Id, BoardEntityType.Project, project.Id, BoardAction.Deleted, 1, DateTime.UtcNow, null!), ct);

        await _auditLogWriter.WriteAsync(
            new AuditLogRequest(
                cmd.ActorId,
                AuditLogScope.Project,
                "Project",
                project.Id,
                "Deleted",
                project.Id,
                null,
                null
            ),
            ct
        );

        return Result.Success();
    }

    private static ProjectDto MapToDto(
        Project project,
        IReadOnlyList<Column> columns,
        IReadOnlyList<ProjectMember> members
    )
    {
        return new ProjectDto(
            project.Id,
            project.Name,
            project.Description,
            project.GitRemoteUrl,
            project.GitProvider,
            project.CreatedAt,
            project.UpdatedAt,
            project.ArchivedAt,
            columns
                .Select(c => new ColumnDto(c.Id, c.Name, c.Position, c.WipLimit, c.Color))
                .ToList(),
            members.Select(m => new ProjectMemberDto(m.Id, m.UserId, m.User?.Username ?? string.Empty, m.Role, m.JoinedAt)).ToList()
        );
    }
}
