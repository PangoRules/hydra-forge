namespace HydraForge.Application.Tests.ProjectDocuments;

using HydraForge.Application.Audit;
using HydraForge.Application.Auth;
using HydraForge.Application.Projects;
using HydraForge.Application.ProjectDocuments;
using HydraForge.Application.ProjectSnapshots;
using HydraForge.Application.Realtime;
using HydraForge.Domain.Common;
using HydraForge.Domain.Entities.Auth;
using HydraForge.Domain.Entities.ProjectSpace;
using HydraForge.Domain.Enums;

public class ProjectDocumentServiceTests
{
    private static Guid NewId() => Guid.NewGuid();

    // ─── CreateAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_NonMember_ReturnsMembershipDenied()
    {
        var (docRepo, memberRepo, userRepo, auditWriter, snapshotRefresher, publisher) =
            CreateMocks();
        var service = new ProjectDocumentService(
            docRepo, memberRepo, userRepo, auditWriter, snapshotRefresher, publisher);
        var projectId = NewId();
        var actorId = NewId();

        var result = await service.CreateAsync(
            new CreateProjectDocumentCommand(projectId, ProjectDocType.Scope, "Doc", null, "C", actorId));

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Projects.MembershipDenied, result.Error.Code);
    }

    [Fact]
    public async Task CreateAsync_Member_Succeeds()
    {
        var (docRepo, memberRepo, userRepo, auditWriter, snapshotRefresher, publisher) =
            CreateMocks();
        var service = new ProjectDocumentService(
            docRepo, memberRepo, userRepo, auditWriter, snapshotRefresher, publisher);
        var projectId = NewId();
        var actorId = NewId();

        memberRepo.Add(new ProjectMember { ProjectId = projectId, UserId = actorId, Role = MemberRole.Member });

        var result = await service.CreateAsync(
            new CreateProjectDocumentCommand(projectId, ProjectDocType.Scope, "Doc Title", "Desc", "# Content", actorId));

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.Version);
        Assert.Single(docRepo.Docs);
        Assert.Single(docRepo.Versions);
        Assert.Equal(1, docRepo.Versions[0].Version);
    }

    [Fact]
    public async Task CreateAsync_UniqueDocType_RejectsDuplicate()
    {
        var (docRepo, memberRepo, userRepo, auditWriter, snapshotRefresher, publisher) =
            CreateMocks();
        var service = new ProjectDocumentService(
            docRepo, memberRepo, userRepo, auditWriter, snapshotRefresher, publisher);
        var projectId = NewId();
        var actorId = NewId();

        memberRepo.Add(new ProjectMember { ProjectId = projectId, UserId = actorId, Role = MemberRole.Member });

        docRepo.Docs.Add(new ProjectDocument
        {
            Id = NewId(),
            ProjectId = projectId,
            DocType = ProjectDocType.Scope,
            Title = "Existing",
            Version = 1,
        });

        var result = await service.CreateAsync(
            new CreateProjectDocumentCommand(projectId, ProjectDocType.Scope, "New", null, "C", actorId));

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.ProjectDocuments.DuplicateDocType, result.Error.Code);
    }

    [Fact]
    public async Task CreateAsync_Reference_AllowsMultiple()
    {
        var (docRepo, memberRepo, userRepo, auditWriter, snapshotRefresher, publisher) =
            CreateMocks();
        var service = new ProjectDocumentService(
            docRepo, memberRepo, userRepo, auditWriter, snapshotRefresher, publisher);
        var projectId = NewId();
        var actorId = NewId();

        memberRepo.Add(new ProjectMember { ProjectId = projectId, UserId = actorId, Role = MemberRole.Member });

        docRepo.Docs.Add(new ProjectDocument
        {
            Id = NewId(),
            ProjectId = projectId,
            DocType = ProjectDocType.Reference,
            Title = "First Ref",
            Version = 1,
        });

        var result = await service.CreateAsync(
            new CreateProjectDocumentCommand(projectId, ProjectDocType.Reference, "Second Ref", null, "C", actorId));

        Assert.True(result.IsSuccess);
        Assert.Equal(2, docRepo.Docs.Count(d => d.ProjectId == projectId));
    }

    [Fact]
    public async Task CreateAsync_ContentTooLarge_ReturnsError()
    {
        var (docRepo, memberRepo, userRepo, auditWriter, snapshotRefresher, publisher) =
            CreateMocks();
        var service = new ProjectDocumentService(
            docRepo, memberRepo, userRepo, auditWriter, snapshotRefresher, publisher);
        var projectId = NewId();
        var actorId = NewId();

        memberRepo.Add(new ProjectMember { ProjectId = projectId, UserId = actorId, Role = MemberRole.Member });

        var largeContent = new string('x', 1_000_001);

        var result = await service.CreateAsync(
            new CreateProjectDocumentCommand(projectId, ProjectDocType.Scope, "Big", null, largeContent, actorId));

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.ProjectDocuments.MarkdownPayloadTooLarge, result.Error.Code);
    }

    // ─── GetByIdAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetByIdAsync_NotFound_ReturnsNotFound()
    {
        var (docRepo, memberRepo, userRepo, auditWriter, snapshotRefresher, publisher) =
            CreateMocks();
        var service = new ProjectDocumentService(
            docRepo, memberRepo, userRepo, auditWriter, snapshotRefresher, publisher);
        var projectId = NewId();
        var actorId = NewId();

        memberRepo.Add(new ProjectMember { ProjectId = projectId, UserId = actorId, Role = MemberRole.Member });

        var result = await service.GetByIdAsync(projectId, NewId(), actorId);

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.ProjectDocuments.NotFound, result.Error.Code);
    }

    [Fact]
    public async Task GetByIdAsync_WrongProject_ReturnsNotFound()
    {
        var (docRepo, memberRepo, userRepo, auditWriter, snapshotRefresher, publisher) =
            CreateMocks();
        var service = new ProjectDocumentService(
            docRepo, memberRepo, userRepo, auditWriter, snapshotRefresher, publisher);
        var projectId = NewId();
        var wrongProjectId = NewId();
        var actorId = NewId();
        var docId = NewId();

        memberRepo.Add(new ProjectMember { ProjectId = projectId, UserId = actorId, Role = MemberRole.Member });
        docRepo.Docs.Add(new ProjectDocument
        {
            Id = docId,
            ProjectId = wrongProjectId,
            DocType = ProjectDocType.Scope,
            Title = "Doc",
            Version = 1,
        });

        var result = await service.GetByIdAsync(projectId, docId, actorId);

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.ProjectDocuments.NotFound, result.Error.Code);
    }

    [Fact]
    public async Task GetByIdAsync_Success_ReturnsDocument()
    {
        var (docRepo, memberRepo, userRepo, auditWriter, snapshotRefresher, publisher) =
            CreateMocks();
        var service = new ProjectDocumentService(
            docRepo, memberRepo, userRepo, auditWriter, snapshotRefresher, publisher);
        var projectId = NewId();
        var actorId = NewId();
        var docId = NewId();

        memberRepo.Add(new ProjectMember { ProjectId = projectId, UserId = actorId, Role = MemberRole.Member });
        docRepo.Docs.Add(new ProjectDocument
        {
            Id = docId,
            ProjectId = projectId,
            DocType = ProjectDocType.Scope,
            Title = "Doc",
            Version = 1,
        });

        var result = await service.GetByIdAsync(projectId, docId, actorId);

        Assert.True(result.IsSuccess);
        Assert.Equal(docId, result.Value.Id);
    }

    // ─── ListAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task ListAsync_ReturnsOnlyNonArchived()
    {
        var (docRepo, memberRepo, userRepo, auditWriter, snapshotRefresher, publisher) =
            CreateMocks();
        var service = new ProjectDocumentService(
            docRepo, memberRepo, userRepo, auditWriter, snapshotRefresher, publisher);
        var projectId = NewId();
        var actorId = NewId();

        memberRepo.Add(new ProjectMember { ProjectId = projectId, UserId = actorId, Role = MemberRole.Member });

        docRepo.Docs.Add(new ProjectDocument
        {
            Id = NewId(),
            ProjectId = projectId,
            DocType = ProjectDocType.Scope,
            Title = "Active",
            Version = 1,
        });
        docRepo.Docs.Add(new ProjectDocument
        {
            Id = NewId(),
            ProjectId = projectId,
            DocType = ProjectDocType.Glossary,
            Title = "Archived",
            Version = 1,
            ArchivedAt = DateTime.UtcNow,
        });

        var result = await service.ListAsync(projectId, actorId);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value);
        Assert.Equal("Active", result.Value[0].Title);
    }

    // ─── UpdateAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateAsync_Success_IncrementsVersion()
    {
        var (docRepo, memberRepo, userRepo, auditWriter, snapshotRefresher, publisher) =
            CreateMocks();
        var service = new ProjectDocumentService(
            docRepo, memberRepo, userRepo, auditWriter, snapshotRefresher, publisher);
        var projectId = NewId();
        var actorId = NewId();
        var docId = NewId();

        memberRepo.Add(new ProjectMember { ProjectId = projectId, UserId = actorId, Role = MemberRole.Member });
        docRepo.Docs.Add(new ProjectDocument
        {
            Id = docId,
            ProjectId = projectId,
            DocType = ProjectDocType.Scope,
            Title = "Original",
            Content = "V1",
            Version = 1,
        });
        docRepo.Versions.Add(new ProjectDocumentVersion
        {
            Id = NewId(),
            ProjectDocumentId = docId,
            Version = 1,
            Title = "Original",
            Content = "V1",
        });

        var result = await service.UpdateAsync(
            new UpdateProjectDocumentCommand(projectId, docId, "Updated", null, "V2", actorId));

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Version);
        Assert.Equal(2, docRepo.Versions.Count);
    }

    [Fact]
    public async Task UpdateAsync_ContentTooLarge_ReturnsError()
    {
        var (docRepo, memberRepo, userRepo, auditWriter, snapshotRefresher, publisher) =
            CreateMocks();
        var service = new ProjectDocumentService(
            docRepo, memberRepo, userRepo, auditWriter, snapshotRefresher, publisher);
        var projectId = NewId();
        var actorId = NewId();
        var docId = NewId();

        memberRepo.Add(new ProjectMember { ProjectId = projectId, UserId = actorId, Role = MemberRole.Member });
        docRepo.Docs.Add(new ProjectDocument
        {
            Id = docId,
            ProjectId = projectId,
            DocType = ProjectDocType.Scope,
            Title = "Doc",
            Version = 1,
        });

        var largeContent = new string('x', 1_000_001);

        var result = await service.UpdateAsync(
            new UpdateProjectDocumentCommand(projectId, docId, "Big", null, largeContent, actorId));

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.ProjectDocuments.MarkdownPayloadTooLarge, result.Error.Code);
    }

    // ─── RestoreVersionAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task RestoreVersionAsync_Success_CreatesNewVersionWithOldContent()
    {
        var (docRepo, memberRepo, userRepo, auditWriter, snapshotRefresher, publisher) =
            CreateMocks();
        var service = new ProjectDocumentService(
            docRepo, memberRepo, userRepo, auditWriter, snapshotRefresher, publisher);
        var projectId = NewId();
        var actorId = NewId();
        var docId = NewId();

        memberRepo.Add(new ProjectMember { ProjectId = projectId, UserId = actorId, Role = MemberRole.Member });
        docRepo.Docs.Add(new ProjectDocument
        {
            Id = docId,
            ProjectId = projectId,
            DocType = ProjectDocType.Scope,
            Title = "Doc",
            Content = "V2",
            Version = 2,
        });
        docRepo.Versions.Add(new ProjectDocumentVersion
        {
            Id = NewId(),
            ProjectDocumentId = docId,
            Version = 1,
            Title = "Doc",
            Content = "V1",
        });
        docRepo.Versions.Add(new ProjectDocumentVersion
        {
            Id = NewId(),
            ProjectDocumentId = docId,
            Version = 2,
            Title = "Doc",
            Content = "V2",
        });

        var result = await service.RestoreVersionAsync(
            new RestoreProjectDocumentVersionCommand(projectId, docId, 1, actorId));

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value.Version);
        Assert.Equal("V1", result.Value.Content);
        Assert.Equal(3, docRepo.Versions.Count);
    }

    [Fact]
    public async Task RestoreVersionAsync_VersionNotFound_ReturnsError()
    {
        var (docRepo, memberRepo, userRepo, auditWriter, snapshotRefresher, publisher) =
            CreateMocks();
        var service = new ProjectDocumentService(
            docRepo, memberRepo, userRepo, auditWriter, snapshotRefresher, publisher);
        var projectId = NewId();
        var actorId = NewId();
        var docId = NewId();

        memberRepo.Add(new ProjectMember { ProjectId = projectId, UserId = actorId, Role = MemberRole.Member });
        docRepo.Docs.Add(new ProjectDocument
        {
            Id = docId,
            ProjectId = projectId,
            DocType = ProjectDocType.Scope,
            Title = "Doc",
            Version = 1,
        });

        var result = await service.RestoreVersionAsync(
            new RestoreProjectDocumentVersionCommand(projectId, docId, 99, actorId));

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.ProjectDocuments.DocumentVersionNotFound, result.Error.Code);
    }

    // ─── Mocks ──────────────────────────────────────────────────────────────

    private static (
        InMemoryProjectDocumentRepository docRepo,
        InMemoryProjectMemberRepository memberRepo,
        FakeUserRepositoryForAdmin userRepo,
        InMemoryAuditLogWriter auditWriter,
        NullSnapshotRefresher snapshotRefresher,
        FakeProjectBoardEventPublisher publisher
    ) CreateMocks()
    {
        return (
            new InMemoryProjectDocumentRepository(),
            new InMemoryProjectMemberRepository(),
            new FakeUserRepositoryForAdmin(),
            new InMemoryAuditLogWriter(),
            new NullSnapshotRefresher(),
            new FakeProjectBoardEventPublisher()
        );
    }
}

internal sealed class FakeUserRepositoryForAdmin : IUserRepository
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

    public Task CreateAsync(User user, CancellationToken ct = default) =>
        throw new NotImplementedException();

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

internal class InMemoryProjectDocumentRepository : IProjectDocumentRepository
{
    public List<ProjectDocument> Docs { get; } = [];
    public List<ProjectDocumentVersion> Versions { get; } = [];

    public Task<ProjectDocument?> GetByIdAsync(Guid documentId, CancellationToken ct = default) =>
        Task.FromResult(Docs.FirstOrDefault(d => d.Id == documentId));

    public Task<IReadOnlyList<ProjectDocument>> ListByProjectAsync(Guid projectId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<ProjectDocument>>([.. Docs.Where(d => d.ProjectId == projectId && d.ArchivedAt == null)]);

    public Task<ProjectDocument?> GetByDocTypeAsync(Guid projectId, ProjectDocType docType, CancellationToken ct = default) =>
        Task.FromResult(Docs.FirstOrDefault(d => d.ProjectId == projectId && d.DocType == docType));

    public Task<ProjectDocumentVersion?> GetVersionAsync(Guid documentId, int version, CancellationToken ct = default) =>
        Task.FromResult(Versions.FirstOrDefault(v => v.ProjectDocumentId == documentId && v.Version == version));

    public Task<IReadOnlyList<ProjectDocumentVersion>> ListVersionsAsync(Guid documentId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<ProjectDocumentVersion>>([.. Versions.Where(v => v.ProjectDocumentId == documentId).OrderBy(v => v.Version)]);

    public Task AddAsync(ProjectDocument document, CancellationToken ct = default)
    {
        Docs.Add(document);
        return Task.CompletedTask;
    }

    public Task AddVersionAsync(ProjectDocumentVersion version, CancellationToken ct = default)
    {
        Versions.Add(version);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(ProjectDocument document, CancellationToken ct = default)
    {
        var idx = Docs.FindIndex(d => d.Id == document.Id);
        if (idx >= 0)
            Docs[idx] = document;
        return Task.CompletedTask;
    }

    public Task<int> SaveChangesAsync(CancellationToken ct = default) => Task.FromResult(1);
}

internal class InMemoryProjectMemberRepository : IProjectMemberRepository
{
    public List<ProjectMember> Members { get; } = [];

    public Task<ProjectMember?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(Members.FirstOrDefault(m => m.Id == id));

    public Task<ProjectMember?> GetByProjectAndUserAsync(Guid projectId, Guid userId, CancellationToken ct = default) =>
        Task.FromResult(Members.FirstOrDefault(m => m.ProjectId == projectId && m.UserId == userId));

    public Task<IReadOnlyList<ProjectMember>> ListMembersAsync(Guid projectId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<ProjectMember>>([.. Members.Where(m => m.ProjectId == projectId)]);

    public Task<IReadOnlyDictionary<Guid, int>> GetMemberCountsAsync(IEnumerable<Guid> projectIds, CancellationToken ct = default)
    {
        var idList = projectIds.ToList();
        var counts = Members.Where(m => idList.Contains(m.ProjectId)).GroupBy(m => m.ProjectId).ToDictionary(g => g.Key, g => g.Count());
        return Task.FromResult<IReadOnlyDictionary<Guid, int>>(counts);
    }

    public Task<IReadOnlyDictionary<Guid, MemberRole>> GetRolesByProjectAndUserAsync(IEnumerable<Guid> projectIds, Guid userId, CancellationToken ct = default)
    {
        var idList = projectIds.ToList();
        var roles = Members.Where(m => idList.Contains(m.ProjectId) && m.UserId == userId).ToDictionary(m => m.ProjectId, m => m.Role);
        return Task.FromResult<IReadOnlyDictionary<Guid, MemberRole>>(roles);
    }

    public Task AddMemberAsync(ProjectMember member, CancellationToken ct = default) { Members.Add(member); return Task.CompletedTask; }
    public void Add(ProjectMember member) => AddMemberAsync(member).GetAwaiter().GetResult();
    public Task UpdateMemberAsync(ProjectMember member, CancellationToken ct = default)
    { var idx = Members.FindIndex(m => m.Id == member.Id); if (idx >= 0) Members[idx] = member; return Task.CompletedTask; }
    public Task RemoveMemberAsync(Guid id, CancellationToken ct = default) { Members.RemoveAll(m => m.Id == id); return Task.CompletedTask; }
}

internal class InMemoryAuditLogWriter : IAuditLogWriter
{
    public List<AuditLogRequest> Writes { get; } = [];

    public Task<Result> WriteAsync(AuditLogRequest request, CancellationToken ct = default)
    { Writes.Add(request); return Task.FromResult(Result.Success()); }

    public void Clear() => Writes.Clear();
}
