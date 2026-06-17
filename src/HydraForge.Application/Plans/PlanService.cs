using HydraForge.Application.Audit;
using HydraForge.Application.Cards;
using HydraForge.Application.Projects;
using HydraForge.Application.Shared;
using HydraForge.Domain.Common;
using HydraForge.Domain.Entities.ProjectSpace;
using HydraForge.Domain.Enums;

namespace HydraForge.Application.Plans;

public class PlanService(
    IPlanRepository planRepo,
    ICardRepository cardRepo,
    IProjectMemberRepository memberRepo,
    IAuditLogWriter auditLogWriter
)
{
    private readonly IPlanRepository _planRepo = planRepo;
    private readonly ICardRepository _cardRepo = cardRepo;
    private readonly IProjectMemberRepository _memberRepo = memberRepo;
    private readonly IAuditLogWriter _auditLogWriter = auditLogWriter;

    public async Task<Result<PlanDto>> CreateAsync(CreatePlanCommand cmd, CancellationToken ct = default)
    {
        var membership = await _memberRepo.GetByProjectAndUserAsync(cmd.ProjectId, cmd.ActorId, ct);
        if (membership == null)
            return Result<PlanDto>.Failure(
                new Error(DomainErrorCodes.Projects.MembershipDenied, "Access denied.")
            );

        if (cmd.Content.Length > DocumentMarkdownLimits.MaxMarkdownPayloadBytes)
            return Result<PlanDto>.Failure(
                new Error(DomainErrorCodes.Plans.MarkdownPayloadTooLarge, "Markdown payload exceeds limit.")
            );

        var plan = new Plan
        {
            Id = Guid.NewGuid(),
            ProjectId = cmd.ProjectId,
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
            Content = cmd.Content,
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = cmd.ActorId,
        };

        await _planRepo.AddAsync(plan, ct);
        await _planRepo.AddVersionAsync(version, ct);
        await _planRepo.SaveChangesAsync(ct);

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

        return Result<PlanDto>.Success(MapToDto(plan, null));
    }

    public async Task<Result<PlanDto>> GetByIdAsync(Guid projectId, Guid planId, Guid actorId, CancellationToken ct = default)
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

        var linkedCardId = await _planRepo.GetLinkedCardIdAsync(planId, ct);
        return Result<PlanDto>.Success(MapToDto(plan, linkedCardId));
    }

    public async Task<Result<IReadOnlyList<PlanDto>>> ListAsync(Guid projectId, PlanListFilter filter, Guid actorId, CancellationToken ct = default)
    {
        var membership = await _memberRepo.GetByProjectAndUserAsync(projectId, actorId, ct);
        if (membership == null)
            return Result<IReadOnlyList<PlanDto>>.Failure(
                new Error(DomainErrorCodes.Projects.MembershipDenied, "Access denied.")
            );

        var plans = await _planRepo.ListByProjectAsync(projectId, filter, ct);
        var dtos = new List<PlanDto>();
        foreach (var p in plans)
        {
            var linkedCardId = await _planRepo.GetLinkedCardIdAsync(p.Id, ct);
            dtos.Add(MapToDto(p, linkedCardId));
        }
        return Result<IReadOnlyList<PlanDto>>.Success(dtos);
    }

    public async Task<Result<PlanDto>> UpdateAsync(UpdatePlanCommand cmd, CancellationToken ct = default)
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
                new Error(DomainErrorCodes.Plans.MarkdownPayloadTooLarge, "Markdown payload exceeds limit.")
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
            Content = cmd.Content,
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = cmd.ActorId,
        };

        await _planRepo.UpdateAsync(plan, ct);
        await _planRepo.AddVersionAsync(version, ct);
        await _planRepo.SaveChangesAsync(ct);

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

        var linkedCardId = await _planRepo.GetLinkedCardIdAsync(plan.Id, ct);
        return Result<PlanDto>.Success(MapToDto(plan, linkedCardId));
    }

    public async Task<Result<IReadOnlyList<PlanVersionDto>>> ListVersionsAsync(Guid projectId, Guid planId, Guid actorId, CancellationToken ct = default)
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
        return Result<IReadOnlyList<PlanVersionDto>>.Success(
            versions.Select(v => new PlanVersionDto(v.Id, v.PlanId, v.Version, v.Content, v.CreatedAt, v.CreatedByUserId)).ToList()
        );
    }

    public async Task<Result<PlanDto>> RestoreVersionAsync(RestorePlanVersionCommand cmd, CancellationToken ct = default)
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

        plan.Content = oldVersion.Content;
        plan.Version += 1;
        plan.UpdatedAt = DateTime.UtcNow;

        var newVersion = new PlanVersion
        {
            Id = Guid.NewGuid(),
            PlanId = plan.Id,
            Version = plan.Version,
            Content = oldVersion.Content,
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = cmd.ActorId,
        };

        await _planRepo.UpdateAsync(plan, ct);
        await _planRepo.AddVersionAsync(newVersion, ct);
        await _planRepo.SaveChangesAsync(ct);

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

        var linkedCardId = await _planRepo.GetLinkedCardIdAsync(plan.Id, ct);
        return Result<PlanDto>.Success(MapToDto(plan, linkedCardId));
    }

    public async Task<Result> LinkToCardAsync(LinkPlanToCardCommand cmd, CancellationToken ct = default)
    {
        var membership = await _memberRepo.GetByProjectAndUserAsync(cmd.ProjectId, cmd.ActorId, ct);
        if (membership == null)
            return Result.Failure(
                new Error(DomainErrorCodes.Projects.MembershipDenied, "Access denied.")
            );

        var plan = await _planRepo.GetByIdAsync(cmd.PlanId, ct);
        if (plan == null || plan.ProjectId != cmd.ProjectId)
            return Result.Failure(new Error(DomainErrorCodes.Plans.NotFound, "Plan not found."));

        var card = await _cardRepo.GetByIdAsync(cmd.CardId, ct);
        if (card == null)
            return Result.Failure(new Error(DomainErrorCodes.Cards.NotFound, "Card not found."));

        if (card.ProjectId != cmd.ProjectId)
            return Result.Failure(
                new Error(DomainErrorCodes.Plans.CardDocumentProjectMismatch, "Card is in a different project.")
            );

        card.PlanId = cmd.PlanId;
        await _cardRepo.UpdateAsync(card, ct);
        await _planRepo.SaveChangesAsync(ct);

        await _auditLogWriter.WriteAsync(
            new AuditLogRequest(
                cmd.ActorId,
                AuditLogScope.Project,
                "Plan",
                plan.Id,
                "Linked",
                cmd.ProjectId,
                null,
                null
            ),
            ct
        );

        return Result.Success();
    }

    public async Task<Result> UnlinkFromCardAsync(UnlinkPlanFromCardCommand cmd, CancellationToken ct = default)
    {
        var membership = await _memberRepo.GetByProjectAndUserAsync(cmd.ProjectId, cmd.ActorId, ct);
        if (membership == null)
            return Result.Failure(
                new Error(DomainErrorCodes.Projects.MembershipDenied, "Access denied.")
            );

        var card = await _cardRepo.GetByIdAsync(cmd.CardId, ct);
        if (card == null)
            return Result.Failure(new Error(DomainErrorCodes.Cards.NotFound, "Card not found."));

        if (card.ProjectId != cmd.ProjectId)
            return Result.Failure(
                new Error(DomainErrorCodes.Plans.CardDocumentProjectMismatch, "Card is in a different project.")
            );

        if (card.PlanId == null)
            return Result.Success();

        card.PlanId = null;
        await _cardRepo.UpdateAsync(card, ct);
        await _planRepo.SaveChangesAsync(ct);

        return Result.Success();
    }

    private static PlanDto MapToDto(Plan plan, Guid? linkedCardId) =>
        new(
            plan.Id,
            plan.ProjectId,
            plan.Title,
            plan.Description,
            plan.Content,
            plan.Version,
            plan.CreatedByUserId,
            plan.CreatedAt,
            plan.UpdatedAt,
            linkedCardId
        );
}
