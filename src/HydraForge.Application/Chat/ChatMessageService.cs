using HydraForge.Application.Auth;
using HydraForge.Application.Llm;
using HydraForge.Application.Projects;
using HydraForge.Domain.Common;
using HydraForge.Domain.Entities.Chat;
using HydraForge.Domain.Enums;

namespace HydraForge.Application.Chat;

public class ChatMessageService(
    IChatSessionRepository sessionRepo,
    IChatMessageRepository messageRepo,
    IUserRepository userRepo,
    IProjectMemberRepository memberRepo
) : IChatMessageService
{
    private readonly IChatSessionRepository _sessionRepo = sessionRepo;
    private readonly IChatMessageRepository _messageRepo = messageRepo;
    private readonly IUserRepository _userRepo = userRepo;
    private readonly IProjectMemberRepository _memberRepo = memberRepo;

    public async Task<Result<ChatMessageDto>> SendUserMessageAsync(
        Guid sessionId,
        Guid userId,
        string content,
        IReadOnlyList<ImageBlock>? images = null,
        CancellationToken ct = default
    )
    {
        var session = await _sessionRepo.GetByIdAsync(sessionId, ct);
        if (session == null)
            return Result<ChatMessageDto>.Failure(
                new Error(DomainErrorCodes.Chat.SessionNotFound, "Session not found.")
            );

        if (session.OwnerId != userId)
            return Result<ChatMessageDto>.Failure(
                new Error(
                    DomainErrorCodes.Chat.SessionNotOwner,
                    "Only the session owner can send messages."
                )
            );

        if (session.Status != ChatSessionStatus.Active)
            return Result<ChatMessageDto>.Failure(
                new Error(
                    DomainErrorCodes.Chat.SessionClosed,
                    "Cannot send a message to a closed session."
                )
            );

        var message = new Domain.Entities.Chat.ChatMessage
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            Role = MessageRole.User,
            Content = content,
            ImagesJson = ChatMessageMapper.ToDomainImagesJson(images),
            CreatedAt = DateTime.UtcNow,
        };

        await _messageRepo.AddAsync(message, ct);

        return Result<ChatMessageDto>.Success(MapToDto(message));
    }

    public async Task<Result<ChatMessagePageDto>> GetHistoryAsync(
        Guid sessionId,
        Guid actorId,
        DateTime? before = null,
        Guid? beforeId = null,
        int limit = 50,
        CancellationToken ct = default
    )
    {
        var session = await _sessionRepo.GetByIdAsync(sessionId, ct);
        if (session == null)
            return Result<ChatMessagePageDto>.Failure(
                new Error(DomainErrorCodes.Chat.SessionNotFound, "Session not found.")
            );

        if (!await CanReadSessionAsync(session, actorId, ct))
            return Result<ChatMessagePageDto>.Failure(
                new Error(DomainErrorCodes.Chat.SessionNotOwner, "Access denied.")
            );

        var messages = await _messageRepo.GetBySessionAsync(sessionId, before, beforeId, limit, ct);

        return Result<ChatMessagePageDto>.Success(
            new ChatMessagePageDto(messages.Select(MapToDto).ToList(), messages.Count)
        );
    }

    private async Task<bool> CanReadSessionAsync(
        ChatSession session,
        Guid actorId,
        CancellationToken ct
    )
    {
        if (session.OwnerId == actorId)
            return true;

        if (session.ProjectId.HasValue && session.IsShared)
            return await MembershipGuard.HasAccessAsync(
                _userRepo,
                _memberRepo,
                session.ProjectId.Value,
                actorId,
                ct
            );

        return false;
    }

    private static ChatMessageDto MapToDto(Domain.Entities.Chat.ChatMessage message) =>
        new(
            message.Id,
            message.SessionId,
            message.Role,
            message.Content,
            message.InputTokens,
            message.OutputTokens,
            message.CachedTokens,
            message.ModelName,
            message.ImagesJson,
            message.CreatedAt
        );
}
