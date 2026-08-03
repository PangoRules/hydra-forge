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
using Microsoft.Extensions.Logging;
using NSubstitute;
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
            Guid ownerId,
            Guid? folderId,
            Guid? projectId,
            DateTime? before,
            int limit,
            CancellationToken ct = default
        ) => Task.FromResult<IReadOnlyList<ChatSession>>([]);

        public Task AddAsync(ChatSession session, CancellationToken ct = default)
        {
            Sessions.Add(session);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(ChatSession session, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task AddCardChatLinkAsync(CardChatLink link, CancellationToken ct = default) =>
            Task.CompletedTask;
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
                    m.CreatedAt < before.Value ||
                    (m.CreatedAt == before.Value && beforeId.HasValue && m.Id < beforeId.Value)
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
        FakeMessageRepo messageRepo
    ) CreateSut()
    {
        var sessionRepo = new FakeSessionRepo();
        var messageRepo = new FakeMessageRepo();
        var userRepo = new FakeUserRepo();
        var memberRepo = new FakeMemberRepo();
        var logger = Substitute.For<ILogger<ChatMessageService>>();

        var service = new ChatMessageService(sessionRepo, messageRepo, userRepo, memberRepo);

        return (service, sessionRepo, messageRepo);
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SendUserMessageAsync_ValidSession_PersistsUserMessage()
    {
        var (service, sessionRepo, messageRepo) = CreateSut();
        var ownerId = NewId();
        var session = new ChatSession
        {
            Id = NewId(),
            OwnerId = ownerId,
            Status = ChatSessionStatus.Active,
        };
        sessionRepo.Sessions.Add(session);

        var result = await service.SendUserMessageAsync(
            session.Id,
            ownerId,
            "Hello, AI!"
        );

        Assert.True(result.IsSuccess);
        Assert.Equal(MessageRole.User, result.Value.Role);
        Assert.Equal("Hello, AI!", result.Value.Content);
        Assert.Single(messageRepo.Messages);
        Assert.Equal(MessageRole.User, messageRepo.Messages[0].Role);
    }

    [Fact]
    public async Task SendUserMessageAsync_WithImages_SerializesImagesJson()
    {
        var (service, sessionRepo, messageRepo) = CreateSut();
        var ownerId = NewId();
        var session = new ChatSession
        {
            Id = NewId(),
            OwnerId = ownerId,
            Status = ChatSessionStatus.Active,
        };
        sessionRepo.Sessions.Add(session);

        var images = new List<ImageBlock>
        {
            new("key1", "image/png"),
            new("key2", "image/jpeg"),
        };

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
        var (service, sessionRepo, _) = CreateSut();
        var ownerId = NewId();
        var session = new ChatSession
        {
            Id = NewId(),
            OwnerId = ownerId,
            Status = ChatSessionStatus.Closed,
        };
        sessionRepo.Sessions.Add(session);

        var result = await service.SendUserMessageAsync(
            session.Id,
            ownerId,
            "Hello"
        );

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Chat.SessionClosed, result.Error.Code);
    }

    [Fact]
    public async Task SendUserMessageAsync_NonOwner_ReturnsSessionNotOwner()
    {
        var (service, sessionRepo, _) = CreateSut();
        var ownerId = NewId();
        var callerId = NewId();
        var session = new ChatSession
        {
            Id = NewId(),
            OwnerId = ownerId,
            Status = ChatSessionStatus.Active,
        };
        sessionRepo.Sessions.Add(session);

        var result = await service.SendUserMessageAsync(
            session.Id,
            callerId,
            "Hello"
        );

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Chat.SessionNotOwner, result.Error.Code);
    }

    [Fact]
    public async Task SendUserMessageAsync_SessionNotFound_ReturnsSessionNotFound()
    {
        var (service, _, _) = CreateSut();

        var result = await service.SendUserMessageAsync(
            NewId(),
            NewId(),
            "Hello"
        );

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Chat.SessionNotFound, result.Error.Code);
    }

    [Fact]
    public async Task GetHistoryAsync_Owner_ReturnsMessages()
    {
        var (service, sessionRepo, messageRepo) = CreateSut();
        var ownerId = NewId();
        var sessionId = NewId();
        var session = new ChatSession
        {
            Id = sessionId,
            OwnerId = ownerId,
            Status = ChatSessionStatus.Active,
        };
        sessionRepo.Sessions.Add(session);

        messageRepo.Messages.Add(new ChatMessage
        {
            Id = NewId(),
            SessionId = sessionId,
            Role = MessageRole.User,
            Content = "First",
            CreatedAt = DateTime.UtcNow.AddMinutes(-2),
        });
        messageRepo.Messages.Add(new ChatMessage
        {
            Id = NewId(),
            SessionId = sessionId,
            Role = MessageRole.Assistant,
            Content = "Reply",
            CreatedAt = DateTime.UtcNow.AddMinutes(-1),
        });

        var result = await service.GetHistoryAsync(sessionId, ownerId, beforeId: null);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Items.Count);
    }

    [Fact]
    public async Task GetHistoryAsync_NonOwnerNonShared_ReturnsAccessDenied()
    {
        var (service, sessionRepo, _) = CreateSut();
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
        var (service, sessionRepo, messageRepo) = CreateSut();
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

        messageRepo.Messages.Add(new ChatMessage
        {
            Id = NewId(),
            SessionId = sessionId,
            Role = MessageRole.User,
            Content = "Hello",
            CreatedAt = DateTime.UtcNow,
        });

        var result = await service.GetHistoryAsync(sessionId, memberId, beforeId: null);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.Items);
    }

    [Fact]
    public async Task GetHistoryAsync_Pagination_CursorPagination()
    {
        var (service, sessionRepo, messageRepo) = CreateSut();
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
            messageRepo.Messages.Add(new ChatMessage
            {
                Id = NewId(),
                SessionId = sessionId,
                Role = MessageRole.User,
                Content = $"Message {i}",
                CreatedAt = baseTime.AddMinutes(-i),
            });
        }

        var firstPage = await service.GetHistoryAsync(sessionId, ownerId, before: null, beforeId: null, limit: 2);
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
        var (service, _, _) = CreateSut();

        var result = await service.GetHistoryAsync(NewId(), NewId(), beforeId: null);

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Chat.SessionNotFound, result.Error.Code);
    }

    [Fact]
    public async Task GetHistoryAsync_CompositeCursorExcludesItself()
    {
        var (service, sessionRepo, messageRepo) = CreateSut();
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
        messageRepo.Messages.Add(new ChatMessage { Id = NewId(), SessionId = sessionId, Role = MessageRole.User, Content = "Older", CreatedAt = baseTime.AddMinutes(-2) });
        messageRepo.Messages.Add(new ChatMessage { Id = NewId(), SessionId = sessionId, Role = MessageRole.User, Content = "Newer", CreatedAt = baseTime.AddMinutes(-1) });

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
    public async Task SendUserMessageAsync_ActiveSession_PersistsAndReturnsMessage()
    {
        var (service, sessionRepo, messageRepo) = CreateSut();
        var ownerId = NewId();
        var session = new ChatSession
        {
            Id = NewId(),
            OwnerId = ownerId,
            Status = ChatSessionStatus.Active,
        };
        sessionRepo.Sessions.Add(session);

        var result = await service.SendUserMessageAsync(
            session.Id,
            ownerId,
            "Test message"
        );

        Assert.True(result.IsSuccess);
        Assert.Equal(MessageRole.User, result.Value.Role);
        Assert.Equal("Test message", result.Value.Content);
        Assert.Single(messageRepo.Messages);
    }
}
