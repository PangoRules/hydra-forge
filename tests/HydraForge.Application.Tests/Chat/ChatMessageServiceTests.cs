namespace HydraForge.Application.Tests.Chat;

using HydraForge.Application.Auth;
using HydraForge.Application.Chat;
using HydraForge.Application.Llm;
using HydraForge.Application.Projects;
using HydraForge.Domain.Common;
using HydraForge.Domain.Entities.Auth;
using HydraForge.Domain.Entities.Chat;
using HydraForge.Domain.Entities.PersonalSpace;
using HydraForge.Domain.Entities.ProjectSpace;
using HydraForge.Domain.Enums;
using ChatMessage = HydraForge.Domain.Entities.Chat.ChatMessage;

public class ChatMessageServiceTests
{
    private static Guid NewId() => Guid.NewGuid();

    // ── In-memory fakes ─────────────────────────────────────────────────────────

    private sealed class FakeSessionRepo : IChatSessionRepository
    {
        public List<ChatSession> Sessions { get; } = [];

        public Task<ChatSession?> GetByIdAsync(Guid sessionId, CancellationToken ct = default) =>
            Task.FromResult(Sessions.FirstOrDefault(s => s.Id == sessionId));

        public Task<ChatSession?> GetActiveByPanelAsync(
            Guid projectId,
            Guid? openCardId,
            Guid ownerId,
            CancellationToken ct = default
        ) => Task.FromResult<ChatSession?>(null);

        public Task<IReadOnlyList<ChatSession>> ListAsync(
            Guid actorId,
            Guid? folderId,
            Guid? projectId,
            DateTime? before,
            Guid? beforeId,
            int limit,
            bool isAdmin = false,
            ChatSessionStatusFilter statusFilter = ChatSessionStatusFilter.NonArchived,
            ChatSessionScope scope = ChatSessionScope.Mine,
            IReadOnlySet<ChatSessionKind>? types = null,
            CancellationToken ct = default
        ) => Task.FromResult<IReadOnlyList<ChatSession>>([]);

        public Task<int> CountAsync(
            Guid actorId,
            Guid? folderId,
            Guid? projectId,
            bool isAdmin = false,
            ChatSessionStatusFilter statusFilter = ChatSessionStatusFilter.NonArchived,
            ChatSessionScope scope = ChatSessionScope.Mine,
            IReadOnlySet<ChatSessionKind>? types = null,
            CancellationToken ct = default
        ) => Task.FromResult(0);

        public Task AddAsync(ChatSession session, CancellationToken ct = default)
        {
            Sessions.Add(session);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(ChatSession session, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<ChatSession>> SearchByTitleAsync(
            Guid actorId,
            string query,
            Guid? projectId,
            int limit,
            bool isAdmin = false,
            ChatSessionScope scope = ChatSessionScope.Mine,
            CancellationToken ct = default
        ) => Task.FromResult<IReadOnlyList<ChatSession>>([]);

        public Task AddCardChatLinkAsync(CardChatLink link, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task<CardChatLink?> FindCardChatLinkAsync(
            Guid cardId,
            Guid chatSessionId,
            CancellationToken ct = default
        ) => Task.FromResult<CardChatLink?>(null);

        public Task UpdateCardChatLinkSummaryAsync(
            Guid linkId,
            string summary,
            CancellationToken ct = default
        ) => Task.CompletedTask;
    }

    private sealed class FakeMessageRepo : IChatMessageRepository
    {
        public List<ChatMessage> Messages { get; } = [];

        public Task<ChatMessage?> GetByIdAsync(Guid messageId, CancellationToken ct = default) =>
            Task.FromResult(Messages.FirstOrDefault(m => m.Id == messageId));

        public Task<IReadOnlyList<ChatMessage>> GetBySessionAsync(
            Guid sessionId,
            DateTime? before,
            Guid? beforeId,
            int limit,
            CancellationToken ct = default
        )
        {
            var query = Messages.Where(m => m.SessionId == sessionId);
            if (before.HasValue)
            {
                query = query.Where(m =>
                    m.CreatedAt < before.Value
                    || (m.CreatedAt == before.Value && beforeId.HasValue && m.Id < beforeId.Value)
                );
            }

            return Task.FromResult<IReadOnlyList<ChatMessage>>(
                query
                    .OrderByDescending(m => m.CreatedAt)
                    .ThenByDescending(m => m.Id)
                    .Take(limit)
                    .ToList()
            );
        }

        public Task AddAsync(ChatMessage message, CancellationToken ct = default)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<ChatMessage>> SearchByContentAsync(
            Guid ownerId,
            string query,
            Guid? projectId,
            int limit,
            bool isAdmin = false,
            ChatSessionScope scope = ChatSessionScope.Mine,
            CancellationToken ct = default
        ) => Task.FromResult<IReadOnlyList<ChatMessage>>([]);

        public Task<bool> DeleteFromAsync(
            Guid sessionId,
            Guid messageId,
            CancellationToken ct = default
        )
        {
            var target = Messages.FirstOrDefault(m =>
                m.Id == messageId && m.SessionId == sessionId
            );
            if (target is null)
                return Task.FromResult(false);

            Messages.RemoveAll(m =>
                m.SessionId == sessionId
                && (
                    m.CreatedAt > target.CreatedAt
                    || (m.CreatedAt == target.CreatedAt && m.Id.CompareTo(target.Id) >= 0)
                )
            );
            return Task.FromResult(true);
        }
    }

    private sealed class FakeStreamRegistry : IChatStreamRegistry
    {
        public List<Guid> CancelledSessions { get; } = [];

        public bool TryRegister(Guid sessionId, CancellationTokenSource cts) => true;

        public bool TryCancel(Guid sessionId)
        {
            CancelledSessions.Add(sessionId);
            return true;
        }

        public void Remove(Guid sessionId) { }
    }

    private sealed class FakeUserRepo : IUserRepository
    {
        public Task<User?> FindByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult<User?>(null);

        public Task<IReadOnlyDictionary<Guid, User>> FindByIdsAsync(
            IReadOnlyList<Guid> ids,
            CancellationToken ct = default
        ) => Task.FromResult<IReadOnlyDictionary<Guid, User>>(new Dictionary<Guid, User>());

        public Task<User?> FindByUsernameAsync(string username) => Task.FromResult<User?>(null);

        public Task<IReadOnlyDictionary<string, User>> FindByUsernamesAsync(
            IReadOnlyList<string> usernames,
            string? searchTerm = null,
            int maxResults = 10,
            CancellationToken ct = default
        ) => Task.FromResult<IReadOnlyDictionary<string, User>>(new Dictionary<string, User>());

        public Task UpdateLastLoginAsync(Guid userId, DateTime loginAt) => Task.CompletedTask;

        public Task<bool> AnyAdminExistsAsync() => Task.FromResult(false);

        public Task<bool> IsAdminAsync(Guid userId, CancellationToken ct = default) =>
            Task.FromResult(false);

        public Task CreateAsync(User user, CancellationToken ct = default) => Task.CompletedTask;

        public Task<IReadOnlyList<User>> ListAsync(
            int skip,
            int take,
            string? search,
            CancellationToken ct = default
        ) => Task.FromResult<IReadOnlyList<User>>([]);

        public Task<int> CountAsync(string? search, CancellationToken ct = default) =>
            Task.FromResult(0);

        public Task UpdateAsync(User user, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeMemberRepo : IProjectMemberRepository
    {
        public bool DenyAccess { get; set; }

        public Task<ProjectMember?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult<ProjectMember?>(null);

        public Task<ProjectMember?> GetByProjectAndUserAsync(
            Guid projectId,
            Guid userId,
            CancellationToken ct = default
        ) =>
            Task.FromResult<ProjectMember?>(
                DenyAccess
                    ? null
                    : new ProjectMember
                    {
                        Id = Guid.NewGuid(),
                        ProjectId = projectId,
                        UserId = userId,
                        Role = MemberRole.Owner,
                    }
            );

        public Task<IReadOnlyList<ProjectMember>> ListMembersAsync(
            Guid projectId,
            CancellationToken ct = default
        ) => Task.FromResult<IReadOnlyList<ProjectMember>>([]);

        public Task<IReadOnlyDictionary<Guid, int>> GetMemberCountsAsync(
            IEnumerable<Guid> projectIds,
            CancellationToken ct = default
        ) => Task.FromResult<IReadOnlyDictionary<Guid, int>>(new Dictionary<Guid, int>());

        public Task<IReadOnlyDictionary<Guid, MemberRole>> GetRolesByProjectAndUserAsync(
            IEnumerable<Guid> projectIds,
            Guid userId,
            CancellationToken ct = default
        ) =>
            Task.FromResult<IReadOnlyDictionary<Guid, MemberRole>>(
                projectIds.ToDictionary(id => id, _ => MemberRole.Owner)
            );

        public Task AddMemberAsync(ProjectMember member, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task UpdateMemberAsync(ProjectMember member, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task RemoveMemberAsync(Guid id, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task<bool> HasAnyRoleAsync(
            Guid projectId,
            Guid userId,
            CancellationToken ct = default
        ) => Task.FromResult(true);
    }

    // ── SUT factory ───────────────────────────────────────────────────────────

    private static (
        ChatMessageService service,
        FakeSessionRepo sessionRepo,
        FakeMessageRepo messageRepo,
        FakeStreamRegistry streamRegistry
    ) CreateSut()
    {
        var sessionRepo = new FakeSessionRepo();
        var messageRepo = new FakeMessageRepo();
        var userRepo = new FakeUserRepo();
        var memberRepo = new FakeMemberRepo();
        var streamRegistry = new FakeStreamRegistry();

        var service = new ChatMessageService(
            sessionRepo,
            messageRepo,
            userRepo,
            memberRepo,
            streamRegistry
        );

        return (service, sessionRepo, messageRepo, streamRegistry);
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SendUserMessageAsync_ValidSession_PersistsUserMessage()
    {
        var (service, sessionRepo, messageRepo, _) = CreateSut();
        var ownerId = NewId();
        var session = new ChatSession
        {
            Id = NewId(),
            OwnerId = ownerId,
            Status = ChatSessionStatus.Active,
        };
        sessionRepo.Sessions.Add(session);

        var result = await service.SendUserMessageAsync(session.Id, ownerId, "Hello, AI!");

        Assert.True(result.IsSuccess);
        Assert.Equal(MessageRole.User, result.Value.Role);
        Assert.Equal("Hello, AI!", result.Value.Content);
        Assert.Single(messageRepo.Messages);
        Assert.Equal(MessageRole.User, messageRepo.Messages[0].Role);
    }

    [Fact]
    public async Task SendUserMessageAsync_WithImages_SerializesImagesJson()
    {
        var (service, sessionRepo, messageRepo, _) = CreateSut();
        var ownerId = NewId();
        var session = new ChatSession
        {
            Id = NewId(),
            OwnerId = ownerId,
            Status = ChatSessionStatus.Active,
        };
        sessionRepo.Sessions.Add(session);

        var images = new List<ImageBlock> { new("key1", "image/png"), new("key2", "image/jpeg") };

        var result = await service.SendUserMessageAsync(
            session.Id,
            ownerId,
            "Look at this",
            images
        );

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value.ImagesJson);
        Assert.Contains("key1", result.Value.ImagesJson);
        Assert.Contains("key2", result.Value.ImagesJson);
    }

    [Fact]
    public async Task SendUserMessageAsync_ClosedSession_ReturnsSessionClosedError()
    {
        var (service, sessionRepo, _, _) = CreateSut();
        var ownerId = NewId();
        var session = new ChatSession
        {
            Id = NewId(),
            OwnerId = ownerId,
            Status = ChatSessionStatus.Closed,
        };
        sessionRepo.Sessions.Add(session);

        var result = await service.SendUserMessageAsync(session.Id, ownerId, "Hello");

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Chat.SessionClosed, result.Error.Code);
    }

    [Fact]
    public async Task SendUserMessageAsync_NonOwner_ReturnsSessionNotOwner()
    {
        var (service, sessionRepo, _, _) = CreateSut();
        var ownerId = NewId();
        var callerId = NewId();
        var session = new ChatSession
        {
            Id = NewId(),
            OwnerId = ownerId,
            Status = ChatSessionStatus.Active,
        };
        sessionRepo.Sessions.Add(session);

        var result = await service.SendUserMessageAsync(session.Id, callerId, "Hello");

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Chat.SessionNotOwner, result.Error.Code);
    }

    [Fact]
    public async Task SendUserMessageAsync_SessionNotFound_ReturnsSessionNotFound()
    {
        var (service, _, _, _) = CreateSut();

        var result = await service.SendUserMessageAsync(NewId(), NewId(), "Hello");

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Chat.SessionNotFound, result.Error.Code);
    }

    [Fact]
    public async Task GetHistoryAsync_Owner_ReturnsMessages()
    {
        var (service, sessionRepo, messageRepo, _) = CreateSut();
        var ownerId = NewId();
        var sessionId = NewId();
        var session = new ChatSession
        {
            Id = sessionId,
            OwnerId = ownerId,
            Status = ChatSessionStatus.Active,
        };
        sessionRepo.Sessions.Add(session);

        messageRepo.Messages.Add(
            new ChatMessage
            {
                Id = NewId(),
                SessionId = sessionId,
                Role = MessageRole.User,
                Content = "First",
                CreatedAt = DateTime.UtcNow.AddMinutes(-2),
            }
        );
        messageRepo.Messages.Add(
            new ChatMessage
            {
                Id = NewId(),
                SessionId = sessionId,
                Role = MessageRole.Assistant,
                Content = "Reply",
                CreatedAt = DateTime.UtcNow.AddMinutes(-1),
            }
        );

        var result = await service.GetHistoryAsync(sessionId, ownerId, beforeId: null);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Items.Count);
    }

    [Fact]
    public async Task GetHistoryAsync_NonOwnerNonShared_ReturnsAccessDenied()
    {
        var (service, sessionRepo, _, _) = CreateSut();
        var ownerId = NewId();
        var callerId = NewId();
        var session = new ChatSession
        {
            Id = NewId(),
            OwnerId = ownerId,
            IsShared = false,
            Status = ChatSessionStatus.Active,
        };
        sessionRepo.Sessions.Add(session);

        var result = await service.GetHistoryAsync(session.Id, callerId, beforeId: null);

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Chat.SessionNotOwner, result.Error.Code);
    }

    [Fact]
    public async Task GetHistoryAsync_SharedProjectMember_ReturnsMessages()
    {
        var (service, sessionRepo, messageRepo, _) = CreateSut();
        var ownerId = NewId();
        var projectId = NewId();
        var memberId = NewId();
        var sessionId = NewId();
        var session = new ChatSession
        {
            Id = sessionId,
            OwnerId = ownerId,
            ProjectId = projectId,
            IsShared = true,
            Status = ChatSessionStatus.Active,
        };
        sessionRepo.Sessions.Add(session);

        messageRepo.Messages.Add(
            new ChatMessage
            {
                Id = NewId(),
                SessionId = sessionId,
                Role = MessageRole.User,
                Content = "Hello",
                CreatedAt = DateTime.UtcNow,
            }
        );

        var result = await service.GetHistoryAsync(sessionId, memberId, beforeId: null);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.Items);
    }

    [Fact]
    public async Task GetHistoryAsync_Pagination_CursorPagination()
    {
        var (service, sessionRepo, messageRepo, _) = CreateSut();
        var ownerId = NewId();
        var sessionId = NewId();
        var session = new ChatSession
        {
            Id = sessionId,
            OwnerId = ownerId,
            Status = ChatSessionStatus.Active,
        };
        sessionRepo.Sessions.Add(session);

        var baseTime = DateTime.UtcNow;
        for (int i = 0; i < 5; i++)
        {
            messageRepo.Messages.Add(
                new ChatMessage
                {
                    Id = NewId(),
                    SessionId = sessionId,
                    Role = MessageRole.User,
                    Content = $"Message {i}",
                    CreatedAt = baseTime.AddMinutes(-i),
                }
            );
        }

        var firstPage = await service.GetHistoryAsync(
            sessionId,
            ownerId,
            before: null,
            beforeId: null,
            limit: 2
        );
        Assert.True(firstPage.IsSuccess);
        Assert.Equal(2, firstPage.Value.Items.Count);

        var oldestInFirstPage = firstPage.Value.Items.MinBy(m => m.CreatedAt)!;
        var secondPage = await service.GetHistoryAsync(
            sessionId,
            ownerId,
            before: oldestInFirstPage.CreatedAt,
            beforeId: oldestInFirstPage.Id,
            limit: 2
        );
        Assert.True(secondPage.IsSuccess);
        Assert.Equal(2, secondPage.Value.Items.Count);

        Assert.All(
            secondPage.Value.Items,
            m => Assert.True(m.CreatedAt < oldestInFirstPage.CreatedAt)
        );
    }

    [Fact]
    public async Task GetHistoryAsync_SessionNotFound_ReturnsSessionNotFound()
    {
        var (service, _, _, _) = CreateSut();

        var result = await service.GetHistoryAsync(NewId(), NewId(), beforeId: null);

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Chat.SessionNotFound, result.Error.Code);
    }

    [Fact]
    public async Task GetHistoryAsync_CompositeCursorExcludesItself()
    {
        var (service, sessionRepo, messageRepo, _) = CreateSut();
        var ownerId = NewId();
        var sessionId = NewId();
        var session = new ChatSession
        {
            Id = sessionId,
            OwnerId = ownerId,
            Status = ChatSessionStatus.Active,
        };
        sessionRepo.Sessions.Add(session);

        var baseTime = DateTime.UtcNow;
        var olderId = NewId();
        var newerId = NewId();
        messageRepo.Messages.Add(
            new ChatMessage
            {
                Id = olderId,
                SessionId = sessionId,
                Role = MessageRole.User,
                Content = "Older",
                CreatedAt = baseTime,
            }
        );
        messageRepo.Messages.Add(
            new ChatMessage
            {
                Id = newerId,
                SessionId = sessionId,
                Role = MessageRole.User,
                Content = "Newer",
                CreatedAt = baseTime,
            }
        );

        var firstPage = await service.GetHistoryAsync(sessionId, ownerId, beforeId: null, limit: 1);
        Assert.True(firstPage.IsSuccess);
        Assert.Single(firstPage.Value.Items);

        var cursor = firstPage.Value.Items[0];
        var secondPage = await service.GetHistoryAsync(
            sessionId,
            ownerId,
            before: cursor.CreatedAt,
            beforeId: cursor.Id,
            limit: 1
        );
        Assert.True(secondPage.IsSuccess);
        Assert.Single(secondPage.Value.Items);
        Assert.NotEqual(cursor.Id, secondPage.Value.Items[0].Id);
    }

    [Fact]
    public async Task RollbackAsync_UserMessage_DeletesTargetAndEverythingAfter()
    {
        var (service, sessionRepo, messageRepo, _) = CreateSut();
        var ownerId = NewId();
        var sessionId = NewId();
        sessionRepo.Sessions.Add(
            new ChatSession
            {
                Id = sessionId,
                OwnerId = ownerId,
                Status = ChatSessionStatus.Active,
            }
        );

        var baseTime = DateTime.UtcNow;
        var kept = new ChatMessage
        {
            Id = NewId(),
            SessionId = sessionId,
            Role = MessageRole.User,
            Content = "kept",
            CreatedAt = baseTime,
        };
        var target = new ChatMessage
        {
            Id = NewId(),
            SessionId = sessionId,
            Role = MessageRole.User,
            Content = "rolled back",
            CreatedAt = baseTime.AddMinutes(1),
        };
        var trailing = new ChatMessage
        {
            Id = NewId(),
            SessionId = sessionId,
            Role = MessageRole.Assistant,
            Content = "reply",
            CreatedAt = baseTime.AddMinutes(2),
        };
        messageRepo.Messages.AddRange([kept, target, trailing]);

        var result = await service.RollbackAsync(sessionId, ownerId, target.Id);

        Assert.True(result.IsSuccess);
        Assert.Single(messageRepo.Messages);
        Assert.Equal(kept.Id, messageRepo.Messages[0].Id);
    }

    [Fact]
    public async Task RollbackAsync_CancelsActiveStreamForSession()
    {
        var (service, sessionRepo, messageRepo, streamRegistry) = CreateSut();
        var ownerId = NewId();
        var sessionId = NewId();
        sessionRepo.Sessions.Add(
            new ChatSession
            {
                Id = sessionId,
                OwnerId = ownerId,
                Status = ChatSessionStatus.Active,
            }
        );
        var target = new ChatMessage
        {
            Id = NewId(),
            SessionId = sessionId,
            Role = MessageRole.User,
            Content = "msg",
            CreatedAt = DateTime.UtcNow,
        };
        messageRepo.Messages.Add(target);

        await service.RollbackAsync(sessionId, ownerId, target.Id);

        Assert.Contains(sessionId, streamRegistry.CancelledSessions);
    }

    [Fact]
    public async Task RollbackAsync_NonOwner_ReturnsSessionNotOwner()
    {
        var (service, sessionRepo, messageRepo, _) = CreateSut();
        var ownerId = NewId();
        var callerId = NewId();
        var sessionId = NewId();
        sessionRepo.Sessions.Add(
            new ChatSession
            {
                Id = sessionId,
                OwnerId = ownerId,
                Status = ChatSessionStatus.Active,
            }
        );
        var target = new ChatMessage
        {
            Id = NewId(),
            SessionId = sessionId,
            Role = MessageRole.User,
            Content = "msg",
            CreatedAt = DateTime.UtcNow,
        };
        messageRepo.Messages.Add(target);

        var result = await service.RollbackAsync(sessionId, callerId, target.Id);

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Chat.SessionNotOwner, result.Error.Code);
        Assert.Single(messageRepo.Messages);
    }

    [Fact]
    public async Task RollbackAsync_SessionNotFound_ReturnsSessionNotFound()
    {
        var (service, _, _, _) = CreateSut();

        var result = await service.RollbackAsync(NewId(), NewId(), NewId());

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Chat.SessionNotFound, result.Error.Code);
    }

    [Fact]
    public async Task RollbackAsync_MessageNotFound_ReturnsMessageNotFound()
    {
        var (service, sessionRepo, _, _) = CreateSut();
        var ownerId = NewId();
        var sessionId = NewId();
        sessionRepo.Sessions.Add(
            new ChatSession
            {
                Id = sessionId,
                OwnerId = ownerId,
                Status = ChatSessionStatus.Active,
            }
        );

        var result = await service.RollbackAsync(sessionId, ownerId, NewId());

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Chat.MessageNotFound, result.Error.Code);
    }
}
