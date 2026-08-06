using HydraForge.Application.Audit;
using HydraForge.Application.Auth;
using HydraForge.Application.Cards;
using HydraForge.Application.Projects;
using HydraForge.Application.ProjectSnapshots;
using HydraForge.Application.Realtime;
using HydraForge.Application.Shared;
using HydraForge.Domain.Common;
using HydraForge.Domain.Entities.ProjectSpace;
using HydraForge.Domain.Enums;

namespace HydraForge.Application.Plans;

public class PlanService(
    IPlanRepository planRepo,
    ICardRepository cardRepo,
    IProjectMemberRepository memberRepo,
    IUserRepository userRepo,
    IAuditLogWriter auditLogWriter,
    IProjectSnapshotRefresher snapshotRefresher,
    IProjectBoardEventPublisher publisher
)
{
    private readonly IPlanRepository _planRepo = planRepo;
    private readonly ICardRepository _cardRepo = cardRepo;
    private readonly IProjectMemberRepository _memberRepo = memberRepo;
    private readonly IUserRepository _userRepo = userRepo;
    private readonly IAuditLogWriter _auditLogWriter = auditLogWriter;
    private readonly IProjectSnapshotRefresher _snapshotRefresher = snapshotRefresher;
    private readonly IProjectBoardEventPublisher _publisher = publisher;

    private sealed record PlanAuditSnapshot(
        string Title,
        string? Description,
        string Content,
        PlanStatus Status
    );

    private static PlanAuditSnapshot BuildSnapshot(Plan plan) =>
        new(plan.Title, plan.Description, plan.Content, plan.Status);

    public async Task<Result<PlanDto>> CreateAsync(
        CreatePlanCommand cmd,
        CancellationToken ct = default
    )
    {
        if (
            !await MembershipGuard.HasAccessAsync(
                _userRepo,
                _memberRepo,
                cmd.ProjectId,
                cmd.ActorId,
                ct
            )
        )
            return Result<PlanDto>.Failure(
                new Error(DomainErrorCodes.Projects.MembershipDenied, "Access denied.")
            );

        var card = await _cardRepo.GetByIdAsync(cmd.CardId, ct);
        if (card == null)
            return Result<PlanDto>.Failure(
                new Error(DomainErrorCodes.Cards.NotFound, "Card not found.")
            );
        if (card.ProjectId != cmd.ProjectId)
            return Result<PlanDto>.Failure(
                new Error(
                    DomainErrorCodes.Plans.CardDocumentProjectMismatch,
                    "Card is in a different project."
                )
            );

        var cardTypeError = Card.ValidateAllowsPlan(card.Type);
        if (cardTypeError != null)
            return Result<PlanDto>.Failure(cardTypeError);

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
            Title = cmd.Title,
            Description = cmd.Description,
            Content = cmd.Content,
            Status = PlanStatus.Pending,
            Position = cmd.Position,
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
                AuditSnapshot.Serialize(BuildSnapshot(plan))
            ),
            ct
        );

        await PublishAsync(cmd.ProjectId, plan.Id, plan.CardId, BoardAction.Created, ct);

        return Result<PlanDto>.Success(MapToDto(plan));
    }

    public async Task<Result<PlanDto>> GetByIdAsync(
        Guid projectId,
        Guid planId,
        Guid actorId,
        CancellationToken ct = default
    )
    {
        if (!await MembershipGuard.HasAccessAsync(_userRepo, _memberRepo, projectId, actorId, ct))
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
        if (!await MembershipGuard.HasAccessAsync(_userRepo, _memberRepo, projectId, actorId, ct))
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
        if (!await MembershipGuard.HasAccessAsync(_userRepo, _memberRepo, projectId, actorId, ct))
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
        if (
            !await MembershipGuard.HasAccessAsync(
                _userRepo,
                _memberRepo,
                cmd.ProjectId,
                cmd.ActorId,
                ct
            )
        )
            return Result<PlanDto>.Failure(
                new Error(DomainErrorCodes.Projects.MembershipDenied, "Access denied.")
            );

        var plan = await _planRepo.GetByIdAsync(cmd.PlanId, ct);
        if (plan == null || plan.ProjectId != cmd.ProjectId)
            return Result<PlanDto>.Failure(
                new Error(DomainErrorCodes.Plans.NotFound, "Plan not found.")
            );

        if (plan.IsDone)
            return Result<PlanDto>.Failure(
                new Error(
                    DomainErrorCodes.Plans.EditForbiddenWhenDone,
                    "Done plans are read-only. Reactivate before editing."
                )
            );

        if (cmd.Content.Length > DocumentMarkdownLimits.MaxMarkdownPayloadBytes)
            return Result<PlanDto>.Failure(
                new Error(
                    DomainErrorCodes.Plans.MarkdownPayloadTooLarge,
                    "Markdown payload exceeds limit."
                )
            );

        var oldSnapshot = BuildSnapshot(plan);
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
                AuditSnapshot.Serialize(oldSnapshot),
                AuditSnapshot.Serialize(BuildSnapshot(plan))
            ),
            ct
        );

        await PublishAsync(cmd.ProjectId, plan.Id, plan.CardId, BoardAction.Updated, ct);

        return Result<PlanDto>.Success(MapToDto(plan));
    }

    public async Task<Result<IReadOnlyList<PlanVersionDto>>> ListVersionsAsync(
        Guid projectId,
        Guid planId,
        Guid actorId,
        CancellationToken ct = default
    )
    {
        if (!await MembershipGuard.HasAccessAsync(_userRepo, _memberRepo, projectId, actorId, ct))
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
        if (
            !await MembershipGuard.HasAccessAsync(
                _userRepo,
                _memberRepo,
                cmd.ProjectId,
                cmd.ActorId,
                ct
            )
        )
            return Result<PlanDto>.Failure(
                new Error(DomainErrorCodes.Projects.MembershipDenied, "Access denied.")
            );

        var plan = await _planRepo.GetByIdAsync(cmd.PlanId, ct);
        if (plan == null || plan.ProjectId != cmd.ProjectId)
            return Result<PlanDto>.Failure(
                new Error(DomainErrorCodes.Plans.NotFound, "Plan not found.")
            );

        if (plan.IsDone)
            return Result<PlanDto>.Failure(
                new Error(
                    DomainErrorCodes.Plans.EditForbiddenWhenDone,
                    "Done plans are read-only. Reactivate before editing."
                )
            );

        var oldVersion = await _planRepo.GetVersionAsync(cmd.PlanId, cmd.Version, ct);
        if (oldVersion == null)
            return Result<PlanDto>.Failure(
                new Error(DomainErrorCodes.Plans.DocumentVersionNotFound, "Plan version not found.")
            );

        var oldSnapshot = BuildSnapshot(plan);
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
                AuditSnapshot.Serialize(oldSnapshot),
                AuditSnapshot.Serialize(BuildSnapshot(plan))
            ),
            ct
        );

        await PublishAsync(cmd.ProjectId, plan.Id, plan.CardId, BoardAction.Restored, ct);

        return Result<PlanDto>.Success(MapToDto(plan));
    }

    public async Task<Result<PlanDto>> SetStatusAsync(
        SetPlanStatusCommand cmd,
        CancellationToken ct = default
    )
    {
        if (
            !await MembershipGuard.HasAccessAsync(
                _userRepo,
                _memberRepo,
                cmd.ProjectId,
                cmd.ActorId,
                ct
            )
        )
            return Result<PlanDto>.Failure(
                new Error(DomainErrorCodes.Projects.MembershipDenied, "Access denied.")
            );

        var plan = await _planRepo.GetByIdAsync(cmd.PlanId, ct);
        if (plan == null || plan.ProjectId != cmd.ProjectId)
            return Result<PlanDto>.Failure(
                new Error(DomainErrorCodes.Plans.NotFound, "Plan not found.")
            );

        if (plan.Status == cmd.Status)
            return Result<PlanDto>.Success(MapToDto(plan));

        var oldSnapshot = BuildSnapshot(plan);
        plan.SetStatus(cmd.Status);
        plan.UpdatedAt = DateTime.UtcNow;

        await _planRepo.UpdateAsync(plan, ct);
        await _planRepo.SaveChangesAsync(ct);

        await _auditLogWriter.WriteAsync(
            new AuditLogRequest(
                cmd.ActorId,
                AuditLogScope.Project,
                "Plan",
                plan.Id,
                "StatusChanged",
                cmd.ProjectId,
                AuditSnapshot.Serialize(oldSnapshot),
                AuditSnapshot.Serialize(BuildSnapshot(plan))
            ),
            ct
        );

        await PublishAsync(cmd.ProjectId, plan.Id, plan.CardId, BoardAction.Updated, ct);

        return Result<PlanDto>.Success(MapToDto(plan));
    }

    private async Task PublishAsync(
        Guid projectId,
        Guid planId,
        Guid cardId,
        BoardAction action,
        CancellationToken ct
    )
    {
        var envelope = new ProjectBoardEventEnvelope(
            Guid.NewGuid(),
            projectId,
            BoardEntityType.Plan,
            planId,
            action,
            1,
            DateTime.UtcNow,
            null!,
            cardId
        );
        await _publisher.PublishAsync(envelope, ct);
    }

    private static PlanDto MapToDto(Plan plan) =>
        new(
            plan.Id,
            plan.ProjectId,
            plan.CardId,
            plan.SpecId,
            plan.Title,
            plan.Description,
            plan.Content,
            plan.Status,
            plan.Position,
            plan.Version,
            plan.CreatedByUserId,
            plan.CreatedAt,
            plan.UpdatedAt
        );
}
