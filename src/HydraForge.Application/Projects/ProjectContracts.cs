using HydraForge.Domain.Enums;

namespace HydraForge.Application.Projects;

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

public record ToggleProjectArchiveCommand(Guid ProjectId, Guid ActorId);

public record AddProjectMemberCommand(
    Guid ProjectId,
    Guid UserId,
    MemberRole Role,
    Guid AddedByUserId
);

public record UpdateProjectMemberCommand(
    Guid ProjectId,
    Guid MemberId,
    MemberRole NewRole,
    Guid ChangedByUserId
);

public record RemoveProjectMemberCommand(Guid ProjectId, Guid MemberId, Guid RemovedByUserId);

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
