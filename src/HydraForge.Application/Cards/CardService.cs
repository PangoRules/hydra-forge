using HydraForge.Application.Audit;
using HydraForge.Application.Auth;
using HydraForge.Application.Logging;
using HydraForge.Application.Notifications;
using HydraForge.Application.Projects;
using HydraForge.Application.ProjectSnapshots;
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
    IProjectBoardEventPublisher publisher,
    INotificationService notifService,
    IWarnLogger warnLogger = null!
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
    private readonly INotificationService _notifService = notifService;
    private readonly IWarnLogger _warnLogger = warnLogger ?? new NullWarnLogger();

    private sealed record CardAuditSnapshot(
        Guid ColumnId,
        Guid? ParentCardId,
        string Title,
        string? Description,
        CardType Type,
        int Position,
        DateTime? DueAt,
        DateTime? ArchivedAt
    );

    private sealed record CardAssigneeAuditSnapshot(Guid UserId, string Username);

    private static CardAuditSnapshot BuildSnapshot(Card card) =>
        new(
            card.ColumnId,
            card.ParentCardId,
            card.Title,
            card.Description,
            card.Type,
            card.Position,
            card.DueAt,
            card.ArchivedAt
        );

    public async Task<Result<CardDto>> CreateAsync(
        CreateCardCommand cmd,
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

                var parentError = Card.ValidateParent(
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
                        AuditSnapshot.Serialize(BuildSnapshot(card))
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
                        var existingAssigneeUserIds = existingAssignees
                            .Select(a => a.UserId)
                            .ToHashSet();
                        var existingWatcherUserIds = existingWatchers
                            .Select(w => w.UserId)
                            .ToHashSet();

                        var newAssignees = new List<CardAssignee>(usersById.Count);
                        var newWatchers = new List<CardWatcher>(usersById.Count);

                        foreach (var userId in usersById.Keys)
                        {
                            if (existingAssigneeUserIds.Contains(userId))
                                continue;

                            newAssignees.Add(
                                new CardAssignee
                                {
                                    Id = Guid.NewGuid(),
                                    CardId = card.Id,
                                    UserId = userId,
                                    AssignedAt = DateTime.UtcNow,
                                    AssignedByUserId = cmd.ActorId,
                                }
                            );

                            if (!existingWatcherUserIds.Contains(userId))
                            {
                                newWatchers.Add(
                                    new CardWatcher
                                    {
                                        CardId = card.Id,
                                        UserId = userId,
                                        AddedAt = DateTime.UtcNow,
                                    }
                                );
                            }
                        }

                        if (newAssignees.Count > 0)
                            await _assigneeRepo.AddRangeAsync(newAssignees, ct);
                        if (newWatchers.Count > 0)
                            await _watcherRepo.AddRangeAsync(newWatchers, ct);
                    }
                }

                await PublishAsync(
                    cmd.ProjectId,
                    BoardEntityType.Card,
                    card.Id,
                    BoardAction.Created,
                    ct
                );

                return Result<CardDto>.Success(await MapToDtoAsync(card, ct));
            }
            catch (Exception ex)
                when (ex.Message.Contains("23505")
                    || ex.Message.Contains("duplicate key")
                    || ex.Message.Contains("IX_cards_ProjectId_CardNumber")
                    || ex.InnerException?.Message.Contains("23505") == true
                    || ex.InnerException?.Message.Contains("duplicate key") == true
                )
            {
                // Unique constraint violation on CardNumber - retry with fresh max
                if (attempt == maxRetries - 1)
                    return Result<CardDto>.Failure(
                        new Error(
                            DomainErrorCodes.Cards.ConcurrencyConflict,
                            "Failed to generate unique card number after retries."
                        )
                    );
                // Continue loop to retry
            }
        }

        return Result<CardDto>.Failure(
            new Error(
                DomainErrorCodes.Cards.ConcurrencyConflict,
                "Failed to generate unique card number after retries."
            )
        );
    }

    private async Task PublishAsync(
        Guid projectId,
        BoardEntityType entityType,
        Guid entityId,
        BoardAction action,
        CancellationToken ct
    )
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
        if (!await MembershipGuard.HasAccessAsync(_userRepo, _memberRepo, projectId, actorId, ct))
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
        if (!await MembershipGuard.HasAccessAsync(_userRepo, _memberRepo, projectId, actorId, ct))
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
        if (!await MembershipGuard.HasAccessAsync(_userRepo, _memberRepo, projectId, actorId, ct))
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
                : new Dictionary<Guid, Domain.Entities.Auth.User>();

        // One project-wide relationship fetch instead of a per-card query — a related
        // card not present in `cardsById` (archived/out of this filtered result) is
        // simply skipped when building badges, same as the single-card path.
        var relationships = await _relationshipRepo.ListActiveByProjectAsync(projectId, ct);
        var cardsById = cards.ToDictionary(c => c.Id);

        // Only count a relationship for a card if the *other* side is active — an
        // archived blocker/predecessor no longer applies, so it must not inflate the
        // count either (a stray "+1 more" pointing at a badge that will never render).
        var relationshipsByCard = relationships
            .SelectMany(r =>
                new[] { r.SourceCardId, r.TargetCardId }
                    .Distinct()
                    .Select(id =>
                        (
                            CardId: id,
                            OtherId: id == r.SourceCardId ? r.TargetCardId : r.SourceCardId,
                            Relationship: r
                        )
                    )
            )
            .Where(x => cardsById.ContainsKey(x.OtherId))
            .ToLookup(x => x.CardId, x => x.Relationship);
        var relationshipCounts = relationshipsByCard.ToDictionary(g => g.Key, g => g.Count());

        // Same in-memory-only approach as relationshipsByCard — a child outside this
        // filtered result set is simply not counted, same caveat as relationship badges.
        var childCountByParentId = cards
            .Where(c => c.ParentCardId.HasValue)
            .GroupBy(c => c.ParentCardId!.Value)
            .ToDictionary(g => g.Key, g => g.Count());

        var dtos = cards
            .Select(card =>
                MapCardToDto(
                    card,
                    assigneeLookupFinal,
                    watcherLookupFinal,
                    usersById,
                    cardsById,
                    relationshipCounts,
                    relationshipsByCard,
                    childCountByParentId
                )
            )
            .ToList();

        return Result<IReadOnlyList<CardDto>>.Success(dtos);
    }

    public async Task<Result<CardDto>> WatchAsync(
        WatchCardCommand cmd,
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
            return Result<CardDto>.Failure(
                new Error(DomainErrorCodes.Projects.MembershipDenied, "Access denied.")
            );

        var card = await _cardRepo.GetByIdAsync(cmd.CardId, ct);
        if (card == null || card.ProjectId != cmd.ProjectId)
            return Result<CardDto>.Failure(
                new Error(DomainErrorCodes.Cards.NotFound, "Card not found.")
            );

        var existing = await _watcherRepo.GetByCardAndUserAsync(cmd.CardId, cmd.ActorId, ct);
        if (existing == null)
        {
            await _watcherRepo.AddAsync(
                new CardWatcher
                {
                    CardId = cmd.CardId,
                    UserId = cmd.ActorId,
                    AddedAt = DateTime.UtcNow,
                },
                ct
            );
        }

        return Result<CardDto>.Success(await MapToDtoAsync(card, ct));
    }

    public async Task<Result<CardDto>> UnwatchAsync(
        UnwatchCardCommand cmd,
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
            return Result<CardDto>.Failure(
                new Error(DomainErrorCodes.Projects.MembershipDenied, "Access denied.")
            );

        var card = await _cardRepo.GetByIdAsync(cmd.CardId, ct);
        if (card == null || card.ProjectId != cmd.ProjectId)
            return Result<CardDto>.Failure(
                new Error(DomainErrorCodes.Cards.NotFound, "Card not found.")
            );

        await _watcherRepo.RemoveAsync(cmd.CardId, cmd.ActorId, ct);

        return Result<CardDto>.Success(await MapToDtoAsync(card, ct));
    }

    public async Task<Result<CardDto>> UpdateAsync(
        UpdateCardCommand cmd,
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

            var parentError = Card.ValidateParent(card, parentCard);
            if (parentError != null)
                return Result<CardDto>.Failure(parentError);
        }

        var oldSnapshot = BuildSnapshot(card);
        card.UpdateDetails(cmd.Title, cmd.Description, cmd.Type, cmd.ParentCardId, cmd.DueAt);

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
                AuditSnapshot.Serialize(oldSnapshot),
                AuditSnapshot.Serialize(BuildSnapshot(card))
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
        if (!await MembershipGuard.HasAccessAsync(_userRepo, _memberRepo, projectId, actorId, ct))
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
        if (
            !await MembershipGuard.HasAccessAsync(
                _userRepo,
                _memberRepo,
                cmd.ProjectId,
                cmd.ActorId,
                ct
            )
        )
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
        )
            .Concat(await _relationshipRepo.ListPredecessorsAsync(cmd.CardId, ct))
            .ToList();

        var blockingRelatedCardIds = blockingRelationships
            .Select(r => r.SourceCardId == cmd.CardId ? r.TargetCardId : r.SourceCardId)
            .Distinct()
            .ToList();
        var blockingRelatedCardsById =
            blockingRelatedCardIds.Count > 0
                ? await _cardRepo.GetByIdsAsync(blockingRelatedCardIds, ct)
                : new Dictionary<Guid, Card>();

        var hasBlockers = blockingRelationships.Any(relationship =>
        {
            var relatedCardId =
                relationship.SourceCardId == cmd.CardId
                    ? relationship.TargetCardId
                    : relationship.SourceCardId;
            return blockingRelatedCardsById.TryGetValue(relatedCardId, out var relatedCard)
                && relatedCard.ArchivedAt == null;
        });

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
        var oldSnapshot = BuildSnapshot(card);

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
            else if (oldPosition < cmd.TargetPosition)
            {
                var allCards = await _cardRepo.ListByProjectAsync(
                    cmd.ProjectId,
                    new CardListFilter(cmd.TargetColumnId, true),
                    ct
                );
                var cardsToShift = allCards
                    .Where(c =>
                        c.Position > oldPosition
                        && c.Position <= cmd.TargetPosition
                        && c.Id != card.Id
                    )
                    .ToList();
                foreach (var c in cardsToShift)
                {
                    c.ShiftPosition(-1);
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
                AuditSnapshot.Serialize(oldSnapshot),
                AuditSnapshot.Serialize(BuildSnapshot(card))
            ),
            ct
        );

        await PublishAsync(cmd.ProjectId, BoardEntityType.Card, card.Id, BoardAction.Moved, ct);

        // Notify assignees + watchers about card move
        var assignees = await _assigneeRepo.ListByCardAsync(card.Id, ct);
        var watchers = await _watcherRepo.ListByCardAsync(card.Id, ct);
        var recipientIds = assignees
            .Select(a => a.UserId)
            .Concat(watchers.Select(w => w.UserId))
            .Distinct()
            .ToList();

        var actorName = (await _userRepo.FindByIdAsync(cmd.ActorId, ct))?.Username ?? "Someone";
        var columnName = targetColumn.Name;

        var moveRequests = recipientIds
            .Where(id => id != cmd.ActorId)
            .Select(id => new NotifyRequest(
                id,
                cmd.ActorId,
                $"{actorName} moved #{card.CardNumber} to {columnName}",
                null,
                null,
                card.Id,
                cmd.ProjectId,
                $"/projects/{cmd.ProjectId}/board?card={card.Id}"
            ))
            .ToList();

        try
        {
            await _notifService.NotifyBatchAsync(moveRequests, ct);
        }
        catch (Exception ex)
        {
            _warnLogger.LogWarning($"Failed to send move notifications: {ex.Message}");
        }

        return Result<CardDto>.Success(await MapToDtoAsync(card, ct));
    }

    public async Task<Result<CardDto>> AssignAsync(
        AssignCardCommand cmd,
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
                AuditSnapshot.Serialize(
                    new CardAssigneeAuditSnapshot(assigneeUser.Id, assigneeUser.Username)
                )
            ),
            ct
        );

        await PublishAsync(cmd.ProjectId, BoardEntityType.Card, card.Id, BoardAction.Assigned, ct);

        // Notify new assignee
        var actorName = (await _userRepo.FindByIdAsync(cmd.ActorId, ct))?.Username ?? "Someone";
        try
        {
            await _notifService.NotifyAsync(
                new NotifyRequest(
                    cmd.AssigneeUserId,
                    cmd.ActorId,
                    $"{actorName} assigned you to #{card.CardNumber}",
                    card.Title,
                    null,
                    card.Id,
                    cmd.ProjectId,
                    $"/projects/{cmd.ProjectId}/board?card={card.Id}"
                ),
                ct
            );
        }
        catch (Exception ex)
        {
            _warnLogger.LogWarning($"Failed to send assignment notification: {ex.Message}");
        }

        return Result<CardDto>.Success(await MapToDtoAsync(card, ct));
    }

    public async Task<Result<CardDto>> UnassignAsync(
        UnassignCardCommand cmd,
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

        var removedUser = await _userRepo.FindByIdAsync(cmd.AssigneeUserId, ct);

        await _assigneeRepo.RemoveAsync(cmd.CardId, cmd.AssigneeUserId, ct);

        await _auditLogWriter.WriteAsync(
            new AuditLogRequest(
                cmd.ActorId,
                AuditLogScope.Project,
                "Card",
                card.Id,
                "Unassigned",
                cmd.ProjectId,
                AuditSnapshot.Serialize(
                    new CardAssigneeAuditSnapshot(
                        cmd.AssigneeUserId,
                        removedUser?.Username ?? "(deleted)"
                    )
                ),
                null
            ),
            ct
        );

        await PublishAsync(
            cmd.ProjectId,
            BoardEntityType.Card,
            card.Id,
            BoardAction.Unassigned,
            ct
        );

        return Result<CardDto>.Success(await MapToDtoAsync(card, ct));
    }

    public async Task<Result<CardDto>> ArchiveAsync(
        ArchiveCardCommand cmd,
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
        var oldSnapshot = BuildSnapshot(card);

        card.Archive();

        await _cardRepo.UpdateAsync(card, ct);
        await _cardRepo.CompactColumnPositionsAsync(oldColumnId, oldPosition, ct);
        await _snapshotRefresher.RefreshAsync(cmd.ProjectId, ct);

        // Archiving is the only action that can actually resolve a "blocked by" dependency —
        // moving a card never sets ArchivedAt, so a blocker only stops counting as active here.
        await NotifyResolvedDependenciesAsync(card, cmd.ProjectId, cmd.ActorId, ct);

        await _auditLogWriter.WriteAsync(
            new AuditLogRequest(
                cmd.ActorId,
                AuditLogScope.Project,
                "Card",
                card.Id,
                "Archived",
                cmd.ProjectId,
                AuditSnapshot.Serialize(oldSnapshot),
                AuditSnapshot.Serialize(BuildSnapshot(card))
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
        if (
            !await MembershipGuard.HasAccessAsync(
                _userRepo,
                _memberRepo,
                cmd.ProjectId,
                cmd.ActorId,
                ct
            )
        )
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
        var oldSnapshot = BuildSnapshot(card);

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
                AuditSnapshot.Serialize(oldSnapshot),
                AuditSnapshot.Serialize(BuildSnapshot(card))
            ),
            ct
        );

        await PublishAsync(cmd.ProjectId, BoardEntityType.Card, card.Id, BoardAction.Restored, ct);

        return Result<CardDto>.Success(await MapToDtoAsync(card, ct));
    }

    public async Task<Result> DeleteAsync(DeleteCardCommand cmd, CancellationToken ct = default)
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
            return Result.Failure(
                new Error(DomainErrorCodes.Projects.MembershipDenied, "Access denied.")
            );

        var card = await _cardRepo.GetByIdAsync(cmd.CardId, ct);
        if (card == null || card.ProjectId != cmd.ProjectId)
            return Result.Failure(new Error(DomainErrorCodes.Cards.NotFound, "Card not found."));

        var oldPosition = card.Position;
        var oldColumnId = card.ColumnId;
        var oldSnapshot = BuildSnapshot(card);

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
                AuditSnapshot.Serialize(oldSnapshot),
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

        var usersById =
            allUserIds.Count > 0
                ? await _userRepo.FindByIdsAsync(allUserIds, ct)
                : new Dictionary<Guid, Domain.Entities.Auth.User>();

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

        var relationships = await _relationshipRepo.ListByCardAsync(card.Id, ct);

        var relatedCardIds = relationships
            .Select(r => r.SourceCardId == card.Id ? r.TargetCardId : r.SourceCardId)
            .Distinct()
            .ToList();
        var relatedCardsById =
            relatedCardIds.Count > 0
                ? await _cardRepo.GetByIdsAsync(relatedCardIds, ct)
                : new Dictionary<Guid, Card>();

        // An archived blocker/predecessor no longer applies — MoveAsync already treats
        // it as resolved, so the badge and count must agree instead of only reappearing
        // here (this path fetches related cards unfiltered) while the board list (which
        // only ever sees active cards) silently drops it.
        var activeRelationships = relationships
            .Where(r =>
            {
                var otherId = r.SourceCardId == card.Id ? r.TargetCardId : r.SourceCardId;
                return relatedCardsById.TryGetValue(otherId, out var other)
                    && other.ArchivedAt == null;
            })
            .ToList();

        var relationshipBadges = BuildRelationshipBadges(
            card.Id,
            activeRelationships,
            relatedCardsById
        );

        ParentCardSummaryDto? parentCard = null;
        if (card.ParentCardId.HasValue)
        {
            var parent = await _cardRepo.GetByIdAsync(card.ParentCardId.Value, ct);
            if (parent != null)
                parentCard = new ParentCardSummaryDto(
                    parent.Id,
                    parent.CardNumber,
                    parent.Title,
                    parent.Type
                );
        }
        var childCount = await _cardRepo.CountActiveChildrenAsync(card.Id, ct);

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
            watcherDtos,
            relationshipBadges,
            activeRelationships.Count,
            parentCard,
            childCount
        );
    }

    // Ordering mirrors the TUI's CardRelationshipIndicatorHelper.TypeOrder — blocking
    // relationships surface first, informational ones last. Capped so a heavily-linked
    // card doesn't blow up the board payload; RelationshipCount on the DTO carries the
    // true total for a "+N more" indicator.
    private const int MaxRelationshipBadges = 5;

    private static readonly Dictionary<RelationshipType, int> RelationshipTypeOrder = new()
    {
        [RelationshipType.BlockedBy] = 0,
        [RelationshipType.Precedes] = 1,
        [RelationshipType.SpawnedFrom] = 2,
        [RelationshipType.Relates] = 3,
    };

    private static List<CardRelationshipBadgeDto> BuildRelationshipBadges(
        Guid cardId,
        IReadOnlyList<CardRelationship> relationships,
        IReadOnlyDictionary<Guid, Card> relatedCardsById
    )
    {
        var badges = new List<CardRelationshipBadgeDto>();
        foreach (var rel in relationships)
        {
            var isSource = rel.SourceCardId == cardId;
            var otherId = isSource ? rel.TargetCardId : rel.SourceCardId;
            if (!relatedCardsById.TryGetValue(otherId, out var otherCard))
                continue;

            badges.Add(
                new CardRelationshipBadgeDto(
                    otherCard.Id,
                    otherCard.CardNumber,
                    otherCard.Title,
                    rel.Type,
                    isSource
                )
            );
        }

        return [.. badges.OrderBy(b => RelationshipTypeOrder[b.Type]).Take(MaxRelationshipBadges)];
    }

    private static CardDto MapCardToDto(
        Card card,
        ILookup<Guid, CardAssignee> assigneeLookup,
        ILookup<Guid, CardWatcher> watcherLookup,
        IReadOnlyDictionary<Guid, Domain.Entities.Auth.User> usersById,
        IReadOnlyDictionary<Guid, Card> cardsById,
        Dictionary<Guid, int> relationshipCounts,
        ILookup<Guid, CardRelationship> relationshipsByCard,
        Dictionary<Guid, int> childCountByParentId
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

        var relationshipCount = relationshipCounts.TryGetValue(card.Id, out var count) ? count : 0;
        var relationshipBadges = BuildRelationshipBadges(
            card.Id,
            [.. relationshipsByCard[card.Id]],
            cardsById
        );

        ParentCardSummaryDto? parentCard = null;
        if (
            card.ParentCardId.HasValue
            && cardsById.TryGetValue(card.ParentCardId.Value, out var parent)
        )
            parentCard = new ParentCardSummaryDto(
                parent.Id,
                parent.CardNumber,
                parent.Title,
                parent.Type
            );
        var childCount = childCountByParentId.TryGetValue(card.Id, out var children) ? children : 0;

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
            watcherDtos,
            relationshipBadges,
            relationshipCount,
            parentCard,
            childCount
        );
    }

    private async Task NotifyResolvedDependenciesAsync(
        Card archivedCard,
        Guid projectId,
        Guid actorId,
        CancellationToken ct
    )
    {
        var relationships = await _relationshipRepo.ListActiveByCardAsync(archivedCard.Id, ct);
        var blockedByRels = relationships.Where(r =>
            r.Type == RelationshipType.BlockedBy && r.SourceCardId == archivedCard.Id
        );

        // Single query for all cards archivedCard blocks
        var blockedCardIds = blockedByRels.Select(r => r.TargetCardId).Distinct().ToList();
        if (blockedCardIds.Count == 0)
            return;

        var blockedCardsById = await _cardRepo.GetByIdsAsync(blockedCardIds, ct);
        var activeBlockedCards = blockedCardsById.Values.Where(c => c.ArchivedAt == null).ToList();
        if (activeBlockedCards.Count == 0)
            return;

        // Check remaining blockers for all blocked cards in one query
        var allRemainingBlockers = await _relationshipRepo.ListBlockersForCardsAsync(
            [.. activeBlockedCards.Select(c => c.Id)],
            ct
        );

        // Blocker cards are a DIFFERENT set than blockedCardsById (which only holds cards
        // archivedCard itself blocks) — a blocked card can have OTHER active blockers too,
        // so their archived state has to come from its own batch fetch, not be looked up
        // against blockedCardsById (that was the bug: an unrelated still-active blocker
        // silently failed the TryGetValue and got treated as already resolved).
        var blockerCardIds = allRemainingBlockers.Select(r => r.SourceCardId).Distinct().ToList();
        var blockerCardsById =
            blockerCardIds.Count > 0
                ? await _cardRepo.GetByIdsAsync(blockerCardIds, ct)
                : new Dictionary<Guid, Card>();

        var assigneesByCard = await _assigneeRepo.ListByCardIdsAsync(
            [.. activeBlockedCards.Select(c => c.Id)],
            ct
        );

        var requests = new List<NotifyRequest>();
        foreach (var blockedCard in activeBlockedCards)
        {
            var hasActiveBlockers = allRemainingBlockers
                .Where(r => r.TargetCardId == blockedCard.Id)
                .Any(r =>
                    blockerCardsById.TryGetValue(r.SourceCardId, out var bc)
                    && bc.ArchivedAt == null
                );

            if (hasActiveBlockers)
                continue;

            foreach (var assignee in assigneesByCard[blockedCard.Id])
            {
                if (assignee.UserId == actorId)
                    continue;
                requests.Add(
                    new NotifyRequest(
                        assignee.UserId,
                        actorId,
                        $"#{blockedCard.CardNumber} is no longer blocked",
                        $"All blocking cards for #{blockedCard.CardNumber} have been resolved.",
                        null,
                        blockedCard.Id,
                        projectId,
                        $"/projects/{projectId}/board?card={blockedCard.Id}"
                    )
                );
            }
        }

        if (requests.Count == 0)
            return;

        try
        {
            await _notifService.NotifyBatchAsync(requests, ct);
        }
        catch (Exception ex)
        {
            _warnLogger.LogWarning($"Failed to send unblock notifications: {ex.Message}");
        }
    }
}
