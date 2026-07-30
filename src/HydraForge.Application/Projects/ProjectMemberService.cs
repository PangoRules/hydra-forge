using HydraForge.Application.Audit;
using HydraForge.Application.Auth;
using HydraForge.Application.Realtime;
using HydraForge.Domain.Common;
using HydraForge.Domain.Entities.ProjectSpace;
using HydraForge.Domain.Enums;

namespace HydraForge.Application.Projects;

public class ProjectMemberService(
    IProjectRepository projectRepo,
    IProjectMemberRepository memberRepo,
    IUserRepository userRepo,
    IProjectBoardEventPublisher publisher,
    IAuditLogWriter auditLogWriter
)
{
    private readonly IProjectBoardEventPublisher _publisher = publisher;
    private readonly IAuditLogWriter _auditLogWriter = auditLogWriter;

    private sealed record ProjectMemberAuditSnapshot(Guid UserId, MemberRole Role);

    private static ProjectMemberAuditSnapshot BuildSnapshot(ProjectMember member) =>
        new(member.UserId, member.Role);

    private async Task PublishAsync(
        Guid projectId,
        BoardEntityType entityType,
        Guid entityId,
        BoardAction action,
        CancellationToken ct
    )
    {
        var envelope = new ProjectBoardEventEnvelope(
            Guid.NewGuid(),
            projectId,
            entityType,
            entityId,
            action,
            1,
            DateTime.UtcNow,
            null!
        );
        await _publisher.PublishAsync(envelope, ct);
    }

    public async Task<Result<ProjectMemberDto>> AddMemberAsync(
        AddProjectMemberCommand cmd,
        CancellationToken ct = default
    )
    {
        var project = await projectRepo.GetByIdAsync(cmd.ProjectId, ct);
        if (project == null)
            return Result<ProjectMemberDto>.Failure(
                new Error(DomainErrorCodes.Projects.NotFound, "Project not found.")
            );

        if (project.ArchivedAt != null)
            return Result<ProjectMemberDto>.Failure(
                new Error(
                    DomainErrorCodes.Projects.Archived,
                    "Cannot add member to archived project."
                )
            );

        if (
            !await MembershipGuard.HasAccessAsync(
                userRepo,
                memberRepo,
                cmd.ProjectId,
                cmd.AddedByUserId,
                ct
            )
        )
            return Result<ProjectMemberDto>.Failure(
                new Error(DomainErrorCodes.Projects.MembershipDenied, "Access denied.")
            );

        var actorMembership = await memberRepo.GetByProjectAndUserAsync(
            cmd.ProjectId,
            cmd.AddedByUserId,
            ct
        );
        if (actorMembership != null && actorMembership.Role != MemberRole.Owner)
            return Result<ProjectMemberDto>.Failure(
                new Error(DomainErrorCodes.Projects.OwnerRequired, "Owner role required.")
            );

        var existingMember = await memberRepo.GetByProjectAndUserAsync(
            cmd.ProjectId,
            cmd.UserId,
            ct
        );
        if (existingMember != null)
            return Result<ProjectMemberDto>.Failure(
                new Error(DomainErrorCodes.Projects.MemberDuplicate, "User is already a member.")
            );

        var newMember = new ProjectMember
        {
            Id = Guid.NewGuid(),
            ProjectId = cmd.ProjectId,
            UserId = cmd.UserId,
            Role = cmd.Role,
            JoinedAt = DateTime.UtcNow,
        };

        await memberRepo.AddMemberAsync(newMember, ct);

        await _auditLogWriter.WriteAsync(
            new AuditLogRequest(
                cmd.AddedByUserId,
                AuditLogScope.Project,
                "ProjectMember",
                newMember.Id,
                "Created",
                cmd.ProjectId,
                null,
                AuditSnapshot.Serialize(BuildSnapshot(newMember))
            ),
            ct
        );

        await PublishAsync(
            cmd.ProjectId,
            BoardEntityType.Card,
            newMember.Id,
            BoardAction.Created,
            ct
        );

        var user = await userRepo.FindByIdAsync(newMember.UserId, ct);

        return Result<ProjectMemberDto>.Success(
            new ProjectMemberDto(
                newMember.Id,
                newMember.UserId,
                user?.Username ?? string.Empty,
                newMember.Role,
                newMember.JoinedAt
            )
        );
    }

    public async Task<Result<ProjectMemberDto>> UpdateMemberAsync(
        UpdateProjectMemberCommand cmd,
        CancellationToken ct = default
    )
    {
        var project = await projectRepo.GetByIdAsync(cmd.ProjectId, ct);
        if (project == null)
            return Result<ProjectMemberDto>.Failure(
                new Error(DomainErrorCodes.Projects.NotFound, "Project not found.")
            );

        if (
            !await MembershipGuard.HasAccessAsync(
                userRepo,
                memberRepo,
                cmd.ProjectId,
                cmd.ChangedByUserId,
                ct
            )
        )
            return Result<ProjectMemberDto>.Failure(
                new Error(DomainErrorCodes.Projects.MembershipDenied, "Access denied.")
            );

        var actorMembership = await memberRepo.GetByProjectAndUserAsync(
            cmd.ProjectId,
            cmd.ChangedByUserId,
            ct
        );
        if (actorMembership != null && actorMembership.Role != MemberRole.Owner)
            return Result<ProjectMemberDto>.Failure(
                new Error(DomainErrorCodes.Projects.OwnerRequired, "Owner role required.")
            );

        var member = await memberRepo.GetByIdAsync(cmd.MemberId, ct);
        if (member == null || member.ProjectId != cmd.ProjectId)
            return Result<ProjectMemberDto>.Failure(
                new Error(DomainErrorCodes.Membership.NotFound, "Member not found.")
            );

        var oldSnapshot = BuildSnapshot(member);
        member.ChangeRole(cmd.NewRole);
        await memberRepo.UpdateMemberAsync(member, ct);

        await _auditLogWriter.WriteAsync(
            new AuditLogRequest(
                cmd.ChangedByUserId,
                AuditLogScope.Project,
                "ProjectMember",
                member.Id,
                "RoleChanged",
                cmd.ProjectId,
                AuditSnapshot.Serialize(oldSnapshot),
                AuditSnapshot.Serialize(BuildSnapshot(member))
            ),
            ct
        );

        return Result<ProjectMemberDto>.Success(
            new ProjectMemberDto(
                member.Id,
                member.UserId,
                member.User?.Username ?? string.Empty,
                member.Role,
                member.JoinedAt
            )
        );
    }

    public async Task<Result> RemoveMemberAsync(
        RemoveProjectMemberCommand cmd,
        CancellationToken ct = default
    )
    {
        var project = await projectRepo.GetByIdAsync(cmd.ProjectId, ct);
        if (project == null)
            return Result.Failure(
                new Error(DomainErrorCodes.Projects.NotFound, "Project not found.")
            );

        if (
            !await MembershipGuard.HasAccessAsync(
                userRepo,
                memberRepo,
                cmd.ProjectId,
                cmd.RemovedByUserId,
                ct
            )
        )
            return Result.Failure(
                new Error(DomainErrorCodes.Projects.MembershipDenied, "Access denied.")
            );

        var actorMembership = await memberRepo.GetByProjectAndUserAsync(
            cmd.ProjectId,
            cmd.RemovedByUserId,
            ct
        );
        if (actorMembership != null && actorMembership.Role != MemberRole.Owner)
            return Result.Failure(
                new Error(DomainErrorCodes.Projects.OwnerRequired, "Owner role required.")
            );

        var member = await memberRepo.GetByIdAsync(cmd.MemberId, ct);
        if (member == null || member.ProjectId != cmd.ProjectId)
            return Result.Failure(
                new Error(DomainErrorCodes.Membership.NotFound, "Member not found.")
            );

        if (member.Role == MemberRole.Owner)
        {
            var allMembers = await memberRepo.ListMembersAsync(cmd.ProjectId, ct);
            var ownerCount = allMembers.Count(m => m.Role == MemberRole.Owner);
            if (ownerCount <= 1)
                return Result.Failure(
                    new Error(
                        DomainErrorCodes.Projects.LastOwnerRemovalDenied,
                        "Cannot remove the last owner."
                    )
                );
        }

        var oldSnapshot = BuildSnapshot(member);

        await memberRepo.RemoveMemberAsync(member.Id, ct);

        await _auditLogWriter.WriteAsync(
            new AuditLogRequest(
                cmd.RemovedByUserId,
                AuditLogScope.Project,
                "ProjectMember",
                member.Id,
                "Deleted",
                cmd.ProjectId,
                AuditSnapshot.Serialize(oldSnapshot),
                null
            ),
            ct
        );

        await PublishAsync(
            cmd.ProjectId,
            BoardEntityType.Project,
            member.Id,
            BoardAction.Deleted,
            ct
        );

        return Result.Success();
    }
}
