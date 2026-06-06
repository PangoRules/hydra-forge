using HydraForge.Application.Auth;
using HydraForge.Domain.Common;
using HydraForge.Domain.Entities.ProjectSpace;
using HydraForge.Domain.Enums;

namespace HydraForge.Application.Projects;

public class ProjectMemberService(
    IProjectRepository projectRepo,
    IProjectMemberRepository memberRepo,
    IUserRepository userRepo
)
{
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

        var actorMembership = await memberRepo.GetByProjectAndUserAsync(
            cmd.ProjectId,
            cmd.AddedByUserId,
            ct
        );
        if (actorMembership == null)
            return Result<ProjectMemberDto>.Failure(
                new Error(
                    DomainErrorCodes.Projects.MembershipDenied,
                    "Access denied."
                )
            );

        if (actorMembership.Role != MemberRole.Owner)
            return Result<ProjectMemberDto>.Failure(
                new Error(
                    DomainErrorCodes.Projects.OwnerRequired,
                    "Owner role required."
                )
            );

        var existingMember = await memberRepo.GetByProjectAndUserAsync(
            cmd.ProjectId,
            cmd.UserId,
            ct
        );
        if (existingMember != null)
            return Result<ProjectMemberDto>.Failure(
                new Error(
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

        await memberRepo.AddMemberAsync(newMember, ct);

        var user = await userRepo.FindByIdAsync(newMember.UserId);

        return Result<ProjectMemberDto>.Success(
            new ProjectMemberDto(newMember.Id, newMember.UserId, user?.Username ?? string.Empty, newMember.Role, newMember.JoinedAt)
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

        var actorMembership = await memberRepo.GetByProjectAndUserAsync(
            cmd.ProjectId,
            cmd.ChangedByUserId,
            ct
        );
        if (actorMembership == null || actorMembership.Role != MemberRole.Owner)
            return Result<ProjectMemberDto>.Failure(
                new Error(
                    DomainErrorCodes.Projects.OwnerRequired,
                    "Owner role required."
                )
            );

        var member = await memberRepo.GetByProjectAndUserAsync(cmd.ProjectId, cmd.UserId, ct);
        if (member == null)
            return Result<ProjectMemberDto>.Failure(
                new Error(DomainErrorCodes.Membership.NotFound, "Member not found.")
            );

        member.Role = cmd.NewRole;
        await memberRepo.UpdateMemberAsync(member, ct);

        var user = await userRepo.FindByIdAsync(member.UserId);

        return Result<ProjectMemberDto>.Success(
            new ProjectMemberDto(member.Id, member.UserId, user?.Username ?? string.Empty, member.Role, member.JoinedAt)
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

        var actorMembership = await memberRepo.GetByProjectAndUserAsync(
            cmd.ProjectId,
            cmd.RemovedByUserId,
            ct
        );
        if (actorMembership == null || actorMembership.Role != MemberRole.Owner)
            return Result.Failure(
                new Error(
                    DomainErrorCodes.Projects.OwnerRequired,
                    "Owner role required."
                )
            );

        var member = await memberRepo.GetByProjectAndUserAsync(cmd.ProjectId, cmd.UserId, ct);
        if (member == null)
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

        await memberRepo.RemoveMemberAsync(member.Id, ct);

        return Result.Success();
    }
}
