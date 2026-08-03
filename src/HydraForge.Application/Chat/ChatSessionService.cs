using HydraForge.Application.Auth;
using HydraForge.Application.Cards;
using HydraForge.Application.Projects;
using HydraForge.Domain.Common;
using HydraForge.Domain.Entities.Chat;
using HydraForge.Domain.Entities.PersonalSpace;
using HydraForge.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HydraForge.Application.Chat;

public class ChatSessionService(
    IChatSessionRepository sessionRepo,
    IChatMessageRepository messageRepo,
    IChatSessionDocumentRepository sessionDocRepo,
    ICardRepository cardRepo,
    IUserRepository userRepo,
    IProjectMemberRepository memberRepo,
    IAgentPersonalityRepository personalityRepo,
    IDocumentRepository documentRepo,
    IChatSummaryGenerator summaryGenerator,
    IBackgroundTaskQueue backgroundTaskQueue,
    ILogger<ChatSessionService> logger
) : IChatSessionService
{
    private readonly IChatSessionRepository _sessionRepo = sessionRepo;
    private readonly IChatMessageRepository _messageRepo = messageRepo;
    private readonly IChatSessionDocumentRepository _sessionDocRepo = sessionDocRepo;
    private readonly ICardRepository _cardRepo = cardRepo;
    private readonly IUserRepository _userRepo = userRepo;
    private readonly IProjectMemberRepository _memberRepo = memberRepo;
    private readonly IAgentPersonalityRepository _personalityRepo = personalityRepo;
    private readonly IDocumentRepository _documentRepo = documentRepo;
    private readonly IChatSummaryGenerator _summaryGenerator = summaryGenerator;
    private readonly IBackgroundTaskQueue _backgroundTaskQueue = backgroundTaskQueue;
    private readonly ILogger<ChatSessionService> _logger = logger;

    public async Task<Result<ChatSessionDto>> CreateAsync(
        CreateChatSessionRequest request,
        Guid actorId,
        CancellationToken ct = default
    )
    {
        if (request.ProjectId.HasValue)
        {
            if (
                !await MembershipGuard.HasAccessAsync(
                    _userRepo,
                    _memberRepo,
                    request.ProjectId.Value,
                    actorId,
                    ct
                )
            )
                return Result<ChatSessionDto>.Failure(
                    new Error(DomainErrorCodes.Projects.MembershipDenied, "Access denied.")
                );
        }

        if (request.OpenCardId.HasValue)
        {
            if (!request.ProjectId.HasValue)
                return Result<ChatSessionDto>.Failure(
                    new Error(
                        DomainErrorCodes.Chat.CardNotInProject,
                        "OpenCardId requires ProjectId."
                    )
                );

            var card = await _cardRepo.GetByIdAsync(request.OpenCardId.Value, ct);
            if (card == null)
                return Result<ChatSessionDto>.Failure(
                    new Error(DomainErrorCodes.Cards.NotFound, "Card not found.")
                );
            if (card.ProjectId != request.ProjectId.Value)
                return Result<ChatSessionDto>.Failure(
                    new Error(
                        DomainErrorCodes.Chat.CardNotInProject,
                        "Card is in a different project."
                    )
                );
        }

        // Fork path: mutually exclusive with F6 panel tuple
        if (request.ForkedFromSessionId.HasValue)
            return await CreateForkedAsync(request, actorId, (Guid)request.ForkedFromSessionId, ct);

        // F6: implicit close of existing active panel session
        if (request.ProjectId.HasValue && request.OpenCardId.HasValue)
        {
            var existing = await _sessionRepo.GetActiveByPanelAsync(
                request.ProjectId.Value,
                request.OpenCardId.Value,
                actorId,
                ct
            );
            if (existing != null)
            {
                // Enqueue background close of old session (summary + CardChatLink, one LLM call);
                // the new session is returned immediately without waiting for it to run.
                await _backgroundTaskQueue.EnqueueJobAsync<CloseSessionJob>(j =>
                    j.RunAsync(existing.Id, actorId, CancellationToken.None)
                );
            }
        }

        var session = new ChatSession
        {
            Id = Guid.NewGuid(),
            OwnerId = actorId,
            Title = request.Title,
            FolderId = request.FolderId,
            ProjectId = request.ProjectId,
            OpenCardId = request.OpenCardId,
            PersonalityId = request.PersonalityId,
            AiEditMode = request.AiEditMode ?? AiEditMode.PerMutation,
            SearchAllMyDocs = request.SearchAllMyDocs,
            Status = ChatSessionStatus.Active,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        await _sessionRepo.AddAsync(session, ct);

        return Result<ChatSessionDto>.Success(await MapToDtoAsync(session, ct));
    }

    private async Task<Result<ChatSessionDto>> CreateForkedAsync(
        CreateChatSessionRequest request,
        Guid actorId,
        Guid forkedId,
        CancellationToken ct
    )
    {
        var source = await _sessionRepo.GetByIdAsync(forkedId, ct);
        if (source == null)
            return Result<ChatSessionDto>.Failure(
                new Error(DomainErrorCodes.Chat.SessionNotFound, "Source session not found.")
            );

        // Caller must be able to read the source session
        if (!source.ProjectId.HasValue || !source.IsShared)
            return Result<ChatSessionDto>.Failure(
                new Error(
                    DomainErrorCodes.Chat.SessionNotOwner,
                    "Cannot fork: only shared project sessions can be forked."
                )
            );

        if (
            !await MembershipGuard.HasAccessAsync(
                _userRepo,
                _memberRepo,
                source.ProjectId.Value,
                actorId,
                ct
            )
        )
            return Result<ChatSessionDto>.Failure(
                new Error(DomainErrorCodes.Projects.MembershipDenied, "Access denied.")
            );

        // Generate summary of source session (one LLM call)
        var summaryResult = await _summaryGenerator.GenerateSummaryAsync(source.Id, ct);
        if (!summaryResult.IsSuccess)
            return Result<ChatSessionDto>.Failure(
                new Error(
                    DomainErrorCodes.Chat.SummaryFailed,
                    "Failed to generate session summary."
                )
            );
        var summaryText = summaryResult.Value;

        var forked = new ChatSession
        {
            Id = Guid.NewGuid(),
            OwnerId = actorId,
            Title = request.Title,
            FolderId = request.FolderId,
            ProjectId = source.ProjectId,
            IsShared = false,
            OpenCardId = null, // Forked sessions don't have an open card
            PersonalityId = request.PersonalityId,
            AiEditMode = request.AiEditMode ?? AiEditMode.PerMutation,
            SearchAllMyDocs = request.SearchAllMyDocs,
            Status = ChatSessionStatus.Active,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        await _sessionRepo.AddAsync(forked, ct);

        // Pre-populate first message with summary
        if (!string.IsNullOrWhiteSpace(summaryText))
        {
            var summaryMessage = new ChatMessage
            {
                Id = Guid.NewGuid(),
                SessionId = forked.Id,
                Role = MessageRole.Assistant,
                Content = $"[Session summary]\n{summaryText}",
                CreatedAt = DateTime.UtcNow,
            };
            await _messageRepo.AddAsync(summaryMessage, ct);
        }

        return Result<ChatSessionDto>.Success(await MapToDtoAsync(forked, ct));
    }

    public async Task<Result<ChatSessionDetailDto>> GetAsync(
        Guid sessionId,
        Guid actorId,
        CancellationToken ct = default
    )
    {
        var session = await _sessionRepo.GetByIdAsync(sessionId, ct);
        if (session == null)
            return Result<ChatSessionDetailDto>.Failure(
                new Error(DomainErrorCodes.Chat.SessionNotFound, "Session not found.")
            );

        if (!await CanReadSessionAsync(session, actorId, ct))
            return Result<ChatSessionDetailDto>.Failure(
                new Error(DomainErrorCodes.Chat.SessionNotOwner, "Access denied.")
            );

        var messages = await _messageRepo.GetBySessionAsync(
            sessionId,
            before: null,
            beforeId: null,
            limit: 1000,
            ct
        );

        return Result<ChatSessionDetailDto>.Success(
            new ChatSessionDetailDto(
                session.Id,
                session.Title,
                session.FolderId,
                session.OwnerId,
                session.ProjectId,
                session.OpenCardId,
                session.PersonalityId,
                await GetPersonalityArchivedAsync(session.PersonalityId, ct),
                session.IsShared,
                session.Status,
                session.AiEditMode,
                session.SearchAllMyDocs,
                session.Summary,
                session.CreatedAt,
                session.UpdatedAt,
                session.ArchivedAt,
                session.ClosedAt,
                messages.Select(MapMessageToDto).ToList()
            )
        );
    }

    public async Task<Result<ChatSessionPageDto>> ListAsync(
        Guid actorId,
        Guid? folderId,
        Guid? projectId,
        DateTime? before,
        int limit,
        CancellationToken ct = default
    )
    {
        var sessions = await _sessionRepo.ListAsync(
            actorId,
            folderId,
            projectId,
            before,
            limit,
            ct
        );
        var dtos = new List<ChatSessionDto>();
        foreach (var session in sessions)
            dtos.Add(await MapToDtoAsync(session, ct));

        return Result<ChatSessionPageDto>.Success(new ChatSessionPageDto(dtos, dtos.Count));
    }

    public async Task<Result<ChatSessionDto>> UpdateAsync(
        Guid sessionId,
        UpdateChatSessionRequest request,
        Guid actorId,
        CancellationToken ct = default
    )
    {
        var session = await _sessionRepo.GetByIdAsync(sessionId, ct);
        if (session == null)
            return Result<ChatSessionDto>.Failure(
                new Error(DomainErrorCodes.Chat.SessionNotFound, "Session not found.")
            );

        if (session.OwnerId != actorId)
            return Result<ChatSessionDto>.Failure(
                new Error(
                    DomainErrorCodes.Chat.SessionNotOwner,
                    "Only the owner can update the session."
                )
            );

        if (session.Status != ChatSessionStatus.Active)
            return Result<ChatSessionDto>.Failure(
                new Error(DomainErrorCodes.Chat.SessionClosed, "Cannot update a closed session.")
            );

        session.UpdateSettings(
            request.Title,
            request.FolderId,
            request.PersonalityId,
            request.AiEditMode,
            request.SearchAllMyDocs
        );

        await _sessionRepo.UpdateAsync(session, ct);

        return Result<ChatSessionDto>.Success(await MapToDtoAsync(session, ct));
    }

    public async Task<Result<ChatSessionDto>> CloseAsync(
        Guid sessionId,
        Guid actorId,
        CancellationToken ct = default
    )
    {
        var session = await _sessionRepo.GetByIdAsync(sessionId, ct);
        if (session == null)
            return Result<ChatSessionDto>.Failure(
                new Error(DomainErrorCodes.Chat.SessionNotFound, "Session not found.")
            );

        if (session.OwnerId != actorId)
            return Result<ChatSessionDto>.Failure(
                new Error(
                    DomainErrorCodes.Chat.SessionNotOwner,
                    "Only the owner can close the session."
                )
            );

        // Idempotent: already closed
        if (session.Status == ChatSessionStatus.Closed)
            return Result<ChatSessionDto>.Success(await MapToDtoAsync(session, ct));

        // Empty session → close without summary or CardChatLink
        var messages = await _messageRepo.GetBySessionAsync(sessionId, before: null, beforeId: null, limit: 1, ct);
        string? summary = null;

        if (messages.Count > 0)
        {
            var summaryResult = await _summaryGenerator.GenerateSummaryAsync(sessionId, ct);
            if (summaryResult.IsSuccess)
                summary = summaryResult.Value;
        }

        session.Close(summary);
        await _sessionRepo.UpdateAsync(session, ct);

        // Create CardChatLink if panel session
        if (
            session.ProjectId.HasValue
            && session.OpenCardId.HasValue
            && !string.IsNullOrWhiteSpace(summary)
        )
        {
            var link = new CardChatLink
            {
                Id = Guid.NewGuid(),
                CardId = session.OpenCardId.Value,
                ChatSessionId = session.Id,
                OwnerId = actorId,
                Summary = summary,
                CreatedAt = DateTime.UtcNow,
            };
            await _sessionRepo.AddCardChatLinkAsync(link, ct);
        }

        return Result<ChatSessionDto>.Success(await MapToDtoAsync(session, ct));
    }

    public async Task<Result<ChatSessionDto>> ArchiveAsync(
        Guid sessionId,
        Guid actorId,
        CancellationToken ct = default
    )
    {
        var session = await _sessionRepo.GetByIdAsync(sessionId, ct);
        if (session == null)
            return Result<ChatSessionDto>.Failure(
                new Error(DomainErrorCodes.Chat.SessionNotFound, "Session not found.")
            );

        if (session.OwnerId != actorId)
            return Result<ChatSessionDto>.Failure(
                new Error(
                    DomainErrorCodes.Chat.SessionNotOwner,
                    "Only the owner can archive the session."
                )
            );

        session.Archive();
        await _sessionRepo.UpdateAsync(session, ct);

        return Result<ChatSessionDto>.Success(await MapToDtoAsync(session, ct));
    }

    public async Task<Result<ChatPermissionDto>> GetPermissionAsync(
        Guid sessionId,
        Guid actorId,
        CancellationToken ct = default
    )
    {
        var session = await _sessionRepo.GetByIdAsync(sessionId, ct);
        if (session == null)
            return Result<ChatPermissionDto>.Failure(
                new Error(DomainErrorCodes.Chat.SessionNotFound, "Session not found.")
            );

        if (!await CanReadSessionAsync(session, actorId, ct))
            return Result<ChatPermissionDto>.Failure(
                new Error(DomainErrorCodes.Chat.SessionNotOwner, "Access denied.")
            );

        var granted = session.Status == ChatSessionStatus.Active && session.ProjectId.HasValue;
        return Result<ChatPermissionDto>.Success(
            new ChatPermissionDto(granted, session.AiEditMode)
        );
    }

    public async Task<Result<ChatSessionDto>> AttachDocumentAsync(
        Guid sessionId,
        Guid documentId,
        Guid actorId,
        CancellationToken ct = default
    )
    {
        var session = await _sessionRepo.GetByIdAsync(sessionId, ct);
        if (session == null)
            return Result<ChatSessionDto>.Failure(
                new Error(DomainErrorCodes.Chat.SessionNotFound, "Session not found.")
            );

        if (session.OwnerId != actorId)
            return Result<ChatSessionDto>.Failure(
                new Error(
                    DomainErrorCodes.Chat.SessionNotOwner,
                    "Only the owner can attach documents."
                )
            );

        var document = await _documentRepo.GetByIdAsync(documentId, ct);
        if (document == null)
            return Result<ChatSessionDto>.Failure(
                new Error(DomainErrorCodes.Chat.SessionNotFound, "Document not found.")
            );

        if (document.UserId != actorId)
            return Result<ChatSessionDto>.Failure(
                new Error(
                    DomainErrorCodes.Chat.DocumentNotOwned,
                    "Cannot attach a document you don't own."
                )
            );

        if (await _sessionDocRepo.ExistsAsync(sessionId, documentId, ct))
            return Result<ChatSessionDto>.Failure(
                new Error(
                    DomainErrorCodes.Chat.DocumentAlreadyAttached,
                    "Document already attached."
                )
            );

        var sessionDoc = new ChatSessionDocument
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            DocumentId = documentId,
            AddedByUserId = actorId,
            AddedAt = DateTime.UtcNow,
        };
        await _sessionDocRepo.AddAsync(sessionDoc, ct);

        return Result<ChatSessionDto>.Success(await MapToDtoAsync(session, ct));
    }

    public async Task<Result<ChatSessionDto>> DetachDocumentAsync(
        Guid sessionId,
        Guid documentId,
        Guid actorId,
        CancellationToken ct = default
    )
    {
        var session = await _sessionRepo.GetByIdAsync(sessionId, ct);
        if (session == null)
            return Result<ChatSessionDto>.Failure(
                new Error(DomainErrorCodes.Chat.SessionNotFound, "Session not found.")
            );

        if (session.OwnerId != actorId)
            return Result<ChatSessionDto>.Failure(
                new Error(
                    DomainErrorCodes.Chat.SessionNotOwner,
                    "Only the owner can detach documents."
                )
            );

        await _sessionDocRepo.RemoveAsync(sessionId, documentId, ct);

        return Result<ChatSessionDto>.Success(await MapToDtoAsync(session, ct));
    }

    public async Task<Result<IReadOnlyList<DocumentDto>>> ListDocumentsAsync(
        Guid sessionId,
        Guid actorId,
        CancellationToken ct = default
    )
    {
        var session = await _sessionRepo.GetByIdAsync(sessionId, ct);
        if (session == null)
            return Result<IReadOnlyList<DocumentDto>>.Failure(
                new Error(DomainErrorCodes.Chat.SessionNotFound, "Session not found.")
            );

        if (!await CanReadSessionAsync(session, actorId, ct))
            return Result<IReadOnlyList<DocumentDto>>.Failure(
                new Error(DomainErrorCodes.Chat.SessionNotOwner, "Access denied.")
            );

        var sessionDocs = await _sessionDocRepo.GetBySessionAsync(sessionId, ct);
        var documentIds = sessionDocs.Select(sd => sd.DocumentId).ToList();

        var documents = new List<DocumentDto>();
        foreach (var docId in documentIds)
        {
            var doc = await _documentRepo.GetByIdAsync(docId, ct);
            if (doc != null && doc.ArchivedAt == null)
                documents.Add(MapDocumentToDto(doc));
        }

        return Result<IReadOnlyList<DocumentDto>>.Success(documents);
    }

    // ── Private helpers ────────────────────────────────────────────────────────

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

    private async Task<bool> GetPersonalityArchivedAsync(Guid? personalityId, CancellationToken ct)
    {
        if (!personalityId.HasValue)
            return false;

        var personality = await _personalityRepo.GetByIdAsync(personalityId.Value, ct);
        return personality?.ArchivedAt != null;
    }

    private async Task<ChatSessionDto> MapToDtoAsync(ChatSession session, CancellationToken ct)
    {
        return new ChatSessionDto(
            session.Id,
            session.Title,
            session.FolderId,
            session.ProjectId,
            session.OpenCardId,
            session.PersonalityId,
            await GetPersonalityArchivedAsync(session.PersonalityId, ct),
            session.Status,
            session.AiEditMode,
            session.SearchAllMyDocs,
            session.Summary,
            session.CreatedAt,
            session.UpdatedAt,
            session.ArchivedAt
        );
    }

    private static ChatMessageDto MapMessageToDto(ChatMessage message) =>
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

    private static DocumentDto MapDocumentToDto(Document doc) =>
        new(
            doc.Id,
            doc.Title,
            doc.ContentType,
            doc.Language,
            doc.Version,
            doc.CreatedAt,
            doc.UpdatedAt,
            doc.ArchivedAt
        );
}
