using HydraForge.Domain.Enums;

namespace HydraForge.Application.Cards;

public record CreateCardCommand(
    Guid ProjectId,
    Guid ColumnId,
    Guid ActorId,
    string Title,
    string Description,
    CardType Type,
    Guid? ParentCardId,
    DateTime? DueAt,
    IReadOnlyList<Guid>? AssigneeUserIds
);

public record UpdateCardCommand(
    Guid ProjectId,
    Guid CardId,
    Guid ActorId,
    string Title,
    string Description,
    CardType Type,
    Guid? ParentCardId,
    DateTime? DueAt,
    int Version
);

public record MoveCardCommand(
    Guid ProjectId,
    Guid CardId,
    Guid TargetColumnId,
    int TargetPosition,
    Guid ActorId,
    bool ConfirmBlockedMove,
    int Version
);

public record AssignCardCommand(Guid ProjectId, Guid CardId, Guid AssigneeUserId, Guid ActorId);

public record UnassignCardCommand(Guid ProjectId, Guid CardId, Guid AssigneeUserId, Guid ActorId);

public record ArchiveCardCommand(Guid ProjectId, Guid CardId, Guid ActorId, int Version);

public record RestoreCardCommand(Guid ProjectId, Guid CardId, Guid ActorId, int Version);

public record DeleteCardCommand(Guid ProjectId, Guid CardId, Guid ActorId);

public record WatchCardCommand(Guid ProjectId, Guid CardId, Guid ActorId);

public record UnwatchCardCommand(Guid ProjectId, Guid CardId, Guid ActorId);

public record CardDto(
    Guid Id,
    Guid ProjectId,
    Guid ColumnId,
    int CardNumber,
    string Title,
    string Description,
    CardType Type,
    int Position,
    DateTime? DueAt,
    int Version,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime MovedAt,
    DateTime? ArchivedAt,
    Guid? ParentCardId,
    IReadOnlyList<CardAssigneeDto> Assignees,
    IReadOnlyList<CardWatcherDto> Watchers,
    IReadOnlyList<CardRelationshipBadgeDto> RelationshipBadges,
    int RelationshipCount,
    ParentCardSummaryDto? ParentCard,
    int ChildCount
);

// Resolved once, server-side (mirrors RelationshipBadges) so clients never need to
// scan the whole board to find a card's parent or count its children. Type is
// included so the client can render the parent's type icon without a second lookup.
public record ParentCardSummaryDto(Guid Id, int CardNumber, string Title, CardType Type);

// Mirrors the TUI's CardRelationshipIndicatorHelper.RelationBadge / BoardRenderer.FormatBadgeLine —
// same Type + IsSource + other-card-number shape, so both clients derive the same verb
// ("blocks"/"blocked by", "precedes"/"preceded by", "spawned"/"spawned from", "relates").
// Capped to a handful per card (matches TUI's MaxBadgesPerCard); RelationshipCount on the
// parent CardDto is the true total, for a "+N more" overflow indicator.
public record CardRelationshipBadgeDto(
    Guid RelatedCardId,
    int RelatedCardNumber,
    string RelatedCardTitle,
    RelationshipType Type,
    bool IsSource
);

public record CardAssigneeDto(Guid Id, Guid UserId, string Username, DateTime AssignedAt);

public record CardWatcherDto(Guid UserId, string Username, DateTime AddedAt);

public record CardListFilter(
    Guid? ColumnId = null,
    bool IncludeArchived = false,
    Guid? AssigneeUserId = null,
    CardType? Type = null,
    string? Search = null,
    int? ArchivedLimit = 200
);

public record BlockedMoveWarningDto(Guid CardId, IReadOnlyList<BlockerDto> Blockers);

public record BlockerDto(
    Guid CardId,
    int CardNumber,
    string Title,
    RelationshipBlockerType BlockerType
);

public enum RelationshipBlockerType
{
    BlockedBy,
    Precedes,
}

public record CreateCardRequest(
    Guid ColumnId,
    string Title,
    string Description,
    CardType Type,
    Guid? ParentCardId,
    DateTime? DueAt,
    IReadOnlyList<Guid>? AssigneeUserIds
);

public record UpdateCardRequest(
    string Title,
    string Description,
    CardType Type,
    Guid? ParentCardId,
    DateTime? DueAt,
    int Version
);

public record MoveCardRequest(
    Guid TargetColumnId,
    int TargetPosition,
    bool ConfirmBlockedMove,
    int Version
);

public record AssignCardRequest(Guid AssigneeUserId);

public record ArchiveCardRequest(int Version);

public record RestoreCardRequest(int Version);

public record CardResponse(
    Guid Id,
    Guid ProjectId,
    Guid ColumnId,
    int CardNumber,
    string Title,
    string Description,
    CardType Type,
    int Position,
    DateTime? DueAt,
    int Version,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime MovedAt,
    DateTime? ArchivedAt,
    Guid? ParentCardId,
    IReadOnlyList<CardAssigneeResponse> Assignees,
    IReadOnlyList<CardWatcherResponse> Watchers,
    IReadOnlyList<CardRelationshipBadgeResponse> RelationshipBadges,
    int RelationshipCount,
    ParentCardSummaryResponse? ParentCard,
    int ChildCount
);

public record ParentCardSummaryResponse(Guid Id, int CardNumber, string Title, CardType Type);

public record CardRelationshipBadgeResponse(
    Guid RelatedCardId,
    int RelatedCardNumber,
    string RelatedCardTitle,
    RelationshipType Type,
    bool IsSource
);

public record CardAssigneeResponse(Guid Id, Guid UserId, string Username, DateTime AssignedAt);

public record CardWatcherResponse(Guid UserId, string Username, DateTime AddedAt);

public record CardListResponse(IReadOnlyList<CardResponse> Cards);

public record BlockedMoveWarningResponse(Guid CardId, IReadOnlyList<BlockerResponse> Blockers);

public record BlockerResponse(Guid CardId, int CardNumber, string Title, string BlockerType);
