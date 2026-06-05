using HydraForge.Domain.Common;
using HydraForge.Domain.Entities.ProjectSpace;
using HydraForge.Domain.Enums;

namespace HydraForge.Application.Projects;

// Commands remain here since they are part of the Application layer use cases

public record CreateProjectCommand(
    Guid OwnerId,
    string Name,
    string Description,
    string? GitRemoteUrl,
    string? GitProvider
);

public record UpdateProjectCommand(
    Guid ProjectId,
    Guid ActorId,
    string Name,
    string Description,
    string? GitRemoteUrl,
    string? GitProvider
);

public record ArchiveProjectCommand(Guid ProjectId, Guid ActorId);

public record DeleteProjectCommand(Guid ProjectId, Guid ActorId);

public record AddProjectMemberCommand(
    Guid ProjectId,
    Guid UserId,
    MemberRole Role,
    Guid AddedByUserId
);

public record UpdateProjectMemberCommand(
    Guid ProjectId,
    Guid UserId,
    MemberRole NewRole,
    Guid ChangedByUserId
);

public record RemoveProjectMemberCommand(Guid ProjectId, Guid UserId, Guid RemovedByUserId);

// DTOs remain here for internal use within Application layer

public record ProjectDto(
    Guid Id,
    string Name,
    string Description,
    string? GitRemoteUrl,
    string? GitProvider,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? ArchivedAt,
    IReadOnlyList<ColumnDto> Columns,
    IReadOnlyList<ProjectMemberDto> Members
);

public record ColumnDto(Guid Id, string Name, int Position, int? WipLimit, string? Color);

public record ProjectMemberDto(Guid Id, Guid UserId, string Username, MemberRole Role, DateTime JoinedAt);

public record ProjectListDto(
    Guid Id,
    string Name,
    string Description,
    DateTime CreatedAt,
    DateTime? ArchivedAt,
    int MemberCount
);

// Use Cases

public class ProjectService(
    IProjectRepository projectRepo,
    IColumnRepository columnRepo,
    IProjectMemberRepository memberRepo,
    IProjectContextSnapshotRepository snapshotRepo,
    IChatArchiveService chatArchiveService,
    HydraForge.Application.Auth.IUserRepository userRepo
)
{
    private readonly IProjectRepository _projectRepo = projectRepo;
    private readonly IColumnRepository _columnRepo = columnRepo;
    private readonly IProjectMemberRepository _memberRepo = memberRepo;
    private readonly IProjectContextSnapshotRepository _snapshotRepo = snapshotRepo;
    private readonly IChatArchiveService _chatArchiveService = chatArchiveService;
    private readonly HydraForge.Application.Auth.IUserRepository _userRepo = userRepo;

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

        await _projectRepo.AddAsync(project, ct);

        var ownerMember = new ProjectMember
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            UserId = cmd.OwnerId,
            Role = MemberRole.Owner,
            JoinedAt = DateTime.UtcNow,
        };
        await _memberRepo.AddMemberAsync(ownerMember, ct);

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

        await _columnRepo.AddRangeAsync(columns, ct);

        var snapshot = new ProjectContextSnapshot
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            TemplateContent = "{}",
            TemplateGeneratedAt = DateTime.UtcNow,
        };
        await _snapshotRepo.AddAsync(snapshot, ct);

        return Result<ProjectDto>.Success(MapToDto(project, columns, [ownerMember]));
    }

    public async Task<Result<ProjectDto>> GetByIdAsync(
        Guid projectId,
        Guid requestUserId,
        CancellationToken ct = default
    )
    {
        var project = await _projectRepo.GetByIdAsync(projectId, ct);
        if (project == null)
            return Result<ProjectDto>.Failure(
                new Domain.Common.Error(DomainErrorCodes.Projects.NotFound, "Project not found.")
            );

        if (project.ArchivedAt != null)
            return Result<ProjectDto>.Failure(
                new Domain.Common.Error(DomainErrorCodes.Projects.Archived, "Project is archived.")
            );

        var membership = await _memberRepo.GetByProjectAndUserAsync(projectId, requestUserId, ct);
        if (membership == null)
            return Result<ProjectDto>.Failure(
                new Domain.Common.Error(
                    DomainErrorCodes.Projects.MembershipDenied,
                    "Access denied."
                )
            );

        var columns = await _columnRepo.GetByProjectIdAsync(projectId, ct);
        var members = await _memberRepo.ListMembersAsync(projectId, ct);

        return Result<ProjectDto>.Success(MapToDto(project, columns, members));
    }

    public async Task<Result<IReadOnlyList<ProjectListDto>>> GetAllAsync(
        Guid requestUserId,
        CancellationToken ct = default
    )
    {
        var projects = await _projectRepo.ListByUserIdAsync(requestUserId, ct);
        var activeProjects = projects.Where(p => p.ArchivedAt == null).ToList();

        var memberCounts = await _memberRepo.GetMemberCountsAsync(
            activeProjects.Select(p => p.Id),
            ct
        );

        var result = activeProjects.Select(project => new ProjectListDto(
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
        var project = await _projectRepo.GetByIdAsync(cmd.ProjectId, ct);
        if (project == null)
            return Result<ProjectDto>.Failure(
                new Domain.Common.Error(DomainErrorCodes.Projects.NotFound, "Project not found.")
            );

        if (project.ArchivedAt != null)
            return Result<ProjectDto>.Failure(
                new Domain.Common.Error(
                    DomainErrorCodes.Projects.Archived,
                    "Cannot update archived project."
                )
            );

        var membership = await _memberRepo.GetByProjectAndUserAsync(cmd.ProjectId, cmd.ActorId, ct);
        if (membership == null)
            return Result<ProjectDto>.Failure(
                new Domain.Common.Error(
                    DomainErrorCodes.Projects.MembershipDenied,
                    "Access denied."
                )
            );

        if (membership.Role != MemberRole.Owner && membership.Role != MemberRole.Member)
            return Result<ProjectDto>.Failure(
                new Domain.Common.Error(
                    DomainErrorCodes.Projects.OwnerRequired,
                    "Owner or Member role required."
                )
            );

        project.Name = cmd.Name;
        project.Description = cmd.Description;
        project.GitRemoteUrl = cmd.GitRemoteUrl;
        project.GitProvider = cmd.GitProvider;
        project.UpdatedAt = DateTime.UtcNow;

        await _projectRepo.UpdateAsync(project, ct);

        var columns = await _columnRepo.GetByProjectIdAsync(cmd.ProjectId, ct);
        var members = await _memberRepo.ListMembersAsync(cmd.ProjectId, ct);

        return Result<ProjectDto>.Success(MapToDto(project, columns, members));
    }

    public async Task<Result> ArchiveAsync(
        ArchiveProjectCommand cmd,
        CancellationToken ct = default
    )
    {
        var project = await _projectRepo.GetByIdAsync(cmd.ProjectId, ct);
        if (project == null)
            return Result.Failure(
                new Domain.Common.Error(DomainErrorCodes.Projects.NotFound, "Project not found.")
            );

        var membership = await _memberRepo.GetByProjectAndUserAsync(cmd.ProjectId, cmd.ActorId, ct);
        if (membership == null)
            return Result.Failure(
                new Domain.Common.Error(
                    DomainErrorCodes.Projects.MembershipDenied,
                    "Access denied."
                )
            );

        if (membership.Role != MemberRole.Owner)
            return Result.Failure(
                new Domain.Common.Error(
                    DomainErrorCodes.Projects.OwnerRequired,
                    "Owner role required."
                )
            );

        project.ArchivedAt = DateTime.UtcNow;
        project.UpdatedAt = DateTime.UtcNow;

        await _projectRepo.UpdateAsync(project, ct);
        await _chatArchiveService.ArchiveProjectAsync(cmd.ProjectId, ct);

        return Result.Success();
    }

    public async Task<Result> DeleteAsync(DeleteProjectCommand cmd, CancellationToken ct = default)
    {
        var project = await _projectRepo.GetByIdAsync(cmd.ProjectId, ct);
        if (project == null)
            return Result.Failure(
                new Domain.Common.Error(DomainErrorCodes.Projects.NotFound, "Project not found.")
            );

        var membership = await _memberRepo.GetByProjectAndUserAsync(cmd.ProjectId, cmd.ActorId, ct);
        if (membership == null)
            return Result.Failure(
                new Domain.Common.Error(
                    DomainErrorCodes.Projects.MembershipDenied,
                    "Access denied."
                )
            );

        if (membership.Role != MemberRole.Owner)
            return Result.Failure(
                new Domain.Common.Error(
                    DomainErrorCodes.Projects.OwnerRequired,
                    "Owner role required."
                )
            );

        await _projectRepo.UpdateAsync(project, ct);

        return Result.Success();
    }

    public async Task<Result<ProjectMemberDto>> AddMemberAsync(
        AddProjectMemberCommand cmd,
        CancellationToken ct = default
    )
    {
        var project = await _projectRepo.GetByIdAsync(cmd.ProjectId, ct);
        if (project == null)
            return Result<ProjectMemberDto>.Failure(
                new Domain.Common.Error(DomainErrorCodes.Projects.NotFound, "Project not found.")
            );

        if (project.ArchivedAt != null)
            return Result<ProjectMemberDto>.Failure(
                new Domain.Common.Error(
                    DomainErrorCodes.Projects.Archived,
                    "Cannot add member to archived project."
                )
            );

        var actorMembership = await _memberRepo.GetByProjectAndUserAsync(
            cmd.ProjectId,
            cmd.AddedByUserId,
            ct
        );
        if (actorMembership == null)
            return Result<ProjectMemberDto>.Failure(
                new Domain.Common.Error(
                    DomainErrorCodes.Projects.MembershipDenied,
                    "Access denied."
                )
            );

        if (actorMembership.Role != MemberRole.Owner)
            return Result<ProjectMemberDto>.Failure(
                new Domain.Common.Error(
                    DomainErrorCodes.Projects.OwnerRequired,
                    "Owner role required."
                )
            );

        var existingMember = await _memberRepo.GetByProjectAndUserAsync(
            cmd.ProjectId,
            cmd.UserId,
            ct
        );
        if (existingMember != null)
            return Result<ProjectMemberDto>.Failure(
                new Domain.Common.Error(
                    DomainErrorCodes.Projects.MemberDuplicate,
                    "User is already a member."
                )
            );

        var newMember = new ProjectMember
        {
            Id = Guid.NewGuid(),
            ProjectId = cmd.ProjectId,
            UserId = cmd.UserId,
            Role = cmd.Role,
            JoinedAt = DateTime.UtcNow,
        };

        await _memberRepo.AddMemberAsync(newMember, ct);

        var user = await _userRepo.FindByIdAsync(newMember.UserId);

        return Result<ProjectMemberDto>.Success(
            new ProjectMemberDto(newMember.Id, newMember.UserId, user?.Username ?? string.Empty, newMember.Role, newMember.JoinedAt)
        );
    }

    public async Task<Result<ProjectMemberDto>> UpdateMemberAsync(
        UpdateProjectMemberCommand cmd,
        CancellationToken ct = default
    )
    {
        var project = await _projectRepo.GetByIdAsync(cmd.ProjectId, ct);
        if (project == null)
            return Result<ProjectMemberDto>.Failure(
                new Domain.Common.Error(DomainErrorCodes.Projects.NotFound, "Project not found.")
            );

        var actorMembership = await _memberRepo.GetByProjectAndUserAsync(
            cmd.ProjectId,
            cmd.ChangedByUserId,
            ct
        );
        if (actorMembership == null || actorMembership.Role != MemberRole.Owner)
            return Result<ProjectMemberDto>.Failure(
                new Domain.Common.Error(
                    DomainErrorCodes.Projects.OwnerRequired,
                    "Owner role required."
                )
            );

        var member = await _memberRepo.GetByProjectAndUserAsync(cmd.ProjectId, cmd.UserId, ct);
        if (member == null)
            return Result<ProjectMemberDto>.Failure(
                new Domain.Common.Error(DomainErrorCodes.Membership.NotFound, "Member not found.")
            );

        member.Role = cmd.NewRole;
        await _memberRepo.UpdateMemberAsync(member, ct);

        var user = await _userRepo.FindByIdAsync(member.UserId);

        return Result<ProjectMemberDto>.Success(
            new ProjectMemberDto(member.Id, member.UserId, user?.Username ?? string.Empty, member.Role, member.JoinedAt)
        );
    }

    public async Task<Result> RemoveMemberAsync(
        RemoveProjectMemberCommand cmd,
        CancellationToken ct = default
    )
    {
        var project = await _projectRepo.GetByIdAsync(cmd.ProjectId, ct);
        if (project == null)
            return Result.Failure(
                new Domain.Common.Error(DomainErrorCodes.Projects.NotFound, "Project not found.")
            );

        var actorMembership = await _memberRepo.GetByProjectAndUserAsync(
            cmd.ProjectId,
            cmd.RemovedByUserId,
            ct
        );
        if (actorMembership == null || actorMembership.Role != MemberRole.Owner)
            return Result.Failure(
                new Domain.Common.Error(
                    DomainErrorCodes.Projects.OwnerRequired,
                    "Owner role required."
                )
            );

        var member = await _memberRepo.GetByProjectAndUserAsync(cmd.ProjectId, cmd.UserId, ct);
        if (member == null)
            return Result.Failure(
                new Domain.Common.Error(DomainErrorCodes.Membership.NotFound, "Member not found.")
            );

        if (member.Role == MemberRole.Owner)
        {
            var allMembers = await _memberRepo.ListMembersAsync(cmd.ProjectId, ct);
            var ownerCount = allMembers.Count(m => m.Role == MemberRole.Owner);
            if (ownerCount <= 1)
                return Result.Failure(
                    new Domain.Common.Error(
                        DomainErrorCodes.Projects.LastOwnerRemovalDenied,
                        "Cannot remove the last owner."
                    )
                );
        }

        await _memberRepo.RemoveMemberAsync(member.Id, ct);

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
