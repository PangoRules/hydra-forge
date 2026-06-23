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
    DateTime? ArchivedAt);

public record CardRelationshipListResponse(List<CardRelationshipDto> Relationships);

public record CreateRelationshipRequest(Guid TargetCardId, RelationshipType Type);
public record CreateRelationshipCommand(Guid ProjectId, Guid SourceCardId, Guid TargetCardId, RelationshipType Type, Guid ActorId);
public record DeleteRelationshipCommand(Guid ProjectId, Guid CardId, Guid RelationshipId, Guid ActorId);
public record ArchiveImpactRequest(bool Confirm);
public record ArchiveImpactCommand(Guid ProjectId, Guid CardId, bool Confirm, Guid ActorId);
public record ArchiveImpactResponse(Guid CardId, List<DependentCardDto> DependentCards, bool RequiresConfirmation);
public record DependentCardDto(Guid Id, int CardNumber, string Title, RelationshipType RelationshipType);
