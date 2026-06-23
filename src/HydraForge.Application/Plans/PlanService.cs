using HydraForge.Application.Audit;
using HydraForge.Application.ProjectSnapshots;
using HydraForge.Application.Projects;
using HydraForge.Application.Shared;
using HydraForge.Domain.Common;
using HydraForge.Domain.Entities.ProjectSpace;
using HydraForge.Domain.Enums;

namespace HydraForge.Application.Plans;

public class PlanService(
    IPlanRepository planRepo,
    IProjectMemberRepository memberRepo,
    IAuditLogWriter auditLogWriter,
    IProjectSnapshotRefresher snapshotRefresher
)
{
    private readonly IPlanRepository _planRepo = planRepo;
    private readonly IProjectMemberRepository _memberRepo = memberRepo;
    private readonly IAuditLogWriter _auditLogWriter = auditLogWriter;
    private readonly IProjectSnapshotRefresher _snapshotRefresher = snapshotRefresher;

    public async Task<Result<PlanDto>> CreateAsync(
        CreatePlanCommand cmd,
        CancellationToken ct = default
    )
    {
        var membership = await _memberRepo.GetByProjectAndUserAsync(cmd.ProjectId, cmd.ActorId, ct);
        if (membership == null)
            return Result<PlanDto>.Failure(
                new Error(DomainErrorCodes.Projects.MembershipDenied, "Access denied.")
            );

        if (cmd.Content.Length > DocumentMarkdownLimits.MaxMarkdownPayloadBytes)
            return Result<PlanDto>.Failure(
                new Error(
                    DomainErrorCodes.Plans.MarkdownPayloadTooLarge,
                    "Markdown payload exceeds limit."
                )
            );

        var plan = new Plan
        {
            Id = Guid.NewGuid(),
            ProjectId = cmd.ProjectId,
            CardId = cmd.CardId,
            SpecId = cmd.SpecId,
            Title = cmd.Title,
            Description = cmd.Description,
            Content = cmd.Content,
            Version = 1,
            CreatedByUserId = cmd.ActorId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        var version = new PlanVersion
        {
            Id = Guid.NewGuid(),
            PlanId = plan.Id,
            Version = 1,
            Title = cmd.Title,
            Description = cmd.Description,
            Content = cmd.Content,
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = cmd.ActorId,
        };

        await _planRepo.AddAsync(plan, ct);
        await _planRepo.AddVersionAsync(version, ct);
        await _planRepo.SaveChangesAsync(ct);
        await _snapshotRefresher.RefreshAsync(cmd.ProjectId, ct);

        await _auditLogWriter.WriteAsync(
            new AuditLogRequest(
                cmd.ActorId,
                AuditLogScope.Project,
                "Plan",
                plan.Id,
                "Created",
                cmd.ProjectId,
                null,
                null
            ),
            ct
        );

        return Result<PlanDto>.Success(MapToDto(plan));
    }

    public async Task<Result<PlanDto>> GetByIdAsync(
        Guid projectId,
        Guid planId,
        Guid actorId,
        CancellationToken ct = default
    )
    {
        var membership = await _memberRepo.GetByProjectAndUserAsync(projectId, actorId, ct);
        if (membership == null)
            return Result<PlanDto>.Failure(
                new Error(DomainErrorCodes.Projects.MembershipDenied, "Access denied.")
            );

        var plan = await _planRepo.GetByIdAsync(planId, ct);
        if (plan == null || plan.ProjectId != projectId)
            return Result<PlanDto>.Failure(
                new Error(DomainErrorCodes.Plans.NotFound, "Plan not found.")
            );

        return Result<PlanDto>.Success(MapToDto(plan));
    }

    public async Task<Result<IReadOnlyList<PlanDto>>> ListAsync(
        Guid projectId,
        PlanListFilter filter,
        Guid actorId,
        CancellationToken ct = default
    )
    {
        var membership = await _memberRepo.GetByProjectAndUserAsync(projectId, actorId, ct);
        if (membership == null)
            return Result<IReadOnlyList<PlanDto>>.Failure(
                new Error(DomainErrorCodes.Projects.MembershipDenied, "Access denied.")
            );

        var plans = await _planRepo.ListByProjectAsync(projectId, filter, ct);
        var dtos = plans.Select(MapToDto).ToList();
        return Result<IReadOnlyList<PlanDto>>.Success(dtos);
    }

    public async Task<Result<IReadOnlyList<PlanDto>>> ListByCardAsync(
        Guid projectId,
        Guid cardId,
        PlanListFilter filter,
        Guid actorId,
        CancellationToken ct = default
    )
    {
        var membership = await _memberRepo.GetByProjectAndUserAsync(projectId, actorId, ct);
        if (membership == null)
            return Result<IReadOnlyList<PlanDto>>.Failure(
                new Error(DomainErrorCodes.Projects.MembershipDenied, "Access denied.")
            );

        var plans = await _planRepo.ListByCardAsync(cardId, filter, ct);
        var dtos = plans.Select(MapToDto).ToList();
        return Result<IReadOnlyList<PlanDto>>.Success(dtos);
    }

    public async Task<Result<PlanDto>> UpdateAsync(
        UpdatePlanCommand cmd,
        CancellationToken ct = default
    )
    {
        var membership = await _memberRepo.GetByProjectAndUserAsync(cmd.ProjectId, cmd.ActorId, ct);
        if (membership == null)
            return Result<PlanDto>.Failure(
                new Error(DomainErrorCodes.Projects.MembershipDenied, "Access denied.")
            );

        var plan = await _planRepo.GetByIdAsync(cmd.PlanId, ct);
        if (plan == null || plan.ProjectId != cmd.ProjectId)
            return Result<PlanDto>.Failure(
                new Error(DomainErrorCodes.Plans.NotFound, "Plan not found.")
            );

        if (cmd.Content.Length > DocumentMarkdownLimits.MaxMarkdownPayloadBytes)
            return Result<PlanDto>.Failure(
                new Error(
                    DomainErrorCodes.Plans.MarkdownPayloadTooLarge,
                    "Markdown payload exceeds limit."
                )
            );

        plan.Title = cmd.Title;
        plan.Description = cmd.Description;
        plan.Content = cmd.Content;
        plan.Version += 1;
        plan.UpdatedAt = DateTime.UtcNow;

        var version = new PlanVersion
        {
            Id = Guid.NewGuid(),
            PlanId = plan.Id,
            Version = plan.Version,
            Title = cmd.Title,
            Description = cmd.Description,
            Content = cmd.Content,
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = cmd.ActorId,
        };

        await _planRepo.UpdateAsync(plan, ct);
        await _planRepo.AddVersionAsync(version, ct);
        await _planRepo.SaveChangesAsync(ct);
        await _snapshotRefresher.RefreshAsync(cmd.ProjectId, ct);

        await _auditLogWriter.WriteAsync(
            new AuditLogRequest(
                cmd.ActorId,
                AuditLogScope.Project,
                "Plan",
                plan.Id,
                "Updated",
                cmd.ProjectId,
                null,
                null
            ),
            ct
        );

        return Result<PlanDto>.Success(MapToDto(plan));
    }

    public async Task<Result<IReadOnlyList<PlanVersionDto>>> ListVersionsAsync(
        Guid projectId,
        Guid planId,
        Guid actorId,
        CancellationToken ct = default
    )
    {
        var membership = await _memberRepo.GetByProjectAndUserAsync(projectId, actorId, ct);
        if (membership == null)
            return Result<IReadOnlyList<PlanVersionDto>>.Failure(
                new Error(DomainErrorCodes.Projects.MembershipDenied, "Access denied.")
            );

        var plan = await _planRepo.GetByIdAsync(planId, ct);
        if (plan == null || plan.ProjectId != projectId)
            return Result<IReadOnlyList<PlanVersionDto>>.Failure(
                new Error(DomainErrorCodes.Plans.NotFound, "Plan not found.")
            );

        var versions = await _planRepo.ListVersionsAsync(planId, ct);
        return Result<IReadOnlyList<PlanVersionDto>>.Success([
            .. versions.Select(v => new PlanVersionDto(
                v.Id,
                v.PlanId,
                v.Version,
                v.Title,
                v.Description,
                v.Content,
                v.CreatedAt,
                v.CreatedByUserId
            )),
        ]);
    }

    public async Task<Result<PlanDto>> RestoreVersionAsync(
        RestorePlanVersionCommand cmd,
        CancellationToken ct = default
    )
    {
        var membership = await _memberRepo.GetByProjectAndUserAsync(cmd.ProjectId, cmd.ActorId, ct);
        if (membership == null)
            return Result<PlanDto>.Failure(
                new Error(DomainErrorCodes.Projects.MembershipDenied, "Access denied.")
            );

        var plan = await _planRepo.GetByIdAsync(cmd.PlanId, ct);
        if (plan == null || plan.ProjectId != cmd.ProjectId)
            return Result<PlanDto>.Failure(
                new Error(DomainErrorCodes.Plans.NotFound, "Plan not found.")
            );

        var oldVersion = await _planRepo.GetVersionAsync(cmd.PlanId, cmd.Version, ct);
        if (oldVersion == null)
            return Result<PlanDto>.Failure(
                new Error(DomainErrorCodes.Plans.DocumentVersionNotFound, "Plan version not found.")
            );

        plan.Title = oldVersion.Title;
        plan.Description = oldVersion.Description;
        plan.Content = oldVersion.Content;
        plan.Version += 1;
        plan.UpdatedAt = DateTime.UtcNow;

        var newVersion = new PlanVersion
        {
            Id = Guid.NewGuid(),
            PlanId = plan.Id,
            Version = plan.Version,
            Title = oldVersion.Title,
            Description = oldVersion.Description,
            Content = oldVersion.Content,
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = cmd.ActorId,
        };

        await _planRepo.UpdateAsync(plan, ct);
        await _planRepo.AddVersionAsync(newVersion, ct);
        await _planRepo.SaveChangesAsync(ct);
        await _snapshotRefresher.RefreshAsync(cmd.ProjectId, ct);

        await _auditLogWriter.WriteAsync(
            new AuditLogRequest(
                cmd.ActorId,
                AuditLogScope.Project,
                "Plan",
                plan.Id,
                "Restored",
                cmd.ProjectId,
                null,
                null
            ),
            ct
        );

        return Result<PlanDto>.Success(MapToDto(plan));
    }

    private static PlanDto MapToDto(Plan plan) =>
        new(
            plan.Id,
            plan.ProjectId,
            plan.CardId,
            plan.Title,
            plan.Description,
            plan.Content,
            plan.Version,
            plan.CreatedByUserId,
            plan.CreatedAt,
            plan.UpdatedAt
        );
}
