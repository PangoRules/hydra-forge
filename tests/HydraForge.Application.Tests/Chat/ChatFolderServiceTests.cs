namespace HydraForge.Application.Tests.Chat;

using HydraForge.Application.Auth;
using HydraForge.Application.Chat;
using HydraForge.Application.Projects;
using HydraForge.Domain.Common;
using HydraForge.Domain.Entities.Auth;
using HydraForge.Domain.Entities.Chat;
using HydraForge.Domain.Entities.PersonalSpace;
using HydraForge.Domain.Entities.ProjectSpace;
using HydraForge.Domain.Enums;

public class ChatFolderServiceTests
{
    private static Guid NewId() => Guid.NewGuid();

    // ── In-memory fakes ─────────────────────────────────────────────────────────

    private sealed class FakeFolderRepo : IChatFolderRepository
    {
        public List<ChatFolder> Folders { get; } = [];

        public Task AddAsync(ChatFolder folder, CancellationToken ct = default)
        {
            Folders.Add(folder);
            return Task.CompletedTask;
        }

        public Task<ChatFolder?> GetByIdAsync(Guid folderId, CancellationToken ct = default) =>
            Task.FromResult(Folders.FirstOrDefault(f => f.Id == folderId));

        public Task<IReadOnlyList<ChatFolder>> ListByOwnerAsync(
            Guid ownerId,
            Guid? projectId,
            CancellationToken ct = default
        )
        {
            var query = Folders.Where(f => f.OwnerId == ownerId && f.ArchivedAt == null);
            if (projectId.HasValue)
                query = query.Where(f => f.ProjectId == projectId.Value);
            return Task.FromResult<IReadOnlyList<ChatFolder>>(query.ToList());
        }

        public Task UpdateAsync(ChatFolder folder, CancellationToken ct = default) =>
            Task.CompletedTask;
    }

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
            Guid? beforeId,
            int limit,
            bool isAdmin = false,
            ChatSessionStatusFilter statusFilter = ChatSessionStatusFilter.NonArchived,
            CancellationToken ct = default
        )
        {
            var query = isAdmin
                ? Sessions.AsEnumerable()
                : Sessions.Where(s => s.OwnerId == ownerId);
            query = statusFilter switch
            {
                ChatSessionStatusFilter.Archived => query.Where(s => s.ArchivedAt != null),
                ChatSessionStatusFilter.Active => query.Where(s =>
                    s.ArchivedAt == null && s.Status == ChatSessionStatus.Active
                ),
                ChatSessionStatusFilter.Closed => query.Where(s =>
                    s.ArchivedAt == null && s.Status == ChatSessionStatus.Closed
                ),
                _ => query.Where(s => s.ArchivedAt == null),
            };
            if (folderId.HasValue)
                query = query.Where(s => s.FolderId == folderId.Value);
            if (projectId.HasValue)
                query = query.Where(s => s.ProjectId == projectId.Value);
            return Task.FromResult<IReadOnlyList<ChatSession>>(
                query.OrderByDescending(s => s.UpdatedAt).Take(limit).ToList()
            );
        }

        public Task<int> CountAsync(
            Guid ownerId,
            Guid? folderId,
            Guid? projectId,
            bool isAdmin = false,
            ChatSessionStatusFilter statusFilter = ChatSessionStatusFilter.NonArchived,
            CancellationToken ct = default
        )
        {
            var query = isAdmin
                ? Sessions.AsEnumerable()
                : Sessions.Where(s => s.OwnerId == ownerId);
            query = statusFilter switch
            {
                ChatSessionStatusFilter.Archived => query.Where(s => s.ArchivedAt != null),
                ChatSessionStatusFilter.Active => query.Where(s =>
                    s.ArchivedAt == null && s.Status == ChatSessionStatus.Active
                ),
                ChatSessionStatusFilter.Closed => query.Where(s =>
                    s.ArchivedAt == null && s.Status == ChatSessionStatus.Closed
                ),
                _ => query.Where(s => s.ArchivedAt == null),
            };
            if (folderId.HasValue)
                query = query.Where(s => s.FolderId == folderId.Value);
            if (projectId.HasValue)
                query = query.Where(s => s.ProjectId == projectId.Value);
            return Task.FromResult(query.Count());
        }

        public Task AddAsync(ChatSession session, CancellationToken ct = default)
        {
            Sessions.Add(session);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(ChatSession session, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<ChatSession>> SearchByTitleAsync(
            Guid ownerId,
            string query,
            Guid? projectId,
            int limit,
            bool isAdmin = false,
            CancellationToken ct = default
        ) => Task.FromResult<IReadOnlyList<ChatSession>>([]);

        public Task AddCardChatLinkAsync(CardChatLink link, CancellationToken ct = default) =>
            Task.CompletedTask;
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
        ChatFolderService service,
        FakeFolderRepo folderRepo,
        FakeSessionRepo sessionRepo
    ) CreateSut()
    {
        var folderRepo = new FakeFolderRepo();
        var sessionRepo = new FakeSessionRepo();
        var userRepo = new FakeUserRepo();
        var memberRepo = new FakeMemberRepo();
        var archiveService = new ChatArchiveService(folderRepo, sessionRepo);
        var service = new ChatFolderService(
            folderRepo,
            sessionRepo,
            userRepo,
            memberRepo,
            archiveService
        );
        return (service, folderRepo, sessionRepo);
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_ValidRequest_CreatesFolder()
    {
        var (service, folderRepo, _) = CreateSut();
        var actorId = NewId();

        var result = await service.CreateAsync(
            new CreateChatFolderRequest(Name: "My Folder", ParentFolderId: null, ProjectId: null),
            actorId
        );

        Assert.True(result.IsSuccess);
        Assert.Single(folderRepo.Folders);
        Assert.Equal("My Folder", folderRepo.Folders[0].Name);
        Assert.Equal(actorId, folderRepo.Folders[0].OwnerId);
    }

    [Fact]
    public async Task CreateAsync_WithProjectId_ChecksMembership()
    {
        var (service, folderRepo, _) = CreateSut();
        var actorId = NewId();
        var projectId = NewId();
        var denyMemberRepo = new FakeMemberRepo();
        denyMemberRepo.DenyAccess = true;

        var userRepo = new FakeUserRepo();
        var sessionRepo = new FakeSessionRepo();
        var archiveService = new ChatArchiveService(folderRepo, sessionRepo);
        var denyService = new ChatFolderService(
            folderRepo,
            sessionRepo,
            userRepo,
            denyMemberRepo,
            archiveService
        );

        var result = await denyService.CreateAsync(
            new CreateChatFolderRequest(
                Name: "Project Folder",
                ParentFolderId: null,
                ProjectId: projectId
            ),
            actorId
        );

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Projects.MembershipDenied, result.Error.Code);
    }

    [Fact]
    public async Task CreateAsync_Depth2Parent_AllowsCreation()
    {
        var (service, folderRepo, _) = CreateSut();
        var actorId = NewId();

        // Root folder
        var root = new ChatFolder
        {
            Id = NewId(),
            OwnerId = actorId,
            Name = "Root",
        };
        folderRepo.Folders.Add(root);

        // Child folder (depth=1)
        var child = new ChatFolder
        {
            Id = NewId(),
            OwnerId = actorId,
            Name = "Child",
            ParentFolderId = root.Id,
        };
        folderRepo.Folders.Add(child);

        // Create grandchild (depth=2) — allowed
        var result = await service.CreateAsync(
            new CreateChatFolderRequest("Grandchild", child.Id, null),
            actorId
        );

        Assert.True(result.IsSuccess);
        Assert.Equal(child.Id, result.Value.ParentFolderId);
    }

    [Fact]
    public async Task CreateAsync_Depth3Parent_RejectsCreation()
    {
        var (service, folderRepo, _) = CreateSut();
        var actorId = NewId();

        // Root (depth=0)
        var root = new ChatFolder
        {
            Id = NewId(),
            OwnerId = actorId,
            Name = "Root",
        };
        folderRepo.Folders.Add(root);

        // Child (depth=1)
        var child1 = new ChatFolder
        {
            Id = NewId(),
            OwnerId = actorId,
            Name = "Child1",
            ParentFolderId = root.Id,
        };
        folderRepo.Folders.Add(child1);

        // Grandchild (depth=2)
        var child2 = new ChatFolder
        {
            Id = NewId(),
            OwnerId = actorId,
            Name = "Child2",
            ParentFolderId = child1.Id,
        };
        folderRepo.Folders.Add(child2);

        // Great-grandchild (depth=3) — should be rejected
        var result = await service.CreateAsync(
            new CreateChatFolderRequest("GreatGrandchild", child2.Id, null),
            actorId
        );

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Chat.FolderMaxDepth, result.Error.Code);
    }

    [Fact]
    public async Task ListAsync_ReturnsOwnerFolders()
    {
        var (service, folderRepo, _) = CreateSut();
        var actorId = NewId();

        folderRepo.Folders.Add(
            new ChatFolder
            {
                Id = NewId(),
                OwnerId = actorId,
                Name = "Folder1",
            }
        );
        folderRepo.Folders.Add(
            new ChatFolder
            {
                Id = NewId(),
                OwnerId = actorId,
                Name = "Folder2",
                ArchivedAt = DateTime.UtcNow,
            }
        );
        folderRepo.Folders.Add(
            new ChatFolder
            {
                Id = NewId(),
                OwnerId = NewId(),
                Name = "Other",
            }
        );

        var result = await service.ListAsync(actorId, projectId: null);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value); // archived ones excluded
        Assert.Equal("Folder1", result.Value[0].Name);
    }

    [Fact]
    public async Task UpdateAsync_ValidRequest_UpdatesFolder()
    {
        var (service, folderRepo, _) = CreateSut();
        var actorId = NewId();
        var folder = new ChatFolder
        {
            Id = NewId(),
            OwnerId = actorId,
            Name = "Old Name",
        };
        folderRepo.Folders.Add(folder);

        var result = await service.UpdateAsync(
            folder.Id,
            new UpdateChatFolderRequest(Name: "New Name", ParentFolderId: null),
            actorId
        );

        Assert.True(result.IsSuccess);
        Assert.Equal("New Name", result.Value.Name);
    }

    [Fact]
    public async Task UpdateAsync_NotOwner_ReturnsError()
    {
        var (service, folderRepo, _) = CreateSut();
        var ownerId = NewId();
        var otherId = NewId();
        var folder = new ChatFolder
        {
            Id = NewId(),
            OwnerId = ownerId,
            Name = "Folder",
        };
        folderRepo.Folders.Add(folder);

        var result = await service.UpdateAsync(
            folder.Id,
            new UpdateChatFolderRequest(Name: "New Name", ParentFolderId: null),
            otherId
        );

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Chat.SessionNotOwner, result.Error.Code);
    }

    [Fact]
    public async Task UpdateAsync_SelfParent_Rejects()
    {
        var (service, folderRepo, _) = CreateSut();
        var actorId = NewId();
        var folder = new ChatFolder
        {
            Id = NewId(),
            OwnerId = actorId,
            Name = "Folder",
        };
        folderRepo.Folders.Add(folder);

        var result = await service.UpdateAsync(
            folder.Id,
            new UpdateChatFolderRequest(Name: "Folder", ParentFolderId: folder.Id),
            actorId
        );

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Chat.FolderSelfParent, result.Error.Code);
    }

    [Fact]
    public async Task ArchiveAsync_EmptyFolder_SetsArchivedAt()
    {
        var (service, folderRepo, _) = CreateSut();
        var actorId = NewId();
        var folder = new ChatFolder
        {
            Id = NewId(),
            OwnerId = actorId,
            Name = "Empty Folder",
        };
        folderRepo.Folders.Add(folder);

        var result = await service.ArchiveAsync(folder.Id, actorId);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value.ArchivedAt);
    }

    [Fact]
    public async Task ArchiveAsync_WithSessions_ArchivesFolderAndAllSessions()
    {
        var (service, folderRepo, sessionRepo) = CreateSut();
        var actorId = NewId();
        var folder = new ChatFolder
        {
            Id = NewId(),
            OwnerId = actorId,
            Name = "Folder With Sessions",
        };
        folderRepo.Folders.Add(folder);

        var session1 = new ChatSession
        {
            Id = NewId(),
            OwnerId = actorId,
            FolderId = folder.Id,
            Title = "Session 1",
            Status = ChatSessionStatus.Active,
        };
        var session2 = new ChatSession
        {
            Id = NewId(),
            OwnerId = actorId,
            FolderId = folder.Id,
            Title = "Session 2",
            Status = ChatSessionStatus.Active,
        };
        sessionRepo.Sessions.Add(session1);
        sessionRepo.Sessions.Add(session2);

        var result = await service.ArchiveAsync(folder.Id, actorId);

        Assert.True(result.IsSuccess);
        Assert.NotNull(folder.ArchivedAt);
        Assert.NotNull(session1.ArchivedAt);
        Assert.NotNull(session2.ArchivedAt);
    }

    [Fact]
    public async Task ArchiveAsync_NotOwner_ReturnsError()
    {
        var (service, folderRepo, _) = CreateSut();
        var ownerId = NewId();
        var otherId = NewId();
        var folder = new ChatFolder
        {
            Id = NewId(),
            OwnerId = ownerId,
            Name = "Folder",
        };
        folderRepo.Folders.Add(folder);

        var result = await service.ArchiveAsync(folder.Id, otherId);

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Chat.SessionNotOwner, result.Error.Code);
    }

    [Fact]
    public async Task ArchiveAsync_NotFound_ReturnsError()
    {
        var (service, _, _) = CreateSut();

        var result = await service.ArchiveAsync(NewId(), NewId());

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Chat.FolderNotFound, result.Error.Code);
    }

    [Fact]
    public async Task CreateAsync_ParentNotFound_ReturnsError()
    {
        var (service, _, _) = CreateSut();
        var actorId = NewId();

        var result = await service.CreateAsync(
            new CreateChatFolderRequest("Orphan", NewId(), null),
            actorId
        );

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Chat.FolderNotFound, result.Error.Code);
    }

    [Fact]
    public async Task CreateAsync_ParentOwnedByAnotherUser_Rejects()
    {
        var (service, folderRepo, _) = CreateSut();
        var otherOwnerId = NewId();
        var parent = new ChatFolder
        {
            Id = NewId(),
            OwnerId = otherOwnerId,
            Name = "Someone Else's",
        };
        folderRepo.Folders.Add(parent);

        var result = await service.CreateAsync(
            new CreateChatFolderRequest("Mine", parent.Id, null),
            NewId()
        );

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Chat.FolderInvalidParent, result.Error.Code);
    }

    [Fact]
    public async Task CreateAsync_ParentInDifferentProjectScope_Rejects()
    {
        var (service, folderRepo, _) = CreateSut();
        var actorId = NewId();
        var parent = new ChatFolder
        {
            Id = NewId(),
            OwnerId = actorId,
            Name = "Project A Folder",
            ProjectId = NewId(),
        };
        folderRepo.Folders.Add(parent);

        var result = await service.CreateAsync(
            new CreateChatFolderRequest("Personal", parent.Id, null),
            actorId
        );

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Chat.FolderInvalidParent, result.Error.Code);
    }

    [Fact]
    public async Task UpdateAsync_ParentNotFound_ReturnsError()
    {
        var (service, folderRepo, _) = CreateSut();
        var actorId = NewId();
        var folder = new ChatFolder
        {
            Id = NewId(),
            OwnerId = actorId,
            Name = "Folder",
        };
        folderRepo.Folders.Add(folder);

        var result = await service.UpdateAsync(
            folder.Id,
            new UpdateChatFolderRequest("Folder", NewId()),
            actorId
        );

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Chat.FolderNotFound, result.Error.Code);
    }

    [Fact]
    public async Task UpdateAsync_ReparentUnderDescendant_ExceedingMaxDepth_Rejects()
    {
        var (service, folderRepo, _) = CreateSut();
        var actorId = NewId();

        // RootA -> ChildA -> GrandA (depths 0/1/2)
        var rootA = new ChatFolder
        {
            Id = NewId(),
            OwnerId = actorId,
            Name = "RootA",
        };
        var childA = new ChatFolder
        {
            Id = NewId(),
            OwnerId = actorId,
            Name = "ChildA",
            ParentFolderId = rootA.Id,
        };
        var grandA = new ChatFolder
        {
            Id = NewId(),
            OwnerId = actorId,
            Name = "GrandA",
            ParentFolderId = childA.Id,
        };

        // RootB -> ChildB (depths 0/1)
        var rootB = new ChatFolder
        {
            Id = NewId(),
            OwnerId = actorId,
            Name = "RootB",
        };
        var childB = new ChatFolder
        {
            Id = NewId(),
            OwnerId = actorId,
            Name = "ChildB",
            ParentFolderId = rootB.Id,
        };

        folderRepo.Folders.AddRange([rootA, childA, grandA, rootB, childB]);

        // Moving ChildA (which still has GrandA beneath it) under ChildB would push
        // GrandA to depth 3 — must be rejected even though ChildB itself is only depth 1.
        var result = await service.UpdateAsync(
            childA.Id,
            new UpdateChatFolderRequest("ChildA", childB.Id),
            actorId
        );

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Chat.FolderMaxDepth, result.Error.Code);
    }
}
