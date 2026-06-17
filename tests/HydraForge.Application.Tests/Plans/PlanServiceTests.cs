namespace HydraForge.Application.Tests.Plans;

using HydraForge.Application.Audit;
using HydraForge.Application.Cards;
using HydraForge.Application.Plans;
using HydraForge.Application.Projects;
using HydraForge.Domain.Common;
using HydraForge.Domain.Entities.Auth;
using HydraForge.Domain.Entities.ProjectSpace;
using HydraForge.Domain.Enums;
using Result = HydraForge.Domain.Common.Result;

public class PlanServiceTests
{
    private static Guid NewId() => Guid.NewGuid();

    [Fact]
    public async Task CreateAsync_CreatesPlanAndVersion1InSameTransaction()
    {
        var (planRepo, cardRepo, memberRepo, auditWriter) = CreateMocks();
        var service = new PlanService(planRepo, cardRepo, memberRepo, auditWriter);
        var projectId = NewId();
        var actorId = NewId();

        memberRepo.Add(new ProjectMember { ProjectId = projectId, UserId = actorId, Role = MemberRole.Member });

        var result = await service.CreateAsync(new CreatePlanCommand(projectId, actorId, "Plan Title", "Desc", "# Plan"));

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.Version);
        Assert.Single(planRepo.Plans);
        Assert.Single(planRepo.Versions);
        Assert.Equal(1, planRepo.Versions[0].Version);
        Assert.Equal("# Plan", planRepo.Versions[0].Content);
    }

    [Fact]
    public async Task UpdateAsync_IncrementsVersionAndWritesImmutableSnapshot()
    {
        var (planRepo, cardRepo, memberRepo, auditWriter) = CreateMocks();
        var service = new PlanService(planRepo, cardRepo, memberRepo, auditWriter);
        var projectId = NewId();
        var actorId = NewId();
        var planId = NewId();

        var plan = new Plan { Id = planId, ProjectId = projectId, Title = "Original", Content = "V1", Version = 1, CreatedByUserId = actorId };
        planRepo.Add(plan);
        planRepo.AddVersion(new PlanVersion { Id = NewId(), PlanId = planId, Version = 1, Content = "V1", CreatedByUserId = actorId });
        memberRepo.Add(new ProjectMember { ProjectId = projectId, UserId = actorId, Role = MemberRole.Member });

        var result = await service.UpdateAsync(new UpdatePlanCommand(projectId, planId, actorId, "Updated", null, "V2"));

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Version);
        Assert.Equal(2, planRepo.Versions.Count);
        var v2 = planRepo.Versions.Last();
        Assert.Equal(2, v2.Version);
        Assert.Equal("V2", v2.Content);
        var v1 = planRepo.Versions.First();
        Assert.Equal(1, v1.Version);
        Assert.Equal("V1", v1.Content);
    }

    [Fact]
    public async Task RestoreVersionAsync_CopiesOldVersionContentIntoCurrentAndWritesNewVersion()
    {
        var (planRepo, cardRepo, memberRepo, auditWriter) = CreateMocks();
        var service = new PlanService(planRepo, cardRepo, memberRepo, auditWriter);
        var projectId = NewId();
        var actorId = NewId();
        var planId = NewId();

        var plan = new Plan { Id = planId, ProjectId = projectId, Title = "Plan", Content = "V2", Version = 2, CreatedByUserId = actorId };
        planRepo.Add(plan);
        planRepo.AddVersion(new PlanVersion { Id = NewId(), PlanId = planId, Version = 1, Content = "V1", CreatedByUserId = actorId });
        planRepo.AddVersion(new PlanVersion { Id = NewId(), PlanId = planId, Version = 2, Content = "V2", CreatedByUserId = actorId });
        memberRepo.Add(new ProjectMember { ProjectId = projectId, UserId = actorId, Role = MemberRole.Member });

        var result = await service.RestoreVersionAsync(new RestorePlanVersionCommand(projectId, planId, 1, actorId));

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value.Version);
        Assert.Equal("V1", result.Value.Content);
        Assert.Equal(3, planRepo.Versions.Count);
        var newVer = planRepo.Versions.Last();
        Assert.Equal(3, newVer.Version);
        Assert.Equal("V1", newVer.Content);
    }

    [Fact]
    public async Task CreateAsync_MarkdownPayloadTooLarge_ReturnsError()
    {
        var (planRepo, cardRepo, memberRepo, auditWriter) = CreateMocks();
        var service = new PlanService(planRepo, cardRepo, memberRepo, auditWriter);
        var projectId = NewId();
        var actorId = NewId();

        memberRepo.Add(new ProjectMember { ProjectId = projectId, UserId = actorId, Role = MemberRole.Member });
        var largeContent = new string('x', 1_000_001);

        var result = await service.CreateAsync(new CreatePlanCommand(projectId, actorId, "Big", null, largeContent));

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Plans.MarkdownPayloadTooLarge, result.Error.Code);
    }

    [Fact]
    public async Task LinkToCardAsync_ValidatesSameProjectAndMembership()
    {
        var (planRepo, cardRepo, memberRepo, auditWriter) = CreateMocks();
        var service = new PlanService(planRepo, cardRepo, memberRepo, auditWriter);
        var projectId = NewId();
        var actorId = NewId();
        var planId = NewId();
        var cardId = NewId();

        planRepo.Add(new Plan { Id = planId, ProjectId = projectId, Title = "P", Content = "", Version = 1, CreatedByUserId = actorId });
        cardRepo.Add(new Card { Id = cardId, ProjectId = projectId, ColumnId = NewId(), Title = "C", CardNumber = 1 });
        memberRepo.Add(new ProjectMember { ProjectId = projectId, UserId = actorId, Role = MemberRole.Member });

        var result = await service.LinkToCardAsync(new LinkPlanToCardCommand(projectId, planId, cardId, actorId));

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task LinkToCardAsync_CardInDifferentProject_ReturnsError()
    {
        var (planRepo, cardRepo, memberRepo, auditWriter) = CreateMocks();
        var service = new PlanService(planRepo, cardRepo, memberRepo, auditWriter);
        var projectId = NewId();
        var otherProjectId = NewId();
        var actorId = NewId();
        var planId = NewId();
        var cardId = NewId();

        planRepo.Add(new Plan { Id = planId, ProjectId = projectId, Title = "P", Content = "", Version = 1, CreatedByUserId = actorId });
        cardRepo.Add(new Card { Id = cardId, ProjectId = otherProjectId, ColumnId = NewId(), Title = "C", CardNumber = 1 });
        memberRepo.Add(new ProjectMember { ProjectId = projectId, UserId = actorId, Role = MemberRole.Member });

        var result = await service.LinkToCardAsync(new LinkPlanToCardCommand(projectId, planId, cardId, actorId));

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Plans.CardDocumentProjectMismatch, result.Error.Code);
    }

    [Fact]
    public async Task UnlinkFromCardAsync_RemovesAssociation()
    {
        var (planRepo, cardRepo, memberRepo, auditWriter) = CreateMocks();
        var service = new PlanService(planRepo, cardRepo, memberRepo, auditWriter);
        var projectId = NewId();
        var actorId = NewId();
        var planId = NewId();
        var cardId = NewId();

        planRepo.Add(new Plan { Id = planId, ProjectId = projectId, Title = "P", Content = "", Version = 1, CreatedByUserId = actorId });
        cardRepo.Add(new Card { Id = cardId, ProjectId = projectId, ColumnId = NewId(), PlanId = planId, Title = "C", CardNumber = 1 });
        memberRepo.Add(new ProjectMember { ProjectId = projectId, UserId = actorId, Role = MemberRole.Member });

        var result = await service.UnlinkFromCardAsync(new UnlinkPlanFromCardCommand(projectId, planId, cardId, actorId));

        Assert.True(result.IsSuccess);
        Assert.Null(cardRepo.Cards.First().PlanId);
    }

    private static (
        InMemoryPlanRepository planRepo,
        InMemoryCardRepository cardRepo,
        InMemoryProjectMemberRepository memberRepo,
        InMemoryAuditLogWriter auditWriter
    ) CreateMocks()
    {
        var cardRepo = new InMemoryCardRepository();
        return (
            new InMemoryPlanRepository(cardRepo),
            cardRepo,
            new InMemoryProjectMemberRepository(),
            new InMemoryAuditLogWriter()
        );
    }
}

internal class InMemoryPlanRepository : IPlanRepository
{
    private readonly InMemoryCardRepository _cardRepo;
    public List<Plan> Plans { get; } = [];
    public List<PlanVersion> Versions { get; } = [];

    public InMemoryPlanRepository(InMemoryCardRepository cardRepo) => _cardRepo = cardRepo;

    public Task<Plan?> GetByIdAsync(Guid planId, CancellationToken ct = default)
        => Task.FromResult(Plans.FirstOrDefault(p => p.Id == planId));

    public Task<IReadOnlyList<Plan>> ListByProjectAsync(Guid projectId, PlanListFilter filter, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Plan>>(Plans.Where(p => p.ProjectId == projectId).ToList());

    public Task<PlanVersion?> GetVersionAsync(Guid planId, int version, CancellationToken ct = default)
        => Task.FromResult(Versions.FirstOrDefault(v => v.PlanId == planId && v.Version == version));

    public Task<IReadOnlyList<PlanVersion>> ListVersionsAsync(Guid planId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<PlanVersion>>(Versions.Where(v => v.PlanId == planId).OrderBy(v => v.Version).ToList());

    public Task<IReadOnlyDictionary<Guid, Guid?>> GetLinkedCardIdsAsync(Guid projectId, CancellationToken ct = default)
    {
        var ids = _cardRepo.Cards
            .Where(c => c.PlanId != null && c.ProjectId == projectId)
            .Select(c => new { c.PlanId, c.Id })
            .ToList();
        return Task.FromResult<IReadOnlyDictionary<Guid, Guid?>>(
            ids.ToDictionary(x => x.PlanId!.Value, x => (Guid?)x.Id)
        );
    }

    public Task<Guid?> GetLinkedCardIdAsync(Guid planId, CancellationToken ct = default)
        => Task.FromResult<Guid?>(null);

    public Task AddAsync(Plan plan, CancellationToken ct = default) { Plans.Add(plan); return Task.CompletedTask; }
    public void Add(Plan plan) => AddAsync(plan).GetAwaiter().GetResult();
    public Task AddVersionAsync(PlanVersion version, CancellationToken ct = default) { Versions.Add(version); return Task.CompletedTask; }
    public void AddVersion(PlanVersion version) => AddVersionAsync(version).GetAwaiter().GetResult();
    public Task UpdateAsync(Plan plan, CancellationToken ct = default)
    {
        var idx = Plans.FindIndex(p => p.Id == plan.Id);
        if (idx >= 0) Plans[idx] = plan;
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
