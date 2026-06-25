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
    DateTime? DueAt
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

public record AssignCardCommand(
    Guid ProjectId,
    Guid CardId,
    Guid AssigneeUserId,
    Guid ActorId
);

public record UnassignCardCommand(
    Guid ProjectId,
    Guid CardId,
    Guid AssigneeUserId,
    Guid ActorId
);

public record ArchiveCardCommand(
    Guid ProjectId,
    Guid CardId,
    Guid ActorId,
    int Version
);

public record DeleteCardCommand(
    Guid ProjectId,
    Guid CardId,
    Guid ActorId
);

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
    IReadOnlyList<CardWatcherDto> Watchers
);

public record CardAssigneeDto(
    Guid Id,
    Guid UserId,
    string Username,
    DateTime AssignedAt
);

public record CardWatcherDto(
    Guid UserId,
    string Username,
    DateTime AddedAt
);

public record CardListFilter(
    Guid? ColumnId = null,
    bool IncludeArchived = false,
    Guid? AssigneeUserId = null,
    CardType? Type = null,
    string? Search = null
);

public record BlockedMoveWarningDto(
    Guid CardId,
    IReadOnlyList<BlockerDto> Blockers
);

public record BlockerDto(
    Guid CardId,
    int CardNumber,
    string Title,
    RelationshipBlockerType BlockerType
);

public enum RelationshipBlockerType
{
    BlockedBy,
    Precedes
}

public record CreateCardRequest(
    Guid ColumnId,
    string Title,
    string Description,
    CardType Type,
    Guid? ParentCardId,
    DateTime? DueAt
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

public record AssignCardRequest(
    Guid AssigneeUserId
);

public record ArchiveCardRequest(
    int Version
);

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
    IReadOnlyList<CardWatcherResponse> Watchers
);

public record CardAssigneeResponse(
    Guid Id,
    Guid UserId,
    string Username,
    DateTime AssignedAt
);

public record CardWatcherResponse(
    Guid UserId,
    string Username,
    DateTime AddedAt
);

public record CardListResponse(
    IReadOnlyList<CardResponse> Cards
);

public record BlockedMoveWarningResponse(
    Guid CardId,
    IReadOnlyList<BlockerResponse> Blockers
);

public record BlockerResponse(
    Guid CardId,
    int CardNumber,
    string Title,
    string BlockerType
);
