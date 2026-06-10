namespace HydraForge.Application.Comments;

public record CreateCommentCommand(
    Guid ProjectId,
    Guid CardId,
    Guid ActorId,
    string Content
);

public record UpdateCommentCommand(
    Guid ProjectId,
    Guid CardId,
    Guid CommentId,
    Guid ActorId,
    string Content
);

public record ArchiveCommentCommand(
    Guid ProjectId,
    Guid CardId,
    Guid CommentId,
    Guid ActorId
);

public record CommentDto(
    Guid Id,
    Guid CardId,
    Guid AuthorId,
    string AuthorUsername,
    string Content,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? ArchivedAt,
    IReadOnlyList<Guid> MentionedUserIds
);

public record CommentResponse(
    Guid Id,
    Guid CardId,
    Guid AuthorId,
    string AuthorUsername,
    string Content,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? ArchivedAt,
    IReadOnlyList<Guid> MentionedUserIds
);

public record CreateCommentRequest(
    string Content
);

public record UpdateCommentRequest(
    string Content
);

public record CommentListResponse(
    IReadOnlyList<CommentResponse> Comments
);