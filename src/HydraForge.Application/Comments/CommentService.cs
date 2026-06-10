using HydraForge.Application.Audit;
using HydraForge.Application.Auth;
using HydraForge.Application.Cards;
using HydraForge.Application.Projects;
using HydraForge.Domain.Common;
using HydraForge.Domain.Entities.ProjectSpace;
using HydraForge.Domain.Enums;

namespace HydraForge.Application.Comments;

public class CommentService(
    ICommentRepository commentRepo,
    ICardWatcherRepository watcherRepo,
    ICardRepository cardRepo,
    IProjectMemberRepository memberRepo,
    IUserRepository userRepo,
    IAuditLogWriter auditLogWriter
)
{
    private readonly ICommentRepository _commentRepo = commentRepo;
    private readonly ICardWatcherRepository _watcherRepo = watcherRepo;
    private readonly ICardRepository _cardRepo = cardRepo;
    private readonly IProjectMemberRepository _memberRepo = memberRepo;
    private readonly IUserRepository _userRepo = userRepo;
    private readonly IAuditLogWriter _auditLogWriter = auditLogWriter;

    public async Task<Result<CommentDto>> CreateAsync(
        CreateCommentCommand cmd,
        CancellationToken ct = default
    )
    {
        var membership = await _memberRepo.GetByProjectAndUserAsync(cmd.ProjectId, cmd.ActorId, ct);
        if (membership == null)
            return Result<CommentDto>.Failure(
                new Error(DomainErrorCodes.Projects.MembershipDenied, "Access denied.")
            );

        var card = await _cardRepo.GetByIdAsync(cmd.CardId, ct);
        if (card == null || card.ProjectId != cmd.ProjectId)
            return Result<CommentDto>.Failure(
                new Error(DomainErrorCodes.Cards.NotFound, "Card not found.")
            );

        var mentionedUsernames = MentionExtractor.Extract(cmd.Content);
        var mentionedUserIds = new List<Guid>();

        foreach (var username in mentionedUsernames)
        {
            var user = await _userRepo.FindByUsernameAsync(username);
            if (user == null || user.IsDisabled)
                continue;

            var member = await _memberRepo.GetByProjectAndUserAsync(cmd.ProjectId, user.Id, ct);
            if (member == null)
                continue;

            mentionedUserIds.Add(user.Id);
        }

        var authorUser = await _userRepo.FindByIdAsync(cmd.ActorId, ct);

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

        var existingWatcher = await _watcherRepo.GetByCardAndUserAsync(cmd.CardId, cmd.ActorId, ct);
        if (existingWatcher == null)
        {
            var watcher = new CardWatcher
            {
                CardId = cmd.CardId,
                UserId = cmd.ActorId,
                AddedAt = DateTime.UtcNow,
            };
            await _watcherRepo.AddAsync(watcher, ct);
        }

        await _auditLogWriter.WriteAsync(
            new AuditLogRequest(
                cmd.ActorId,
                AuditLogScope.Project,
                "Comment",
                comment.Id,
                "Created",
                cmd.ProjectId,
                null,
                null
            ),
            ct
        );

        return Result<CommentDto>.Success(
            new CommentDto(
                comment.Id,
                comment.CardId,
                comment.AuthorId,
                authorUser?.Username ?? string.Empty,
                comment.Content,
                comment.CreatedAt,
                comment.UpdatedAt,
                comment.ArchivedAt,
                mentionedUserIds
            )
        );
    }

    public async Task<Result<CommentDto>> UpdateAsync(
        UpdateCommentCommand cmd,
        CancellationToken ct = default
    )
    {
        var membership = await _memberRepo.GetByProjectAndUserAsync(cmd.ProjectId, cmd.ActorId, ct);
        if (membership == null)
            return Result<CommentDto>.Failure(
                new Error(DomainErrorCodes.Projects.MembershipDenied, "Access denied.")
            );

        var card = await _cardRepo.GetByIdAsync(cmd.CardId, ct);
        if (card == null || card.ProjectId != cmd.ProjectId)
            return Result<CommentDto>.Failure(
                new Error(DomainErrorCodes.Cards.NotFound, "Card not found.")
            );

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
        var mentionedUserIds = new List<Guid>();

        foreach (var username in mentionedUsernames)
        {
            var user = await _userRepo.FindByUsernameAsync(username);
            if (user == null || user.IsDisabled)
                continue;

            var member = await _memberRepo.GetByProjectAndUserAsync(cmd.ProjectId, user.Id, ct);
            if (member == null)
                continue;

            mentionedUserIds.Add(user.Id);
        }

        comment.Content = cmd.Content;
        comment.UpdatedAt = DateTime.UtcNow;
        await _commentRepo.UpdateAsync(comment, ct);

        var authorUser = await _userRepo.FindByIdAsync(cmd.ActorId, ct);

        await _auditLogWriter.WriteAsync(
            new AuditLogRequest(
                cmd.ActorId,
                AuditLogScope.Project,
                "Comment",
                comment.Id,
                "Updated",
                cmd.ProjectId,
                null,
                null
            ),
            ct
        );

        return Result<CommentDto>.Success(
            new CommentDto(
                comment.Id,
                comment.CardId,
                comment.AuthorId,
                authorUser?.Username ?? string.Empty,
                comment.Content,
                comment.CreatedAt,
                comment.UpdatedAt,
                comment.ArchivedAt,
                mentionedUserIds
            )
        );
    }

    public async Task<Result<CommentDto>> ArchiveAsync(
        ArchiveCommentCommand cmd,
        CancellationToken ct = default
    )
    {
        var membership = await _memberRepo.GetByProjectAndUserAsync(cmd.ProjectId, cmd.ActorId, ct);
        if (membership == null)
            return Result<CommentDto>.Failure(
                new Error(DomainErrorCodes.Projects.MembershipDenied, "Access denied.")
            );

        var card = await _cardRepo.GetByIdAsync(cmd.CardId, ct);
        if (card == null || card.ProjectId != cmd.ProjectId)
            return Result<CommentDto>.Failure(
                new Error(DomainErrorCodes.Cards.NotFound, "Card not found.")
            );

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

        var authorUser = await _userRepo.FindByIdAsync(cmd.ActorId, ct);

        await _auditLogWriter.WriteAsync(
            new AuditLogRequest(
                cmd.ActorId,
                AuditLogScope.Project,
                "Comment",
                comment.Id,
                "Archived",
                cmd.ProjectId,
                null,
                null
            ),
            ct
        );

        return Result<CommentDto>.Success(
            new CommentDto(
                comment.Id,
                comment.CardId,
                comment.AuthorId,
                authorUser?.Username ?? string.Empty,
                comment.Content,
                comment.CreatedAt,
                comment.UpdatedAt,
                comment.ArchivedAt,
                []
            )
        );
    }

    public async Task<Result<IReadOnlyList<CommentDto>>> ListAsync(
        Guid projectId,
        Guid cardId,
        Guid actorId,
        CancellationToken ct = default
    )
    {
        var membership = await _memberRepo.GetByProjectAndUserAsync(projectId, actorId, ct);
        if (membership == null)
            return Result<IReadOnlyList<CommentDto>>.Failure(
                new Error(DomainErrorCodes.Projects.MembershipDenied, "Access denied.")
            );

        var card = await _cardRepo.GetByIdAsync(cardId, ct);
        if (card == null || card.ProjectId != projectId)
            return Result<IReadOnlyList<CommentDto>>.Failure(
                new Error(DomainErrorCodes.Cards.NotFound, "Card not found.")
            );

        var comments = await _commentRepo.ListByCardAsync(cardId, ct);
        var dtos = new List<CommentDto>();

        foreach (var comment in comments)
        {
            var authorUser = await _userRepo.FindByIdAsync(comment.AuthorId, ct);
            var mentionedUsernames = MentionExtractor.Extract(comment.Content);
            var mentionedUserIds = new List<Guid>();

            foreach (var username in mentionedUsernames)
            {
                var user = await _userRepo.FindByUsernameAsync(username);
                if (user == null || user.IsDisabled)
                    continue;

                var member = await _memberRepo.GetByProjectAndUserAsync(projectId, user.Id, ct);
                if (member == null)
                    continue;

                mentionedUserIds.Add(user.Id);
            }

            dtos.Add(new CommentDto(
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

        return Result<IReadOnlyList<CommentDto>>.Success(dtos);
    }
}