namespace HydraForge.Application.Tests.Specs;

using HydraForge.Application.Audit;
using HydraForge.Application.Cards;
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
        var (specRepo, cardRepo, memberRepo, auditWriter) = CreateMocks();
        var service = new SpecService(specRepo, cardRepo, memberRepo, auditWriter);
        var projectId = NewId();
        var actorId = NewId();

        memberRepo.Add(new ProjectMember { ProjectId = projectId, UserId = actorId, Role = MemberRole.Member });

        var result = await service.CreateAsync(new CreateSpecCommand(projectId, actorId, "Spec Title", "Desc", "# Hello"));

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
        var (specRepo, cardRepo, memberRepo, auditWriter) = CreateMocks();
        var service = new SpecService(specRepo, cardRepo, memberRepo, auditWriter);
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
        var (specRepo, cardRepo, memberRepo, auditWriter) = CreateMocks();
        var service = new SpecService(specRepo, cardRepo, memberRepo, auditWriter);
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
        var (specRepo, cardRepo, memberRepo, auditWriter) = CreateMocks();
        var service = new SpecService(specRepo, cardRepo, memberRepo, auditWriter);
        var projectId = NewId();
        var actorId = NewId();

        memberRepo.Add(new ProjectMember { ProjectId = projectId, UserId = actorId, Role = MemberRole.Member });
        var largeContent = new string('x', 1_000_001);

        var result = await service.CreateAsync(new CreateSpecCommand(projectId, actorId, "Big", null, largeContent));

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Specs.MarkdownPayloadTooLarge, result.Error.Code);
    }

    [Fact]
    public async Task LinkToCardAsync_ValidatesSameProjectAndMembership()
    {
        var (specRepo, cardRepo, memberRepo, auditWriter) = CreateMocks();
        var service = new SpecService(specRepo, cardRepo, memberRepo, auditWriter);
        var projectId = NewId();
        var actorId = NewId();
        var specId = NewId();
        var cardId = NewId();

        specRepo.Add(new Spec { Id = specId, ProjectId = projectId, Title = "S", Content = "", Version = 1, CreatedByUserId = actorId });
        cardRepo.Add(new Card { Id = cardId, ProjectId = projectId, ColumnId = NewId(), Title = "C", CardNumber = 1 });
        memberRepo.Add(new ProjectMember { ProjectId = projectId, UserId = actorId, Role = MemberRole.Member });

        var result = await service.LinkToCardAsync(new LinkSpecToCardCommand(projectId, specId, cardId, actorId));

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task LinkToCardAsync_CardInDifferentProject_ReturnsError()
    {
        var (specRepo, cardRepo, memberRepo, auditWriter) = CreateMocks();
        var service = new SpecService(specRepo, cardRepo, memberRepo, auditWriter);
        var projectId = NewId();
        var otherProjectId = NewId();
        var actorId = NewId();
        var specId = NewId();
        var cardId = NewId();

        specRepo.Add(new Spec { Id = specId, ProjectId = projectId, Title = "S", Content = "", Version = 1, CreatedByUserId = actorId });
        cardRepo.Add(new Card { Id = cardId, ProjectId = otherProjectId, ColumnId = NewId(), Title = "C", CardNumber = 1 });
        memberRepo.Add(new ProjectMember { ProjectId = projectId, UserId = actorId, Role = MemberRole.Member });

        var result = await service.LinkToCardAsync(new LinkSpecToCardCommand(projectId, specId, cardId, actorId));

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Specs.CardDocumentProjectMismatch, result.Error.Code);
    }

    [Fact]
    public async Task UnlinkFromCardAsync_RemovesAssociation()
    {
        var (specRepo, cardRepo, memberRepo, auditWriter) = CreateMocks();
        var service = new SpecService(specRepo, cardRepo, memberRepo, auditWriter);
        var projectId = NewId();
        var actorId = NewId();
        var specId = NewId();
        var cardId = NewId();

        specRepo.Add(new Spec { Id = specId, ProjectId = projectId, Title = "S", Content = "", Version = 1, CreatedByUserId = actorId });
        cardRepo.Add(new Card { Id = cardId, ProjectId = projectId, ColumnId = NewId(), SpecId = specId, Title = "C", CardNumber = 1 });
        memberRepo.Add(new ProjectMember { ProjectId = projectId, UserId = actorId, Role = MemberRole.Member });

        var result = await service.UnlinkFromCardAsync(new UnlinkSpecFromCardCommand(projectId, specId, cardId, actorId));

        Assert.True(result.IsSuccess);
        Assert.Null(cardRepo.Cards.First().SpecId);
    }

    private static (
        InMemorySpecRepository specRepo,
        InMemoryCardRepository cardRepo,
        InMemoryProjectMemberRepository memberRepo,
        InMemoryAuditLogWriter auditWriter
    ) CreateMocks()
    {
        var cardRepo = new InMemoryCardRepository();
        return (
            new InMemorySpecRepository(cardRepo),
            cardRepo,
            new InMemoryProjectMemberRepository(),
            new InMemoryAuditLogWriter()
        );
    }
}

internal class InMemorySpecRepository : ISpecRepository
{
    private readonly InMemoryCardRepository _cardRepo;
    public List<Spec> Specs { get; } = [];
    public List<SpecVersion> Versions { get; } = [];

    public InMemorySpecRepository(InMemoryCardRepository cardRepo) => _cardRepo = cardRepo;

    public Task<Spec?> GetByIdAsync(Guid specId, CancellationToken ct = default)
        => Task.FromResult(Specs.FirstOrDefault(s => s.Id == specId));

    public Task<IReadOnlyList<Spec>> ListByProjectAsync(Guid projectId, SpecListFilter filter, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Spec>>(Specs.Where(s => s.ProjectId == projectId).ToList());

    public Task<SpecVersion?> GetVersionAsync(Guid specId, int version, CancellationToken ct = default)
        => Task.FromResult(Versions.FirstOrDefault(v => v.SpecId == specId && v.Version == version));

    public Task<IReadOnlyList<SpecVersion>> ListVersionsAsync(Guid specId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<SpecVersion>>(Versions.Where(v => v.SpecId == specId).OrderBy(v => v.Version).ToList());

    public Task<Guid?> GetLinkedCardIdAsync(Guid specId, CancellationToken ct = default)
        => Task.FromResult<Guid?>(null);

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

internal class InMemoryCardRepository : ICardRepository
{
    public List<Card> Cards { get; } = [];

    public Task<Card?> GetByIdAsync(Guid cardId, CancellationToken ct = default)
        => Task.FromResult(Cards.FirstOrDefault(c => c.Id == cardId));

    public Task<IReadOnlyDictionary<Guid, Card>> GetByIdsAsync(IReadOnlyList<Guid> cardIds, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyDictionary<Guid, Card>>(Cards.Where(c => cardIds.Contains(c.Id)).ToDictionary(c => c.Id));

    public Task<Card?> GetByProjectAndNumberAsync(Guid projectId, int cardNumber, CancellationToken ct = default)
        => Task.FromResult(Cards.FirstOrDefault(c => c.ProjectId == projectId && c.CardNumber == cardNumber));

    public Task<IReadOnlyList<Card>> ListByProjectAsync(Guid projectId, CardListFilter filter, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Card>>(Cards.Where(c => c.ProjectId == projectId).ToList());

    public Task<int> GetMaxCardNumberAsync(Guid projectId, CancellationToken ct = default)
        => Task.FromResult(Cards.Where(c => c.ProjectId == projectId).Select(c => c.CardNumber).DefaultIfEmpty(0).Max());

    public Task AddAsync(Card card, CancellationToken ct = default) { Cards.Add(card); return Task.CompletedTask; }
    public void Add(Card card) => AddAsync(card).GetAwaiter().GetResult();
    public Task UpdateAsync(Card card, CancellationToken ct = default)
    {
        var idx = Cards.FindIndex(c => c.Id == card.Id);
        if (idx >= 0) Cards[idx] = card;
        return Task.CompletedTask;
    }
    public Task UpdateRangeAsync(IReadOnlyList<Card> cards, CancellationToken ct = default) { foreach (var c in cards) { var idx = Cards.FindIndex(x => x.Id == c.Id); if (idx >= 0) Cards[idx] = c; } return Task.CompletedTask; }
    public Task DeleteAsync(Guid cardId, CancellationToken ct = default) { Cards.RemoveAll(c => c.Id == cardId); return Task.CompletedTask; }
    public Task CompactColumnPositionsAsync(Guid columnId, int exceptPosition, CancellationToken ct = default)
    {
        var toCompact = Cards.Where(c => c.ColumnId == columnId && c.Position > exceptPosition && c.ArchivedAt == null).ToList();
        foreach (var c in toCompact) c.Position -= 1;
        return Task.CompletedTask;
    }
    public Task<int> CountByColumnIdAsync(Guid columnId, CancellationToken ct = default)
        => Task.FromResult(Cards.Count(c => c.ColumnId == columnId && c.ArchivedAt == null));
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
