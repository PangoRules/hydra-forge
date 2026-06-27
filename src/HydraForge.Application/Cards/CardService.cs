using HydraForge.Application.Audit;
using HydraForge.Application.Auth;
using HydraForge.Application.ProjectSnapshots;
using HydraForge.Application.Projects;
using HydraForge.Application.Realtime;
using HydraForge.Domain.Common;
using HydraForge.Domain.Entities.ProjectSpace;
using HydraForge.Domain.Enums;

namespace HydraForge.Application.Cards;

public class CardService(
    ICardRepository cardRepo,
    ICardAssigneeRepository assigneeRepo,
    ICardWatcherRepository watcherRepo,
    ICardRelationshipRepository relationshipRepo,
    IColumnRepository columnRepo,
    IProjectMemberRepository memberRepo,
    IUserRepository userRepo,
    IAuditLogWriter auditLogWriter,
    IProjectSnapshotRefresher snapshotRefresher,
    IProjectBoardEventPublisher publisher
)
{
    private readonly ICardRepository _cardRepo = cardRepo;
    private readonly ICardAssigneeRepository _assigneeRepo = assigneeRepo;
    private readonly ICardWatcherRepository _watcherRepo = watcherRepo;
    private readonly ICardRelationshipRepository _relationshipRepo = relationshipRepo;
    private readonly IColumnRepository _columnRepo = columnRepo;
    private readonly IProjectMemberRepository _memberRepo = memberRepo;
    private readonly IUserRepository _userRepo = userRepo;
    private readonly IAuditLogWriter _auditLogWriter = auditLogWriter;
    private readonly IProjectSnapshotRefresher _snapshotRefresher = snapshotRefresher;
    private readonly IProjectBoardEventPublisher _publisher = publisher;

    public async Task<Result<CardDto>> CreateAsync(
        CreateCardCommand cmd,
        CancellationToken ct = default
    )
    {
        var membership = await _memberRepo.GetByProjectAndUserAsync(cmd.ProjectId, cmd.ActorId, ct);
        if (membership == null)
            return Result<CardDto>.Failure(
                new Error(DomainErrorCodes.Projects.MembershipDenied, "Access denied.")
            );

        var column = await _columnRepo.GetByIdAsync(cmd.ColumnId, ct);
        if (column == null || column.ProjectId != cmd.ProjectId)
            return Result<CardDto>.Failure(
                new Error(DomainErrorCodes.Columns.NotFound, "Column not found.")
            );

        // Retry on unique constraint violation (race condition on CardNumber)
        const int maxRetries = 3;
        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            var maxNumber = await _cardRepo.GetMaxCardNumberAsync(cmd.ProjectId, ct);
            var cardCount = await _cardRepo.CountByColumnIdAsync(cmd.ColumnId, ct);
            if (cmd.ParentCardId.HasValue)
            {
                Card? parentCard = await _cardRepo.GetByIdAsync(cmd.ParentCardId.Value, ct);
                if (parentCard == null)
                    return Result<CardDto>.Failure(
                        new Error(DomainErrorCodes.Cards.NotFound, "Parent card not found.")
                    );

                var parentError = Card.ValidateParentEpic(
                    new Card
                    {
                        Id = Guid.Empty,
                        ProjectId = cmd.ProjectId,
                        Type = cmd.Type,
                    },
                    parentCard
                );
                if (parentError != null)
                    return Result<CardDto>.Failure(parentError);
            }

            var card = new Card
            {
                Id = Guid.NewGuid(),
                ProjectId = cmd.ProjectId,
                ColumnId = cmd.ColumnId,
                ParentCardId = cmd.ParentCardId,
                CardNumber = maxNumber + 1,
                Title = cmd.Title,
                Description = cmd.Description,
                Type = cmd.Type,
                Position = cardCount,
                DueAt = cmd.DueAt,
                Version = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                MovedAt = DateTime.UtcNow,
            };

            try
            {
                await _cardRepo.AddAsync(card, ct);
                // Success - exit retry loop
                await _snapshotRefresher.RefreshAsync(cmd.ProjectId, ct);

                await _auditLogWriter.WriteAsync(
                    new AuditLogRequest(
                        cmd.ActorId,
                        AuditLogScope.Project,
                        "Card",
                        card.Id,
                        "Created",
                        cmd.ProjectId,
                        null,
                        null
                    ),
                    ct
                );

                // Assign requested users after card creation (batch flow — no N+1)
                if (cmd.AssigneeUserIds is { Count: > 0 })
                {
                    var usersById = await _userRepo.FindByIdsAsync(cmd.AssigneeUserIds, ct);
                    if (usersById.Count > 0)
                    {
                        var existingAssignees = await _assigneeRepo.ListByCardAsync(card.Id, ct);
                        var existingWatchers = await _watcherRepo.ListByCardAsync(card.Id, ct);
                        var existingAssigneeUserIds = existingAssignees.Select(a => a.UserId).ToHashSet();
                        var existingWatcherUserIds = existingWatchers.Select(w => w.UserId).ToHashSet();

                        var newAssignees = new List<CardAssignee>(usersById.Count);
                        var newWatchers = new List<CardWatcher>(usersById.Count);

                        foreach (var userId in usersById.Keys)
                        {
                            if (existingAssigneeUserIds.Contains(userId))
                                continue;

                            newAssignees.Add(new CardAssignee
                            {
                                Id = Guid.NewGuid(),
                                CardId = card.Id,
                                UserId = userId,
                                AssignedAt = DateTime.UtcNow,
                                AssignedByUserId = cmd.ActorId,
                            });

                            if (!existingWatcherUserIds.Contains(userId))
                            {
                                newWatchers.Add(new CardWatcher
                                {
                                    CardId = card.Id,
                                    UserId = userId,
                                    AddedAt = DateTime.UtcNow,
                                });
                            }
                        }

                        if (newAssignees.Count > 0)
                            await _assigneeRepo.AddRangeAsync(newAssignees, ct);
                        if (newWatchers.Count > 0)
                            await _watcherRepo.AddRangeAsync(newWatchers, ct);
                    }
                }

                await PublishAsync(cmd.ProjectId, BoardEntityType.Card, card.Id, BoardAction.Created, ct);

                return Result<CardDto>.Success(await MapToDtoAsync(card, ct));
            }
            catch (Exception ex) when (ex.Message.Contains("23505") 
                || ex.Message.Contains("duplicate key") 
                || ex.Message.Contains("IX_cards_ProjectId_CardNumber")
                || ex.InnerException?.Message.Contains("23505") == true
                || ex.InnerException?.Message.Contains("duplicate key") == true)
            {
                // Unique constraint violation on CardNumber - retry with fresh max
                if (attempt == maxRetries - 1)
                    return Result<CardDto>.Failure(
                        new Error(DomainErrorCodes.Cards.ConcurrencyConflict, "Failed to generate unique card number after retries.")
                    );
                // Continue loop to retry
            }
        }

        return Result<CardDto>.Failure(
            new Error(DomainErrorCodes.Cards.ConcurrencyConflict, "Failed to generate unique card number after retries.")
        );
    }

    private async Task PublishAsync(Guid projectId, BoardEntityType entityType, Guid entityId, BoardAction action, CancellationToken ct)
    {
        var envelope = new ProjectBoardEventEnvelope(
            Guid.NewGuid(),
            projectId,
            entityType,
            entityId,
            action,
            1,
            DateTime.UtcNow,
            null!
        );
        await _publisher.PublishAsync(envelope, ct);
    }

    public async Task<Result<CardDto>> GetByIdAsync(
        Guid projectId,
        Guid cardId,
        Guid actorId,
        CancellationToken ct = default
    )
    {
        var membership = await _memberRepo.GetByProjectAndUserAsync(projectId, actorId, ct);
        if (membership == null)
            return Result<CardDto>.Failure(
                new Error(DomainErrorCodes.Projects.MembershipDenied, "Access denied.")
            );

        var card = await _cardRepo.GetByIdAsync(cardId, ct);
        if (card == null || card.ProjectId != projectId)
            return Result<CardDto>.Failure(
                new Error(DomainErrorCodes.Cards.NotFound, "Card not found.")
            );

        return Result<CardDto>.Success(await MapToDtoAsync(card, ct));
    }

    public async Task<Result<CardDto>> GetByNumberAsync(
        Guid projectId,
        int cardNumber,
        Guid actorId,
        CancellationToken ct = default
    )
    {
        var membership = await _memberRepo.GetByProjectAndUserAsync(projectId, actorId, ct);
        if (membership == null)
            return Result<CardDto>.Failure(
                new Error(DomainErrorCodes.Projects.MembershipDenied, "Access denied.")
            );

        var card = await _cardRepo.GetByProjectAndNumberAsync(projectId, cardNumber, ct);
        if (card == null)
            return Result<CardDto>.Failure(
                new Error(DomainErrorCodes.Cards.NotFound, "Card not found.")
            );

        return Result<CardDto>.Success(await MapToDtoAsync(card, ct));
    }

    public async Task<Result<IReadOnlyList<CardDto>>> ListAsync(
        Guid projectId,
        CardListFilter filter,
        Guid actorId,
        CancellationToken ct = default
    )
    {
        var membership = await _memberRepo.GetByProjectAndUserAsync(projectId, actorId, ct);
        if (membership == null)
            return Result<IReadOnlyList<CardDto>>.Failure(
                new Error(DomainErrorCodes.Projects.MembershipDenied, "Access denied.")
            );

        var cards = await _cardRepo.ListByProjectAsync(projectId, filter, ct);

        if (filter.AssigneeUserId.HasValue)
        {
            var allCardIds = cards.Select(c => c.Id).ToList();
            var assigneeLookup = await _assigneeRepo.ListByCardIdsAsync(allCardIds, ct);
            cards =
            [
                .. cards.Where(c =>
                    assigneeLookup[c.Id].Any(a => a.UserId == filter.AssigneeUserId.Value)
                ),
            ];
        }

        var cardIds = cards.Select(c => c.Id).ToList();
        var assigneeLookupFinal = await _assigneeRepo.ListByCardIdsAsync(cardIds, ct);
        var watcherLookupFinal = await _watcherRepo.ListByCardIdsAsync(cardIds, ct);
        var allUserIds = assigneeLookupFinal
            .SelectMany(g => g.Select(a => a.UserId))
            .Concat(watcherLookupFinal.SelectMany(g => g.Select(w => w.UserId)))
            .Distinct()
            .ToList();
        var usersById =
            allUserIds.Count > 0
                ? await _userRepo.FindByIdsAsync(allUserIds, ct)
                : new Dictionary<Guid, HydraForge.Domain.Entities.Auth.User>();

        var dtos = cards
            .Select(card => MapCardToDto(card, assigneeLookupFinal, watcherLookupFinal, usersById))
            .ToList();

        return Result<IReadOnlyList<CardDto>>.Success(dtos);
    }

    public async Task<Result<CardDto>> UpdateAsync(
        UpdateCardCommand cmd,
        CancellationToken ct = default
    )
    {
        var membership = await _memberRepo.GetByProjectAndUserAsync(cmd.ProjectId, cmd.ActorId, ct);
        if (membership == null)
            return Result<CardDto>.Failure(
                new Error(DomainErrorCodes.Projects.MembershipDenied, "Access denied.")
            );

        var card = await _cardRepo.GetByIdAsync(cmd.CardId, ct);
        if (card == null || card.ProjectId != cmd.ProjectId)
            return Result<CardDto>.Failure(
                new Error(DomainErrorCodes.Cards.NotFound, "Card not found.")
            );

        if (card.Version != cmd.Version)
            return Result<CardDto>.Failure(
                new Error(DomainErrorCodes.Cards.ConcurrencyMismatch, "Card has been modified.")
            );

        if (card.ArchivedAt != null)
            return Result<CardDto>.Failure(
                new Error(DomainErrorCodes.Cards.Archived, "Card is archived.")
            );
        if (cmd.ParentCardId.HasValue)
        {
            Card? parentCard = await _cardRepo.GetByIdAsync(cmd.ParentCardId.Value, ct);
            if (parentCard == null)
                return Result<CardDto>.Failure(
                    new Error(DomainErrorCodes.Cards.NotFound, "Parent card not found.")
                );

            var parentError = Card.ValidateParentEpic(card, parentCard);
            if (parentError != null)
                return Result<CardDto>.Failure(parentError);
        }

        card.UpdateDetails(
            cmd.Title,
            cmd.Description,
            cmd.Type,
            cmd.ParentCardId,
            cmd.DueAt
        );

        await _cardRepo.UpdateAsync(card, ct);
        await _snapshotRefresher.RefreshAsync(cmd.ProjectId, ct);

        await _auditLogWriter.WriteAsync(
            new AuditLogRequest(
                cmd.ActorId,
                AuditLogScope.Project,
                "Card",
                card.Id,
                "Updated",
                cmd.ProjectId,
                null,
                null
            ),
            ct
        );

        await PublishAsync(cmd.ProjectId, BoardEntityType.Card, card.Id, BoardAction.Updated, ct);

        return Result<CardDto>.Success(await MapToDtoAsync(card, ct));
    }

    public async Task<Result<BlockedMoveWarningDto>> GetBlockedMoveWarningAsync(
        Guid projectId,
        Guid cardId,
        Guid actorId,
        CancellationToken ct = default
    )
    {
        var membership = await _memberRepo.GetByProjectAndUserAsync(projectId, actorId, ct);
        if (membership == null)
            return Result<BlockedMoveWarningDto>.Failure(
                new Error(DomainErrorCodes.Projects.MembershipDenied, "Access denied.")
            );

        var card = await _cardRepo.GetByIdAsync(cardId, ct);
        if (card == null || card.ProjectId != projectId)
            return Result<BlockedMoveWarningDto>.Failure(
                new Error(DomainErrorCodes.Cards.NotFound, "Card not found.")
            );

        var blockers = await _relationshipRepo.ListBlockersForCardAsync(cardId, ct);
        var predecessors = await _relationshipRepo.ListPredecessorsAsync(cardId, ct);

        var relatedCardIds = blockers
            .Select(b => b.SourceCardId)
            .Concat(predecessors.Select(p => p.TargetCardId))
            .Distinct()
            .ToList();

        var cardsById =
            relatedCardIds.Count > 0
                ? await _cardRepo.GetByIdsAsync(relatedCardIds, ct)
                : new Dictionary<Guid, Card>();

        var blockerDtos = new List<BlockerDto>();

        foreach (var blocker in blockers)
        {
            if (
                cardsById.TryGetValue(blocker.SourceCardId, out var blockerCard)
                && blockerCard.ArchivedAt == null
            )
            {
                blockerDtos.Add(
                    new BlockerDto(
                        blockerCard.Id,
                        blockerCard.CardNumber,
                        blockerCard.Title,
                        RelationshipBlockerType.BlockedBy
                    )
                );
            }
        }

        foreach (var pred in predecessors)
        {
            if (
                cardsById.TryGetValue(pred.TargetCardId, out var predCard)
                && predCard.ArchivedAt == null
            )
            {
                blockerDtos.Add(
                    new BlockerDto(
                        predCard.Id,
                        predCard.CardNumber,
                        predCard.Title,
                        RelationshipBlockerType.Precedes
                    )
                );
            }
        }

        return Result<BlockedMoveWarningDto>.Success(
            new BlockedMoveWarningDto(cardId, blockerDtos)
        );
    }

    public async Task<Result<CardDto>> MoveAsync(
        MoveCardCommand cmd,
        CancellationToken ct = default
    )
    {
        var membership = await _memberRepo.GetByProjectAndUserAsync(cmd.ProjectId, cmd.ActorId, ct);
        if (membership == null)
            return Result<CardDto>.Failure(
                new Error(DomainErrorCodes.Projects.MembershipDenied, "Access denied.")
            );

        var card = await _cardRepo.GetByIdAsync(cmd.CardId, ct);
        if (card == null || card.ProjectId != cmd.ProjectId)
            return Result<CardDto>.Failure(
                new Error(DomainErrorCodes.Cards.NotFound, "Card not found.")
            );

        if (card.Version != cmd.Version)
            return Result<CardDto>.Failure(
                new Error(DomainErrorCodes.Cards.ConcurrencyMismatch, "Card has been modified.")
            );

        var targetColumn = await _columnRepo.GetByIdAsync(cmd.TargetColumnId, ct);
        if (targetColumn == null || targetColumn.ProjectId != cmd.ProjectId)
            return Result<CardDto>.Failure(
                new Error(DomainErrorCodes.Columns.NotFound, "Target column not found.")
            );

        var blockingRelationships = (
            await _relationshipRepo.ListBlockersForCardAsync(cmd.CardId, ct)
        ).Concat(await _relationshipRepo.ListPredecessorsAsync(cmd.CardId, ct));
        var hasBlockers = false;
        foreach (var relationship in blockingRelationships)
        {
            var relatedCardId =
                relationship.SourceCardId == cmd.CardId
                    ? relationship.TargetCardId
                    : relationship.SourceCardId;
            var relatedCard = await _cardRepo.GetByIdAsync(relatedCardId, ct);
            if (relatedCard != null && relatedCard.ArchivedAt == null)
            {
                hasBlockers = true;
                break;
            }
        }

        if (hasBlockers && !cmd.ConfirmBlockedMove)
        {
            var warning = await GetBlockedMoveWarningAsync(
                cmd.ProjectId,
                cmd.CardId,
                cmd.ActorId,
                ct
            );
            if (warning.IsFailure)
                return Result<CardDto>.Failure(warning.Error);

            return Result<CardDto>.Failure(
                new Error(DomainErrorCodes.Cards.BlockedMoveWarning, "Card has blockers.")
            );
        }

        var oldColumnId = card.ColumnId;
        var oldPosition = card.Position;

        var toUpdate = new List<Card>();

        if (oldColumnId == cmd.TargetColumnId)
        {
            if (oldPosition > cmd.TargetPosition)
            {
                var allCards = await _cardRepo.ListByProjectAsync(
                    cmd.ProjectId,
                    new CardListFilter(cmd.TargetColumnId, true),
                    ct
                );
                var cardsToShift = allCards
                    .Where(c =>
                        c.Position >= cmd.TargetPosition
                        && c.Position < oldPosition
                        && c.Id != card.Id
                    )
                    .ToList();
                foreach (var c in cardsToShift)
                {
                    c.ShiftPosition(1);
                    toUpdate.Add(c);
                }
            }
        }
        else
        {
            await _cardRepo.CompactColumnPositionsAsync(oldColumnId, oldPosition, ct);
            var cardsInTarget = await _cardRepo.ListByProjectAsync(
                cmd.ProjectId,
                new CardListFilter(cmd.TargetColumnId, true),
                ct
            );
            foreach (var c in cardsInTarget.Where(c => c.Position >= cmd.TargetPosition))
            {
                c.ShiftPosition(1);
                toUpdate.Add(c);
            }
        }

        card.MoveTo(cmd.TargetColumnId, cmd.TargetPosition);
        toUpdate.Add(card);

        await _cardRepo.UpdateRangeAsync(toUpdate, ct);
        await _snapshotRefresher.RefreshAsync(cmd.ProjectId, ct);

        await _auditLogWriter.WriteAsync(
            new AuditLogRequest(
                cmd.ActorId,
                AuditLogScope.Project,
                "Card",
                card.Id,
                "Moved",
                cmd.ProjectId,
                null,
                null
            ),
            ct
        );

        await PublishAsync(cmd.ProjectId, BoardEntityType.Card, card.Id, BoardAction.Moved, ct);

        return Result<CardDto>.Success(await MapToDtoAsync(card, ct));
    }

    public async Task<Result<CardDto>> AssignAsync(
        AssignCardCommand cmd,
        CancellationToken ct = default
    )
    {
        var membership = await _memberRepo.GetByProjectAndUserAsync(cmd.ProjectId, cmd.ActorId, ct);
        if (membership == null)
            return Result<CardDto>.Failure(
                new Error(DomainErrorCodes.Projects.MembershipDenied, "Access denied.")
            );

        var card = await _cardRepo.GetByIdAsync(cmd.CardId, ct);
        if (card == null || card.ProjectId != cmd.ProjectId)
            return Result<CardDto>.Failure(
                new Error(DomainErrorCodes.Cards.NotFound, "Card not found.")
            );

        var assigneeUser = await _userRepo.FindByIdAsync(cmd.AssigneeUserId, ct);
        if (assigneeUser == null)
            return Result<CardDto>.Failure(
                new Error(DomainErrorCodes.Cards.InvalidAssignee, "Assignee user not found.")
            );

        var existing = await _assigneeRepo.GetByCardAndUserAsync(
            cmd.CardId,
            cmd.AssigneeUserId,
            ct
        );
        if (existing != null)
            return Result<CardDto>.Failure(
                new Error(DomainErrorCodes.Cards.DuplicateAssignee, "User is already assigned.")
            );

        var assignee = new CardAssignee
        {
            Id = Guid.NewGuid(),
            CardId = cmd.CardId,
            UserId = cmd.AssigneeUserId,
            AssignedAt = DateTime.UtcNow,
            AssignedByUserId = cmd.ActorId,
        };
        await _assigneeRepo.AddAsync(assignee, ct);

        var watcher = await _watcherRepo.GetByCardAndUserAsync(cmd.CardId, cmd.AssigneeUserId, ct);
        if (watcher == null)
        {
            var newWatcher = new CardWatcher
            {
                CardId = cmd.CardId,
                UserId = cmd.AssigneeUserId,
                AddedAt = DateTime.UtcNow,
            };
            await _watcherRepo.AddAsync(newWatcher, ct);
        }

        await _auditLogWriter.WriteAsync(
            new AuditLogRequest(
                cmd.ActorId,
                AuditLogScope.Project,
                "Card",
                card.Id,
                "Assigned",
                cmd.ProjectId,
                null,
                null
            ),
            ct
        );

        await PublishAsync(cmd.ProjectId, BoardEntityType.Card, card.Id, BoardAction.Assigned, ct);

        return Result<CardDto>.Success(await MapToDtoAsync(card, ct));
    }

    public async Task<Result<CardDto>> UnassignAsync(
        UnassignCardCommand cmd,
        CancellationToken ct = default
    )
    {
        var membership = await _memberRepo.GetByProjectAndUserAsync(cmd.ProjectId, cmd.ActorId, ct);
        if (membership == null)
            return Result<CardDto>.Failure(
                new Error(DomainErrorCodes.Projects.MembershipDenied, "Access denied.")
            );

        var card = await _cardRepo.GetByIdAsync(cmd.CardId, ct);
        if (card == null || card.ProjectId != cmd.ProjectId)
            return Result<CardDto>.Failure(
                new Error(DomainErrorCodes.Cards.NotFound, "Card not found.")
            );

        var existing = await _assigneeRepo.GetByCardAndUserAsync(
            cmd.CardId,
            cmd.AssigneeUserId,
            ct
        );
        if (existing == null)
            return Result<CardDto>.Failure(
                new Error(DomainErrorCodes.Cards.InvalidAssignee, "Assignee not found.")
            );

        await _assigneeRepo.RemoveAsync(cmd.CardId, cmd.AssigneeUserId, ct);

        await _auditLogWriter.WriteAsync(
            new AuditLogRequest(
                cmd.ActorId,
                AuditLogScope.Project,
                "Card",
                card.Id,
                "Unassigned",
                cmd.ProjectId,
                null,
                null
            ),
            ct
        );

        await PublishAsync(cmd.ProjectId, BoardEntityType.Card, card.Id, BoardAction.Unassigned, ct);

        return Result<CardDto>.Success(await MapToDtoAsync(card, ct));
    }

    public async Task<Result<CardDto>> ArchiveAsync(
        ArchiveCardCommand cmd,
        CancellationToken ct = default
    )
    {
        var membership = await _memberRepo.GetByProjectAndUserAsync(cmd.ProjectId, cmd.ActorId, ct);
        if (membership == null)
            return Result<CardDto>.Failure(
                new Error(DomainErrorCodes.Projects.MembershipDenied, "Access denied.")
            );

        var card = await _cardRepo.GetByIdAsync(cmd.CardId, ct);
        if (card == null || card.ProjectId != cmd.ProjectId)
            return Result<CardDto>.Failure(
                new Error(DomainErrorCodes.Cards.NotFound, "Card not found.")
            );

        if (card.Version != cmd.Version)
            return Result<CardDto>.Failure(
                new Error(DomainErrorCodes.Cards.ConcurrencyMismatch, "Card has been modified.")
            );

        var oldPosition = card.Position;
        var oldColumnId = card.ColumnId;

        card.Archive();

        await _cardRepo.UpdateAsync(card, ct);
        await _cardRepo.CompactColumnPositionsAsync(oldColumnId, oldPosition, ct);
        await _snapshotRefresher.RefreshAsync(cmd.ProjectId, ct);

        await _auditLogWriter.WriteAsync(
            new AuditLogRequest(
                cmd.ActorId,
                AuditLogScope.Project,
                "Card",
                card.Id,
                "Archived",
                cmd.ProjectId,
                null,
                null
            ),
            ct
        );

        await PublishAsync(cmd.ProjectId, BoardEntityType.Card, card.Id, BoardAction.Archived, ct);

        return Result<CardDto>.Success(await MapToDtoAsync(card, ct));
    }

    public async Task<Result<CardDto>> RestoreAsync(
        RestoreCardCommand cmd,
        CancellationToken ct = default
    )
    {
        var membership = await _memberRepo.GetByProjectAndUserAsync(cmd.ProjectId, cmd.ActorId, ct);
        if (membership == null)
            return Result<CardDto>.Failure(
                new Error(DomainErrorCodes.Projects.MembershipDenied, "Access denied.")
            );

        var card = await _cardRepo.GetByIdAsync(cmd.CardId, ct);
        if (card == null || card.ProjectId != cmd.ProjectId)
            return Result<CardDto>.Failure(
                new Error(DomainErrorCodes.Cards.NotFound, "Card not found.")
            );

        if (card.Version != cmd.Version)
            return Result<CardDto>.Failure(
                new Error(DomainErrorCodes.Cards.ConcurrencyMismatch, "Card has been modified.")
            );

        if (card.ArchivedAt == null)
            return Result<CardDto>.Failure(
                new Error(DomainErrorCodes.Cards.Archived, "Card is not archived.")
            );

        var maxPosition = await _cardRepo.CountByColumnIdAsync(card.ColumnId, ct);

        card.Restore();
        card.Position = maxPosition + 1;

        await _cardRepo.UpdateAsync(card, ct);
        await _snapshotRefresher.RefreshAsync(cmd.ProjectId, ct);

        await _auditLogWriter.WriteAsync(
            new AuditLogRequest(
                cmd.ActorId,
                AuditLogScope.Project,
                "Card",
                card.Id,
                "Restored",
                cmd.ProjectId,
                null,
                null
            ),
            ct
        );

        await PublishAsync(cmd.ProjectId, BoardEntityType.Card, card.Id, BoardAction.Restored, ct);

        return Result<CardDto>.Success(await MapToDtoAsync(card, ct));
    }

    public async Task<Result> DeleteAsync(DeleteCardCommand cmd, CancellationToken ct = default)
    {
        var membership = await _memberRepo.GetByProjectAndUserAsync(cmd.ProjectId, cmd.ActorId, ct);
        if (membership == null)
            return Result.Failure(
                new Error(DomainErrorCodes.Projects.MembershipDenied, "Access denied.")
            );

        var card = await _cardRepo.GetByIdAsync(cmd.CardId, ct);
        if (card == null || card.ProjectId != cmd.ProjectId)
            return Result.Failure(new Error(DomainErrorCodes.Cards.NotFound, "Card not found."));

        var oldPosition = card.Position;
        var oldColumnId = card.ColumnId;

        await _cardRepo.DeleteAsync(cmd.CardId, ct);
        await _cardRepo.CompactColumnPositionsAsync(oldColumnId, oldPosition, ct);
        await _snapshotRefresher.RefreshAsync(cmd.ProjectId, ct);

        await _auditLogWriter.WriteAsync(
            new AuditLogRequest(
                cmd.ActorId,
                AuditLogScope.Project,
                "Card",
                card.Id,
                "Deleted",
                cmd.ProjectId,
                null,
                null
            ),
            ct
        );

        return Result.Success();
    }

    private async Task<CardDto> MapToDtoAsync(Card card, CancellationToken ct)
    {
        var assignees = await _assigneeRepo.ListByCardAsync(card.Id, ct);
        var watchers = await _watcherRepo.ListByCardAsync(card.Id, ct);

        var allUserIds = assignees
            .Select(a => a.UserId)
            .Concat(watchers.Select(w => w.UserId))
            .Distinct()
            .ToList();

        var usersById = allUserIds.Count > 0
            ? await _userRepo.FindByIdsAsync(allUserIds, ct)
            : new Dictionary<Guid, HydraForge.Domain.Entities.Auth.User>();

        var assigneeDtos = assignees
            .Select(a => new CardAssigneeDto(
                a.Id,
                a.UserId,
                usersById.TryGetValue(a.UserId, out var u) ? u.Username : string.Empty,
                a.AssignedAt
            ))
            .ToList();

        var watcherDtos = watchers
            .Select(w => new CardWatcherDto(
                w.UserId,
                usersById.TryGetValue(w.UserId, out var u) ? u.Username : string.Empty,
                w.AddedAt
            ))
            .ToList();

        return new CardDto(
            card.Id,
            card.ProjectId,
            card.ColumnId,
            card.CardNumber,
            card.Title,
            card.Description,
            card.Type,
            card.Position,
            card.DueAt,
            card.Version,
            card.CreatedAt,
            card.UpdatedAt,
            card.MovedAt,
            card.ArchivedAt,
            card.ParentCardId,
            assigneeDtos,
            watcherDtos
        );
    }

    private static CardDto MapCardToDto(
        Card card,
        ILookup<Guid, CardAssignee> assigneeLookup,
        ILookup<Guid, CardWatcher> watcherLookup,
        IReadOnlyDictionary<Guid, HydraForge.Domain.Entities.Auth.User> usersById
    )
    {
        var assigneeDtos = assigneeLookup[card.Id]
            .Select(a => new CardAssigneeDto(
                a.Id,
                a.UserId,
                usersById.TryGetValue(a.UserId, out var u) ? u.Username : string.Empty,
                a.AssignedAt
            ))
            .ToList();

        var watcherDtos = watcherLookup[card.Id]
            .Select(w => new CardWatcherDto(
                w.UserId,
                usersById.TryGetValue(w.UserId, out var u) ? u.Username : string.Empty,
                w.AddedAt
            ))
            .ToList();

        return new CardDto(
            card.Id,
            card.ProjectId,
            card.ColumnId,
            card.CardNumber,
            card.Title,
            card.Description,
            card.Type,
            card.Position,
            card.DueAt,
            card.Version,
            card.CreatedAt,
            card.UpdatedAt,
            card.MovedAt,
            card.ArchivedAt,
            card.ParentCardId,
            assigneeDtos,
            watcherDtos
        );
    }
}
