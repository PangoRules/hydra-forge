using HydraForge.Domain.Enums;

namespace HydraForge.Application.Cards;

public record CardRelationshipDto(
    Guid Id,
    Guid SourceCardId,
    Guid TargetCardId,
    int SourceCardNumber,
    string SourceCardTitle,
    int TargetCardNumber,
    string TargetCardTitle,
    RelationshipType Type,
    DateTime CreatedAt,
    Guid CreatedByUserId,
    DateTime? ArchivedAt
);

public record CardRelationshipListResponse(List<CardRelationshipDto> Relationships);

// Reverse=true swaps which side becomes SourceCardId — lets a client on card X
// declare "X is blocked by / follows targetCardId" instead of the default
// "X blocks / precedes targetCardId", without changing the storage convention
// (Source is always the blocker/predecessor) or breaking the existing wire contract.
public record CreateRelationshipRequest(
    Guid TargetCardId,
    RelationshipType Type,
    bool Reverse = false
);

public record CreateRelationshipCommand(
    Guid ProjectId,
    Guid SourceCardId,
    Guid TargetCardId,
    RelationshipType Type,
    Guid ActorId
);

public record DeleteRelationshipCommand(
    Guid ProjectId,
    Guid CardId,
    Guid RelationshipId,
    Guid ActorId
);

public record ArchiveImpactRequest(bool Confirm);

public record ArchiveImpactCommand(Guid ProjectId, Guid CardId, bool Confirm, Guid ActorId);

public record ArchiveImpactResponse(
    Guid CardId,
    List<DependentCardDto> DependentCards,
    bool RequiresConfirmation
);

public record DependentCardDto(
    Guid Id,
    int CardNumber,
    string Title,
    RelationshipType RelationshipType
);
