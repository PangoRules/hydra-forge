namespace HydraForge.Application.Plans;

using HydraForge.Domain.Enums;

public record CreatePlanCommand(
    Guid ProjectId,
    Guid CardId,
    Guid? SpecId,
    Guid ActorId,
    string Title,
    string? Description,
    string Content,
    int Position = 0
);

public record ActivatePlanCommand(Guid ProjectId, Guid PlanId, Guid ActorId);

public record CompletePlanCommand(Guid ProjectId, Guid PlanId, Guid ActorId);

public record ReactivatePlanCommand(Guid ProjectId, Guid PlanId, Guid ActorId);

public record UpdatePlanCommand(
    Guid ProjectId,
    Guid PlanId,
    Guid ActorId,
    string Title,
    string? Description,
    string Content
);

public record RestorePlanVersionCommand(
    Guid ProjectId,
    Guid PlanId,
    int Version,
    Guid ActorId
);

public record PlanDto(
    Guid Id,
    Guid ProjectId,
    Guid CardId,
    Guid? SpecId,
    string Title,
    string? Description,
    string Content,
    PlanStatus Status,
    int Position,
    int Version,
    Guid CreatedByUserId,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record PlanVersionDto(
    Guid Id,
    Guid PlanId,
    int Version,
    string Title,
    string? Description,
    string Content,
    DateTime CreatedAt,
    Guid CreatedByUserId
);

public record PlanListFilter(bool IncludeArchived = false);

public record CreatePlanRequest(
    string Title,
    string? Description,
    string Content,
    Guid? SpecId = null,
    int Position = 0
);

public record UpdatePlanRequest(
    string Title,
    string? Description,
    string Content
);

public record RestorePlanVersionRequest(
    int Version
);

public record PlanResponse(
    Guid Id,
    Guid ProjectId,
    Guid CardId,
    string Title,
    string? Description,
    string Content,
    int Version,
    Guid CreatedByUserId,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    Guid? SpecId,
    PlanStatus Status,
    int Position
);

public record PlanVersionResponse(
    Guid Id,
    Guid PlanId,
    int Version,
    string Title,
    string? Description,
    string Content,
    DateTime CreatedAt,
    Guid CreatedByUserId
);

public record PlanListResponse(IReadOnlyList<PlanResponse> Plans);

public record PlanVersionListResponse(IReadOnlyList<PlanVersionResponse> Versions);