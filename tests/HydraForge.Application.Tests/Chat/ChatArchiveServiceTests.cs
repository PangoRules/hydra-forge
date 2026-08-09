namespace HydraForge.Application.Tests.Chat;

using HydraForge.Application.Chat;
using HydraForge.Domain.Entities.Chat;
using HydraForge.Domain.Enums;

public class ChatArchiveServiceTests
{
    private static Guid NewId() => Guid.NewGuid();

    // Unlike ChatFolderServiceTests' fake session repo (which ignores the scope
    // parameter entirely), this one actually applies it — needed to catch
    // ArchiveFolderAsync passing the wrong scope, which would otherwise pass
    // silently against a fake that returns the same rows regardless of scope.
    private sealed class ScopeAwareFakeSessionRepo : IChatSessionRepository
    {
        public List<ChatSession> Sessions { get; } = [];
        public List<Guid> UpdatedSessionIds { get; } = [];

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
        )
        {
            var query = Sessions.Where(s => s.ArchivedAt == null);
            if (folderId.HasValue)
                query = query.Where(s => s.FolderId == folderId.Value);

            // scope == Mine: strictly the actor's own sessions. scope == Participated:
            // owner OR (any session, since this fake has no membership concept — the
            // point of these tests is only to catch ArchiveFolderAsync passing the
            // wrong scope value, not to re-verify participated-in semantics).
            query = scope == ChatSessionScope.Mine ? query.Where(s => s.OwnerId == actorId) : query;

            return Task.FromResult<IReadOnlyList<ChatSession>>(query.ToList());
        }

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

        public Task<IReadOnlyList<ChatSession>> SearchByTitleAsync(
            Guid actorId,
            string query,
            Guid? projectId,
            int limit,
            bool isAdmin = false,
            ChatSessionScope scope = ChatSessionScope.Mine,
            CancellationToken ct = default
        ) => Task.FromResult<IReadOnlyList<ChatSession>>([]);

        public Task AddAsync(ChatSession session, CancellationToken ct = default)
        {
            Sessions.Add(session);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(ChatSession session, CancellationToken ct = default)
        {
            UpdatedSessionIds.Add(session.Id);
            return Task.CompletedTask;
        }

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

    private sealed class FakeFolderRepo : IChatFolderRepository
    {
        public List<ChatFolder> Folders { get; } = [];

        public Task<ChatFolder?> GetByIdAsync(Guid folderId, CancellationToken ct = default) =>
            Task.FromResult(Folders.FirstOrDefault(f => f.Id == folderId));

        public Task<IReadOnlyList<ChatFolder>> ListByOwnerAsync(
            Guid ownerId,
            Guid? projectId,
            CancellationToken ct = default
        ) => Task.FromResult<IReadOnlyList<ChatFolder>>([]);

        public Task AddAsync(ChatFolder folder, CancellationToken ct = default)
        {
            Folders.Add(folder);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(ChatFolder folder, CancellationToken ct = default) =>
            Task.CompletedTask;
    }

    // Regression: ArchiveFolderAsync used to list sessions with scope=Participated —
    // an owner-only maintenance cascade admitting "session I merely participate in
    // as a project member" is a scope-widening bug (see CLAUDE.md's scope-default
    // lesson). It must archive only the folder owner's own sessions.
    [Fact]
    public async Task ArchiveFolderAsync_OnlyArchivesFolderOwnersOwnSessions()
    {
        var sessionRepo = new ScopeAwareFakeSessionRepo();
        var folderRepo = new FakeFolderRepo();
        var service = new ChatArchiveService(folderRepo, sessionRepo);

        var ownerId = NewId();
        var otherUserId = NewId();
        var folderId = NewId();

        var folder = new ChatFolder
        {
            Id = folderId,
            OwnerId = ownerId,
            Name = "Folder",
        };
        folderRepo.Folders.Add(folder);

        var ownedSession = new ChatSession
        {
            Id = NewId(),
            OwnerId = ownerId,
            FolderId = folderId,
            Status = ChatSessionStatus.Active,
        };
        var otherUsersSessionInSameFolderId = new ChatSession
        {
            Id = NewId(),
            OwnerId = otherUserId,
            FolderId = folderId,
            Status = ChatSessionStatus.Active,
        };
        sessionRepo.Sessions.Add(ownedSession);
        sessionRepo.Sessions.Add(otherUsersSessionInSameFolderId);

        await service.ArchiveFolderAsync(folderId);

        Assert.True(folder.ArchivedAt.HasValue);
        Assert.Contains(ownedSession.Id, sessionRepo.UpdatedSessionIds);
        Assert.DoesNotContain(otherUsersSessionInSameFolderId.Id, sessionRepo.UpdatedSessionIds);
    }

    [Fact]
    public async Task ArchiveFolderAsync_MissingFolder_NoOp()
    {
        var sessionRepo = new ScopeAwareFakeSessionRepo();
        var folderRepo = new FakeFolderRepo();
        var service = new ChatArchiveService(folderRepo, sessionRepo);

        await service.ArchiveFolderAsync(NewId());

        Assert.Empty(sessionRepo.UpdatedSessionIds);
    }

    [Fact]
    public async Task ArchiveSessionAsync_ArchivesSession()
    {
        var sessionRepo = new ScopeAwareFakeSessionRepo();
        var folderRepo = new FakeFolderRepo();
        var service = new ChatArchiveService(folderRepo, sessionRepo);

        var session = new ChatSession
        {
            Id = NewId(),
            OwnerId = NewId(),
            Status = ChatSessionStatus.Active,
        };
        sessionRepo.Sessions.Add(session);

        await service.ArchiveSessionAsync(session.Id);

        Assert.True(session.ArchivedAt.HasValue);
        Assert.Contains(session.Id, sessionRepo.UpdatedSessionIds);
    }
}
