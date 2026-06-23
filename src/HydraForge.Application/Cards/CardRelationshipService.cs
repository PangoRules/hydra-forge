using HydraForge.Application.Audit;
using HydraForge.Application.ProjectSnapshots;
using HydraForge.Application.Projects;
using HydraForge.Application.Realtime;
using HydraForge.Domain.Common;
using HydraForge.Domain.Entities.ProjectSpace;
using HydraForge.Domain.Enums;

namespace HydraForge.Application.Cards;

public class CardRelationshipService(
    ICardRelationshipRepository relationshipRepo,
    ICardRepository cardRepo,
    IProjectMemberRepository memberRepo,
    IAuditLogWriter auditLogWriter,
    IProjectSnapshotRefresher snapshotRefresher,
    IProjectBoardEventPublisher publisher
)
{
    private readonly ICardRelationshipRepository _relationshipRepo = relationshipRepo;
    private readonly ICardRepository _cardRepo = cardRepo;
    private readonly IProjectMemberRepository _memberRepo = memberRepo;
    private readonly IAuditLogWriter _auditLogWriter = auditLogWriter;
    private readonly IProjectSnapshotRefresher _snapshotRefresher = snapshotRefresher;
    private readonly IProjectBoardEventPublisher _publisher = publisher;

    public async Task<Result<CardRelationshipListResponse>> ListAsync(
        Guid projectId,
        Guid cardId,
        Guid actorId,
        CancellationToken ct = default
    )
    {
        var membership = await _memberRepo.GetByProjectAndUserAsync(projectId, actorId, ct);
        if (membership == null)
            return Result<CardRelationshipListResponse>.Failure(
                new Error(DomainErrorCodes.Projects.MembershipDenied, "Access denied.")
            );

        var card = await _cardRepo.GetByIdAsync(cardId, ct);
        if (card == null || card.ProjectId != projectId)
            return Result<CardRelationshipListResponse>.Failure(
                new Error(DomainErrorCodes.Cards.NotFound, "Card not found.")
            );

        var relationships = await _relationshipRepo.ListActiveByCardAsync(cardId, ct);
        var cardIds = relationships
            .SelectMany(r => new[] { r.SourceCardId, r.TargetCardId })
            .Distinct()
            .ToList();
        var cardsById = await _cardRepo.GetByIdsAsync(cardIds, ct);

        var dtos = relationships.Select(r => MapToDto(r, cardsById)).ToList();
        return Result<CardRelationshipListResponse>.Success(new CardRelationshipListResponse(dtos));
    }

    public async Task<Result<CardRelationshipDto>> CreateAsync(
        CreateRelationshipCommand cmd,
        CancellationToken ct = default
    )
    {
        var membership = await _memberRepo.GetByProjectAndUserAsync(cmd.ProjectId, cmd.ActorId, ct);
        if (membership == null)
            return Result<CardRelationshipDto>.Failure(
                new Error(DomainErrorCodes.Projects.MembershipDenied, "Access denied.")
            );

        var sourceCard = await _cardRepo.GetByIdAsync(cmd.SourceCardId, ct);
        if (sourceCard == null)
            return Result<CardRelationshipDto>.Failure(
                new Error(DomainErrorCodes.Cards.NotFound, "Source card not found.")
            );
        if (sourceCard.ProjectId != cmd.ProjectId)
            return Result<CardRelationshipDto>.Failure(
                new Error(
                    DomainErrorCodes.Relationships.CrossProjectDenied,
                    "Source card is in a different project."
                )
            );

        var targetCard = await _cardRepo.GetByIdAsync(cmd.TargetCardId, ct);
        if (targetCard == null)
            return Result<CardRelationshipDto>.Failure(
                new Error(DomainErrorCodes.Cards.NotFound, "Target card not found.")
            );
        if (targetCard.ProjectId != cmd.ProjectId)
            return Result<CardRelationshipDto>.Failure(
                new Error(
                    DomainErrorCodes.Relationships.CrossProjectDenied,
                    "Target card is in a different project."
                )
            );

        if (cmd.SourceCardId == cmd.TargetCardId)
            return Result<CardRelationshipDto>.Failure(
                new Error(
                    DomainErrorCodes.Relationships.SelfDenied,
                    "Card cannot have a relationship with itself."
                )
            );

        var existing = await _relationshipRepo.FindActiveAsync(
            cmd.SourceCardId,
            cmd.TargetCardId,
            cmd.Type,
            ct
        );
        if (existing != null)
            return Result<CardRelationshipDto>.Failure(
                new Error(DomainErrorCodes.Relationships.Duplicate, "Relationship already exists.")
            );

        var allRelationships = await _relationshipRepo.ListActiveByCardAsync(cmd.SourceCardId, ct);
        allRelationships =
        [
            .. allRelationships
                .Concat(await _relationshipRepo.ListActiveByCardAsync(cmd.TargetCardId, ct))
                .Distinct(),
        ];
        var cardIds = allRelationships
            .SelectMany(r => new[] { r.SourceCardId, r.TargetCardId })
            .Distinct()
            .ToList();
        var cardsById = new Dictionary<Guid, Card>(await _cardRepo.GetByIdsAsync(cardIds, ct))
        {
            [sourceCard.Id] = sourceCard,
            [targetCard.Id] = targetCard,
        };

        if (
            CardDependencyGraph.WouldCreateCycle(
                cmd.SourceCardId,
                cmd.TargetCardId,
                cmd.Type,
                allRelationships
            )
        )
            return Result<CardRelationshipDto>.Failure(
                new Error(
                    DomainErrorCodes.Relationships.Cycle,
                    "Relationship would create a cycle."
                )
            );

        var relationship = new CardRelationship
        {
            Id = Guid.NewGuid(),
            SourceCardId = cmd.SourceCardId,
            TargetCardId = cmd.TargetCardId,
            Type = cmd.Type,
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = cmd.ActorId,
        };

        await _relationshipRepo.AddAsync(relationship, ct);

        await _auditLogWriter.WriteAsync(
            new AuditLogRequest(
                cmd.ActorId,
                AuditLogScope.Project,
                "CardRelationship",
                relationship.Id,
                "Created",
                cmd.ProjectId,
                null,
                null
            ),
            ct
        );

        await PublishAsync(cmd.ProjectId, relationship.Id, BoardAction.Created, ct);
        await _snapshotRefresher.RefreshAsync(cmd.ProjectId, ct);

        cardsById.TryGetValue(relationship.SourceCardId, out var s);
        cardsById.TryGetValue(relationship.TargetCardId, out var t);
        return Result<CardRelationshipDto>.Success(MapToDto(relationship, cardsById));
    }

    public async Task<Result> DeleteAsync(
        DeleteRelationshipCommand cmd,
        CancellationToken ct = default
    )
    {
        var membership = await _memberRepo.GetByProjectAndUserAsync(cmd.ProjectId, cmd.ActorId, ct);
        if (membership == null)
            return Result.Failure(
                new Error(DomainErrorCodes.Projects.MembershipDenied, "Access denied.")
            );

        var relationship = await _relationshipRepo.GetByIdAsync(cmd.RelationshipId, ct);
        if (relationship == null)
            return Result.Failure(
                new Error(DomainErrorCodes.Relationships.NotFound, "Relationship not found.")
            );

        // Verify the cardId from the route matches either endpoint of the relationship
        if (relationship.SourceCardId != cmd.CardId && relationship.TargetCardId != cmd.CardId)
            return Result.Failure(
                new Error(DomainErrorCodes.Relationships.NotFound, "Relationship not found.")
            );

        var sourceCard = await _cardRepo.GetByIdAsync(relationship.SourceCardId, ct);
        if (sourceCard == null || sourceCard.ProjectId != cmd.ProjectId)
            return Result.Failure(
                new Error(DomainErrorCodes.Cards.NotFound, "Source card not found.")
            );

        await _relationshipRepo.ArchiveAsync(cmd.RelationshipId, ct);
        await _snapshotRefresher.RefreshAsync(cmd.ProjectId, ct);

        await _auditLogWriter.WriteAsync(
            new AuditLogRequest(
                cmd.ActorId,
                AuditLogScope.Project,
                "CardRelationship",
                relationship.Id,
                "Deleted",
                cmd.ProjectId,
                null,
                null
            ),
            ct
        );

        await PublishAsync(cmd.ProjectId, relationship.Id, BoardAction.Deleted, ct);

        return Result.Success();
    }

    public async Task<Result<ArchiveImpactResponse>> GetArchiveImpactAsync(
        ArchiveImpactCommand cmd,
        CancellationToken ct = default
    )
    {
        var membership = await _memberRepo.GetByProjectAndUserAsync(cmd.ProjectId, cmd.ActorId, ct);
        if (membership == null)
            return Result<ArchiveImpactResponse>.Failure(
                new Error(DomainErrorCodes.Projects.MembershipDenied, "Access denied.")
            );

        var card = await _cardRepo.GetByIdAsync(cmd.CardId, ct);
        if (card == null || card.ProjectId != cmd.ProjectId)
            return Result<ArchiveImpactResponse>.Failure(
                new Error(DomainErrorCodes.Cards.NotFound, "Card not found.")
            );

        var relationships = await _relationshipRepo.ListActiveByCardAsync(cmd.CardId, ct);
        var cardIds = relationships
            .SelectMany(r => new[] { r.SourceCardId, r.TargetCardId })
            .Distinct()
            .ToList();
        var cardsById = new Dictionary<Guid, Card>(await _cardRepo.GetByIdsAsync(cardIds, ct))
        {
            [card.Id] = card,
        };

        var dependentCards = new List<DependentCardDto>();

        var blockedBy = relationships.Where(r =>
            r.SourceCardId == cmd.CardId && r.Type == RelationshipType.BlockedBy
        );
        foreach (var rel in blockedBy)
        {
            if (cardsById.TryGetValue(rel.TargetCardId, out var dep) && dep.ArchivedAt == null)
                dependentCards.Add(
                    new DependentCardDto(
                        dep.Id,
                        dep.CardNumber,
                        dep.Title,
                        RelationshipType.BlockedBy
                    )
                );
        }

        var precedes = relationships.Where(r =>
            r.TargetCardId == cmd.CardId && r.Type == RelationshipType.Precedes
        );
        foreach (var rel in precedes)
        {
            if (cardsById.TryGetValue(rel.SourceCardId, out var dep) && dep.ArchivedAt == null)
                dependentCards.Add(
                    new DependentCardDto(
                        dep.Id,
                        dep.CardNumber,
                        dep.Title,
                        RelationshipType.Precedes
                    )
                );
        }

        return Result<ArchiveImpactResponse>.Success(
            new ArchiveImpactResponse(cmd.CardId, dependentCards, dependentCards.Count > 0)
        );
    }

    public async Task<Result<ArchiveImpactResponse>> ArchiveCardWithRelationshipsAsync(
        ArchiveImpactCommand cmd,
        CancellationToken ct = default
    )
    {
        var impact = await GetArchiveImpactAsync(cmd, ct);
        if (impact.IsFailure)
            return Result<ArchiveImpactResponse>.Failure(impact.Error);

        if (impact.Value.DependentCards.Count > 0 && !cmd.Confirm)
            return Result<ArchiveImpactResponse>.Failure(
                new Error(
                    DomainErrorCodes.Relationships.ArchiveImpactConfirmRequired,
                    "Confirmation required to archive card with dependents."
                )
            );

        var card = await _cardRepo.GetByIdAsync(cmd.CardId, ct);
        if (card != null)
        {
            card.Archive();
            await _cardRepo.UpdateAsync(card, ct);
        }

        var relationshipIds = (await _relationshipRepo.ListActiveByCardAsync(cmd.CardId, ct))
            .Select(r => r.Id)
            .ToList();
        await _relationshipRepo.ArchiveRangeAsync(relationshipIds, ct);
        await _snapshotRefresher.RefreshAsync(cmd.ProjectId, ct);

        await _auditLogWriter.WriteAsync(
            new AuditLogRequest(
                cmd.ActorId,
                AuditLogScope.Project,
                "Card",
                cmd.CardId,
                "ArchivedWithRelationships",
                cmd.ProjectId,
                null,
                null
            ),
            ct
        );

        return Result<ArchiveImpactResponse>.Success(
            new ArchiveImpactResponse(
                cmd.CardId,
                impact.Value.DependentCards,
                impact.Value.RequiresConfirmation
            )
        );
    }

    private async Task PublishAsync(Guid projectId, Guid entityId, BoardAction action, CancellationToken ct)
    {
        var envelope = new ProjectBoardEventEnvelope(
            Guid.NewGuid(),
            projectId,
            BoardEntityType.CardRelationship,
            entityId,
            action,
            1,
            DateTime.UtcNow,
            null!
        );
        await _publisher.PublishAsync(envelope, ct);
    }

    private static CardRelationshipDto MapToDto(
        CardRelationship r,
        IReadOnlyDictionary<Guid, Card> cardsById
    )
    {
        cardsById.TryGetValue(r.SourceCardId, out var source);
        cardsById.TryGetValue(r.TargetCardId, out var target);
        return new CardRelationshipDto(
            r.Id,
            r.SourceCardId,
            r.TargetCardId,
            source?.CardNumber ?? 0,
            source?.Title ?? string.Empty,
            target?.CardNumber ?? 0,
            target?.Title ?? string.Empty,
            r.Type,
            r.CreatedAt,
            r.CreatedByUserId,
            r.ArchivedAt
        );
    }
}
