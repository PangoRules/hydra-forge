using HydraForge.Application.Audit;
using HydraForge.Application.Auth;
using HydraForge.Application.Cards;
using HydraForge.Application.ProjectSnapshots;
using HydraForge.Application.Projects;
using HydraForge.Domain.Common;
using HydraForge.Domain.Entities.Auth;
using HydraForge.Domain.Entities.ProjectSpace;
using HydraForge.Domain.Enums;

namespace HydraForge.Application.Comments;

public class CommentService(
    ICommentRepository commentRepo,
    ICardWatcherRepository watcherRepo,
    ICardRepository cardRepo,
    IProjectMemberRepository memberRepo,
    IUserRepository userRepo,
    IAuditLogWriter auditLogWriter,
    IProjectSnapshotRefresher snapshotRefresher
)
{
    private readonly ICommentRepository _commentRepo = commentRepo;
    private readonly ICardWatcherRepository _watcherRepo = watcherRepo;
    private readonly ICardRepository _cardRepo = cardRepo;
    private readonly IProjectMemberRepository _memberRepo = memberRepo;
    private readonly IUserRepository _userRepo = userRepo;
    private readonly IAuditLogWriter _auditLogWriter = auditLogWriter;
    private readonly IProjectSnapshotRefresher _snapshotRefresher = snapshotRefresher;

    // ── Shared helpers ──────────────────────────────────────

    private async Task<Result<(ProjectMember, Card)>> ValidateMembershipAndCardAsync(
        Guid projectId, Guid userId, Guid cardId, CancellationToken ct
    )
    {
        var membership = await _memberRepo.GetByProjectAndUserAsync(projectId, userId, ct);
        if (membership == null)
            return Result<(ProjectMember, Card)>.Failure(
                new Error(DomainErrorCodes.Projects.MembershipDenied, "Access denied.")
            );

        var card = await _cardRepo.GetByIdAsync(cardId, ct);
        if (card == null || card.ProjectId != projectId)
            return Result<(ProjectMember, Card)>.Failure(
                new Error(DomainErrorCodes.Cards.NotFound, "Card not found.")
            );

        return Result<(ProjectMember, Card)>.Success((membership, card));
    }

    /// Batches mention resolution into 2 queries total:
    ///   1. FindByUsernamesAsync — all mentioned users in 1 query
    ///   2. ListMembersAsync — all project members in 1 query
    ///   Cross-references in memory to filter disabled/non-member.
    private async Task<List<Guid>> ResolveMentionsAsync(
        Guid projectId, IReadOnlyList<string> usernames, CancellationToken ct
    )
    {
        if (usernames.Count == 0)
            return [];

        var usersByUsername = await _userRepo.FindByUsernamesAsync(usernames, ct);
        var candidateIds = usersByUsername.Values
            .Where(u => !u.IsDisabled)
            .Select(u => u.Id)
            .ToList();

        if (candidateIds.Count == 0)
            return [];

        var members = await _memberRepo.ListMembersAsync(projectId, ct);
        var memberUserIds = members.Select(m => m.UserId).ToHashSet();

        return candidateIds.Where(id => memberUserIds.Contains(id)).ToList();
    }

    private async Task<Result<CommentDto>> BuildCommentDtoResultAsync(
        Comment comment, Guid authorId, List<Guid> mentionedUserIds, CancellationToken ct
    )
    {
        var authorUser = await _userRepo.FindByIdAsync(authorId, ct);
        return Result<CommentDto>.Success(new CommentDto(
            comment.Id,
            comment.CardId,
            comment.AuthorId,
            authorUser?.Username ?? string.Empty,
            comment.Content,
            comment.CreatedAt,
            comment.UpdatedAt,
            comment.ArchivedAt,
            mentionedUserIds
        ));
    }

    private async Task EnsureWatcherAsync(Guid cardId, Guid userId, CancellationToken ct)
    {
        var existing = await _watcherRepo.GetByCardAndUserAsync(cardId, userId, ct);
        if (existing == null)
        {
            await _watcherRepo.AddAsync(new CardWatcher
            {
                CardId = cardId,
                UserId = userId,
                AddedAt = DateTime.UtcNow,
            }, ct);
        }
    }

    private async Task WriteAuditAsync(Guid actorId, Guid commentId, Guid projectId, string action, CancellationToken ct)
    {
        await _auditLogWriter.WriteAsync(
            new AuditLogRequest(actorId, AuditLogScope.Project, "Comment", commentId, action, projectId, null, null),
            ct
        );
    }

    // ── Commands ────────────────────────────────────────────

    public async Task<Result<CommentDto>> CreateAsync(
        CreateCommentCommand cmd,
        CancellationToken ct = default
    )
    {
        var validation = await ValidateMembershipAndCardAsync(cmd.ProjectId, cmd.ActorId, cmd.CardId, ct);
        if (validation.IsFailure)
            return Result<CommentDto>.Failure(validation.Error);

        var mentionedUsernames = MentionExtractor.Extract(cmd.Content);
        var mentionedUserIds = await ResolveMentionsAsync(cmd.ProjectId, mentionedUsernames, ct);

        var comment = new Comment
        {
            Id = Guid.NewGuid(),
            CardId = cmd.CardId,
            AuthorId = cmd.ActorId,
            Content = cmd.Content,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        await _commentRepo.AddAsync(comment, ct);
        await EnsureWatcherAsync(cmd.CardId, cmd.ActorId, ct);
        await WriteAuditAsync(cmd.ActorId, comment.Id, cmd.ProjectId, "Created", ct);
        await _snapshotRefresher.RefreshAsync(cmd.ProjectId, ct);

        return await BuildCommentDtoResultAsync(comment, cmd.ActorId, mentionedUserIds, ct);
    }

    public async Task<Result<CommentDto>> UpdateAsync(
        UpdateCommentCommand cmd,
        CancellationToken ct = default
    )
    {
        var validation = await ValidateMembershipAndCardAsync(cmd.ProjectId, cmd.ActorId, cmd.CardId, ct);
        if (validation.IsFailure)
            return Result<CommentDto>.Failure(validation.Error);

        var comment = await _commentRepo.GetByIdAsync(cmd.CommentId, ct);
        if (comment == null || comment.CardId != cmd.CardId)
            return Result<CommentDto>.Failure(
                new Error(DomainErrorCodes.Comments.NotFound, "Comment not found.")
            );

        if (comment.ArchivedAt != null)
            return Result<CommentDto>.Failure(
                new Error(DomainErrorCodes.Comments.Archived, "Comment is archived.")
            );

        var mentionedUsernames = MentionExtractor.Extract(cmd.Content);
        var mentionedUserIds = await ResolveMentionsAsync(cmd.ProjectId, mentionedUsernames, ct);

        comment.Content = cmd.Content;
        comment.UpdatedAt = DateTime.UtcNow;
        await _commentRepo.UpdateAsync(comment, ct);
        await WriteAuditAsync(cmd.ActorId, comment.Id, cmd.ProjectId, "Updated", ct);
        await _snapshotRefresher.RefreshAsync(cmd.ProjectId, ct);

        return await BuildCommentDtoResultAsync(comment, cmd.ActorId, mentionedUserIds, ct);
    }

    public async Task<Result<CommentDto>> ArchiveAsync(
        ArchiveCommentCommand cmd,
        CancellationToken ct = default
    )
    {
        var validation = await ValidateMembershipAndCardAsync(cmd.ProjectId, cmd.ActorId, cmd.CardId, ct);
        if (validation.IsFailure)
            return Result<CommentDto>.Failure(validation.Error);

        var comment = await _commentRepo.GetByIdAsync(cmd.CommentId, ct);
        if (comment == null || comment.CardId != cmd.CardId)
            return Result<CommentDto>.Failure(
                new Error(DomainErrorCodes.Comments.NotFound, "Comment not found.")
            );

        if (comment.ArchivedAt != null)
            return Result<CommentDto>.Failure(
                new Error(DomainErrorCodes.Comments.Archived, "Comment is already archived.")
            );

        comment.ArchivedAt = DateTime.UtcNow;
        await _commentRepo.UpdateAsync(comment, ct);
        await WriteAuditAsync(cmd.ActorId, comment.Id, cmd.ProjectId, "Archived", ct);
        await _snapshotRefresher.RefreshAsync(cmd.ProjectId, ct);

        return await BuildCommentDtoResultAsync(comment, cmd.ActorId, [], ct);
    }

    public async Task<Result<IReadOnlyList<CommentDto>>> ListAsync(
        Guid projectId,
        Guid cardId,
        Guid actorId,
        CancellationToken ct = default
    )
    {
        var validation = await ValidateMembershipAndCardAsync(projectId, actorId, cardId, ct);
        if (validation.IsFailure)
            return Result<IReadOnlyList<CommentDto>>.Failure(validation.Error);

        var comments = (await _commentRepo.ListByCardAsync(cardId, ct))
            .Where(c => c.ArchivedAt == null)
            .ToList();

        // Preload all authors + members once
        var authorIds = comments.Select(c => c.AuthorId).Distinct().ToList();
        var authorsById = await _userRepo.FindByIdsAsync(authorIds, ct);
        var allMembers = await _memberRepo.ListMembersAsync(projectId, ct);
        var memberUserIds = allMembers.Select(m => m.UserId).ToHashSet();

        // Preload all mentioned usernames across all comments
        var allMentioned = comments
            .SelectMany(c => MentionExtractor.Extract(c.Content))
            .Distinct()
            .ToList();

        var usersByUsername = allMentioned.Count > 0
            ? await _userRepo.FindByUsernamesAsync(allMentioned, ct)
            : (IReadOnlyDictionary<string, User>)new Dictionary<string, User>(StringComparer.OrdinalIgnoreCase);

        var dtos = new List<CommentDto>(comments.Count);
        foreach (var comment in comments)
        {
            var mentioned = MentionExtractor.Extract(comment.Content);
            var mentionedIds = new List<Guid>(mentioned.Count);
            foreach (var username in mentioned)
            {
                if (usersByUsername.TryGetValue(username, out var user)
                    && !user.IsDisabled
                    && memberUserIds.Contains(user.Id))
                {
                    mentionedIds.Add(user.Id);
                }
            }

            authorsById.TryGetValue(comment.AuthorId, out var author);
            dtos.Add(new CommentDto(
                comment.Id,
                comment.CardId,
                comment.AuthorId,
                author?.Username ?? string.Empty,
                comment.Content,
                comment.CreatedAt,
                comment.UpdatedAt,
                comment.ArchivedAt,
                mentionedIds
            ));
        }

        return Result<IReadOnlyList<CommentDto>>.Success(dtos);
    }
}