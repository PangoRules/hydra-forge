namespace HydraForge.Application.Tests.Specs;

using HydraForge.Application.Audit;
using HydraForge.Application.Projects;
using HydraForge.Application.Specs;
using HydraForge.Domain.Common;
using HydraForge.Domain.Entities.Auth;
using HydraForge.Domain.Entities.ProjectSpace;
using HydraForge.Domain.Enums;
using Result = HydraForge.Domain.Common.Result;

public class SpecServiceTests
{
    private static Guid NewId() => Guid.NewGuid();

    [Fact]
    public async Task CreateAsync_CreatesSpecAndVersion1InSameTransaction()
    {
        var (specRepo, memberRepo, auditWriter, snapshotRefresher, publisher) = CreateMocks();
        var service = new SpecService(specRepo, memberRepo, auditWriter, snapshotRefresher, publisher);
        var projectId = NewId();
        var cardId = NewId();
        var actorId = NewId();

        memberRepo.Add(new ProjectMember { ProjectId = projectId, UserId = actorId, Role = MemberRole.Member });

        var result = await service.CreateAsync(new CreateSpecCommand(projectId, cardId, actorId, "Spec Title", "Desc", "# Hello"));

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.Version);
        Assert.Single(specRepo.Specs);
        Assert.Single(specRepo.Versions);
        Assert.Equal(1, specRepo.Versions[0].Version);
        Assert.Equal("# Hello", specRepo.Versions[0].Content);
    }

    [Fact]
    public async Task UpdateAsync_IncrementsVersionAndWritesImmutableSnapshot()
    {
        var (specRepo, memberRepo, auditWriter, snapshotRefresher, publisher) = CreateMocks();
        var service = new SpecService(specRepo, memberRepo, auditWriter, snapshotRefresher, publisher);
        var projectId = NewId();
        var actorId = NewId();
        var specId = NewId();

        var spec = new Spec { Id = specId, ProjectId = projectId, Title = "Original", Content = "V1", Version = 1, CreatedByUserId = actorId };
        specRepo.Add(spec);
        specRepo.AddVersion(new SpecVersion { Id = NewId(), SpecId = specId, Version = 1, Content = "V1", CreatedByUserId = actorId });
        memberRepo.Add(new ProjectMember { ProjectId = projectId, UserId = actorId, Role = MemberRole.Member });

        var result = await service.UpdateAsync(new UpdateSpecCommand(projectId, specId, actorId, "Updated", null, "V2"));

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Version);
        Assert.Equal(2, specRepo.Versions.Count);
        var v2 = specRepo.Versions.Last();
        Assert.Equal(2, v2.Version);
        Assert.Equal("V2", v2.Content);
        var v1 = specRepo.Versions.First();
        Assert.Equal(1, v1.Version);
        Assert.Equal("V1", v1.Content);
    }

    [Fact]
    public async Task RestoreVersionAsync_CopiesOldVersionContentIntoCurrentAndWritesNewVersion()
    {
        var (specRepo, memberRepo, auditWriter, snapshotRefresher, publisher) = CreateMocks();
        var service = new SpecService(specRepo, memberRepo, auditWriter, snapshotRefresher, publisher);
        var projectId = NewId();
        var actorId = NewId();
        var specId = NewId();

        var spec = new Spec { Id = specId, ProjectId = projectId, Title = "Spec", Content = "V2", Version = 2, CreatedByUserId = actorId };
        specRepo.Add(spec);
        specRepo.AddVersion(new SpecVersion { Id = NewId(), SpecId = specId, Version = 1, Content = "V1", CreatedByUserId = actorId });
        specRepo.AddVersion(new SpecVersion { Id = NewId(), SpecId = specId, Version = 2, Content = "V2", CreatedByUserId = actorId });
        memberRepo.Add(new ProjectMember { ProjectId = projectId, UserId = actorId, Role = MemberRole.Member });

        var result = await service.RestoreVersionAsync(new RestoreSpecVersionCommand(projectId, specId, 1, actorId));

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value.Version);
        Assert.Equal("V1", result.Value.Content);
        Assert.Equal(3, specRepo.Versions.Count);
        var newVer = specRepo.Versions.Last();
        Assert.Equal(3, newVer.Version);
        Assert.Equal("V1", newVer.Content);
    }

    [Fact]
    public async Task CreateAsync_MarkdownPayloadTooLarge_ReturnsError()
    {
        var (specRepo, memberRepo, auditWriter, snapshotRefresher, publisher) = CreateMocks();
        var service = new SpecService(specRepo, memberRepo, auditWriter, snapshotRefresher, publisher);
        var projectId = NewId();
        var cardId = NewId();
        var actorId = NewId();

        memberRepo.Add(new ProjectMember { ProjectId = projectId, UserId = actorId, Role = MemberRole.Member });
        var largeContent = new string('x', 1_000_001);

        var result = await service.CreateAsync(new CreateSpecCommand(projectId, cardId, actorId, "Big", null, largeContent));

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Specs.MarkdownPayloadTooLarge, result.Error.Code);
    }

    private static (
        InMemorySpecRepository specRepo,
        InMemoryProjectMemberRepository memberRepo,
        InMemoryAuditLogWriter auditWriter,
        NullSnapshotRefresher snapshotRefresher,
        FakeProjectBoardEventPublisher publisher
    ) CreateMocks()
    {
        return (
            new InMemorySpecRepository(),
            new InMemoryProjectMemberRepository(),
            new InMemoryAuditLogWriter(),
            new NullSnapshotRefresher(),
            new FakeProjectBoardEventPublisher()
        );
    }
}

internal class InMemorySpecRepository : ISpecRepository
{
    public List<Spec> Specs { get; } = [];
    public List<SpecVersion> Versions { get; } = [];

    public Task<Spec?> GetByIdAsync(Guid specId, CancellationToken ct = default)
        => Task.FromResult(Specs.FirstOrDefault(s => s.Id == specId));

    public Task<IReadOnlyList<Spec>> ListByProjectAsync(Guid projectId, SpecListFilter filter, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Spec>>(Specs.Where(s => s.ProjectId == projectId).ToList());

    public Task<SpecVersion?> GetVersionAsync(Guid specId, int version, CancellationToken ct = default)
        => Task.FromResult(Versions.FirstOrDefault(v => v.SpecId == specId && v.Version == version));

    public Task<IReadOnlyList<SpecVersion>> ListVersionsAsync(Guid specId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<SpecVersion>>(Versions.Where(v => v.SpecId == specId).OrderBy(v => v.Version).ToList());

    public Task<IReadOnlyList<Spec>> ListByCardAsync(Guid cardId, SpecListFilter filter, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Spec>>(Specs.Where(s => s.CardId == cardId).ToList());

    public Task AddAsync(Spec spec, CancellationToken ct = default) { Specs.Add(spec); return Task.CompletedTask; }
    public void Add(Spec spec) => AddAsync(spec).GetAwaiter().GetResult();
    public Task AddVersionAsync(SpecVersion version, CancellationToken ct = default) { Versions.Add(version); return Task.CompletedTask; }
    public void AddVersion(SpecVersion version) => AddVersionAsync(version).GetAwaiter().GetResult();
    public Task UpdateAsync(Spec spec, CancellationToken ct = default)
    {
        var idx = Specs.FindIndex(s => s.Id == spec.Id);
        if (idx >= 0) Specs[idx] = spec;
        return Task.CompletedTask;
    }
    public Task<int> SaveChangesAsync(CancellationToken ct = default) => Task.FromResult(1);
}

internal class InMemoryProjectMemberRepository : IProjectMemberRepository
{
    public List<ProjectMember> Members { get; } = [];

    public Task<ProjectMember?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(Members.FirstOrDefault(m => m.Id == id));
    public Task<ProjectMember?> GetByProjectAndUserAsync(Guid projectId, Guid userId, CancellationToken ct = default)
        => Task.FromResult(Members.FirstOrDefault(m => m.ProjectId == projectId && m.UserId == userId));
    public Task<IReadOnlyList<ProjectMember>> ListMembersAsync(Guid projectId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<ProjectMember>>(Members.Where(m => m.ProjectId == projectId).ToList());
    public Task<IReadOnlyDictionary<Guid, int>> GetMemberCountsAsync(IEnumerable<Guid> projectIds, CancellationToken ct = default)
    {
        var idList = projectIds.ToList();
        var counts = Members.Where(m => idList.Contains(m.ProjectId)).GroupBy(m => m.ProjectId).ToDictionary(g => g.Key, g => g.Count());
        return Task.FromResult<IReadOnlyDictionary<Guid, int>>(counts);
    }
    public Task AddMemberAsync(ProjectMember member, CancellationToken ct = default) { Members.Add(member); return Task.CompletedTask; }
    public void Add(ProjectMember member) => AddMemberAsync(member).GetAwaiter().GetResult();
    public Task UpdateMemberAsync(ProjectMember member, CancellationToken ct = default)
    {
        var idx = Members.FindIndex(m => m.Id == member.Id);
        if (idx >= 0) Members[idx] = member;
        return Task.CompletedTask;
    }
    public Task RemoveMemberAsync(Guid id, CancellationToken ct = default) { Members.RemoveAll(m => m.Id == id); return Task.CompletedTask; }
}

internal class InMemoryAuditLogWriter : IAuditLogWriter
{
    public Task<Result> WriteAsync(AuditLogRequest request, CancellationToken ct = default) => Task.FromResult(Result.Success());
}
