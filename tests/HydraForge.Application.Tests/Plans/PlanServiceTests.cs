namespace HydraForge.Application.Tests.Plans;

using HydraForge.Application.Audit;
using HydraForge.Application.Auth;
using HydraForge.Application.Cards;
using HydraForge.Application.Plans;
using HydraForge.Application.Projects;
using HydraForge.Application.Specs;
using HydraForge.Domain.Common;
using HydraForge.Domain.Entities.Auth;
using HydraForge.Domain.Entities.ProjectSpace;
using HydraForge.Domain.Enums;

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

internal sealed class FakeUserRepositoryAdmin : IUserRepository
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
        Task.FromResult(true);

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

public class PlanServiceTests
{
    private static Guid NewId() => Guid.NewGuid();

    [Fact]
    public async Task CreateAsync_CreatesPlanAndVersion1InSameTransaction()
    {
        var (
            planRepo,
            cardRepo,
            specRepo,
            memberRepo,
            userRepo,
            auditWriter,
            snapshotRefresher,
            publisher
        ) = CreateMocks();
        var service = new PlanService(
            planRepo,
            cardRepo,
            specRepo,
            memberRepo,
            userRepo,
            auditWriter,
            snapshotRefresher,
            publisher
        );
        var projectId = NewId();
        var cardId = NewId();
        var actorId = NewId();

        memberRepo.Add(
            new ProjectMember
            {
                ProjectId = projectId,
                UserId = actorId,
                Role = MemberRole.Member,
            }
        );
        cardRepo.Cards.Add(
            new Card
            {
                Id = cardId,
                ProjectId = projectId,
                Type = CardType.Task,
            }
        );

        var result = await service.CreateAsync(
            new CreatePlanCommand(projectId, cardId, null, actorId, "Plan Title", "Desc", "# Plan")
        );

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
        var (
            planRepo,
            cardRepo,
            specRepo,
            memberRepo,
            userRepo,
            auditWriter,
            snapshotRefresher,
            publisher
        ) = CreateMocks();
        var service = new PlanService(
            planRepo,
            cardRepo,
            specRepo,
            memberRepo,
            userRepo,
            auditWriter,
            snapshotRefresher,
            publisher
        );
        var projectId = NewId();
        var actorId = NewId();
        var planId = NewId();

        var plan = new Plan
        {
            Id = planId,
            ProjectId = projectId,
            Title = "Original",
            Content = "V1",
            Version = 1,
            CreatedByUserId = actorId,
        };
        planRepo.Add(plan);
        planRepo.AddVersion(
            new PlanVersion
            {
                Id = NewId(),
                PlanId = planId,
                Version = 1,
                Content = "V1",
                CreatedByUserId = actorId,
            }
        );
        memberRepo.Add(
            new ProjectMember
            {
                ProjectId = projectId,
                UserId = actorId,
                Role = MemberRole.Member,
            }
        );

        var result = await service.UpdateAsync(
            new UpdatePlanCommand(projectId, planId, actorId, "Updated", null, "V2")
        );

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
        var (
            planRepo,
            cardRepo,
            specRepo,
            memberRepo,
            userRepo,
            auditWriter,
            snapshotRefresher,
            publisher
        ) = CreateMocks();
        var service = new PlanService(
            planRepo,
            cardRepo,
            specRepo,
            memberRepo,
            userRepo,
            auditWriter,
            snapshotRefresher,
            publisher
        );
        var projectId = NewId();
        var actorId = NewId();
        var planId = NewId();

        var plan = new Plan
        {
            Id = planId,
            ProjectId = projectId,
            Title = "Plan",
            Content = "V2",
            Version = 2,
            CreatedByUserId = actorId,
        };
        planRepo.Add(plan);
        planRepo.AddVersion(
            new PlanVersion
            {
                Id = NewId(),
                PlanId = planId,
                Version = 1,
                Content = "V1",
                CreatedByUserId = actorId,
            }
        );
        planRepo.AddVersion(
            new PlanVersion
            {
                Id = NewId(),
                PlanId = planId,
                Version = 2,
                Content = "V2",
                CreatedByUserId = actorId,
            }
        );
        memberRepo.Add(
            new ProjectMember
            {
                ProjectId = projectId,
                UserId = actorId,
                Role = MemberRole.Member,
            }
        );

        var result = await service.RestoreVersionAsync(
            new RestorePlanVersionCommand(projectId, planId, 1, actorId)
        );

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
        var (
            planRepo,
            cardRepo,
            specRepo,
            memberRepo,
            userRepo,
            auditWriter,
            snapshotRefresher,
            publisher
        ) = CreateMocks();
        var service = new PlanService(
            planRepo,
            cardRepo,
            specRepo,
            memberRepo,
            userRepo,
            auditWriter,
            snapshotRefresher,
            publisher
        );
        var projectId = NewId();
        var cardId = NewId();
        var actorId = NewId();

        memberRepo.Add(
            new ProjectMember
            {
                ProjectId = projectId,
                UserId = actorId,
                Role = MemberRole.Member,
            }
        );
        cardRepo.Cards.Add(
            new Card
            {
                Id = cardId,
                ProjectId = projectId,
                Type = CardType.Task,
            }
        );
        var largeContent = new string('x', 1_000_001);

        var result = await service.CreateAsync(
            new CreatePlanCommand(projectId, cardId, null, actorId, "Big", null, largeContent)
        );

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Plans.MarkdownPayloadTooLarge, result.Error.Code);
    }

    // ─── Card-type / SpecId validation (D-44) ─────────────────────────────────

    [Fact]
    public async Task CreateAsync_CardNotFound_ReturnsCardNotFound()
    {
        var (
            planRepo,
            cardRepo,
            specRepo,
            memberRepo,
            userRepo,
            auditWriter,
            snapshotRefresher,
            publisher
        ) = CreateMocks();
        var service = new PlanService(
            planRepo,
            cardRepo,
            specRepo,
            memberRepo,
            userRepo,
            auditWriter,
            snapshotRefresher,
            publisher
        );
        var projectId = NewId();
        var actorId = NewId();

        memberRepo.Add(
            new ProjectMember
            {
                ProjectId = projectId,
                UserId = actorId,
                Role = MemberRole.Member,
            }
        );

        var result = await service.CreateAsync(
            new CreatePlanCommand(projectId, NewId(), null, actorId, "T", null, "C")
        );

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Cards.NotFound, result.Error.Code);
    }

    [Fact]
    public async Task CreateAsync_CardInDifferentProject_ReturnsProjectMismatch()
    {
        var (
            planRepo,
            cardRepo,
            specRepo,
            memberRepo,
            userRepo,
            auditWriter,
            snapshotRefresher,
            publisher
        ) = CreateMocks();
        var service = new PlanService(
            planRepo,
            cardRepo,
            specRepo,
            memberRepo,
            userRepo,
            auditWriter,
            snapshotRefresher,
            publisher
        );
        var projectId = NewId();
        var cardId = NewId();
        var actorId = NewId();

        memberRepo.Add(
            new ProjectMember
            {
                ProjectId = projectId,
                UserId = actorId,
                Role = MemberRole.Member,
            }
        );
        cardRepo.Cards.Add(
            new Card
            {
                Id = cardId,
                ProjectId = NewId(),
                Type = CardType.Task,
            }
        );

        var result = await service.CreateAsync(
            new CreatePlanCommand(projectId, cardId, null, actorId, "T", null, "C")
        );

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Plans.CardDocumentProjectMismatch, result.Error.Code);
    }

    [Fact]
    public async Task CreateAsync_IdeaCard_ReturnsInvalidCardType()
    {
        var (
            planRepo,
            cardRepo,
            specRepo,
            memberRepo,
            userRepo,
            auditWriter,
            snapshotRefresher,
            publisher
        ) = CreateMocks();
        var service = new PlanService(
            planRepo,
            cardRepo,
            specRepo,
            memberRepo,
            userRepo,
            auditWriter,
            snapshotRefresher,
            publisher
        );
        var projectId = NewId();
        var cardId = NewId();
        var actorId = NewId();

        memberRepo.Add(
            new ProjectMember
            {
                ProjectId = projectId,
                UserId = actorId,
                Role = MemberRole.Member,
            }
        );
        cardRepo.Cards.Add(
            new Card
            {
                Id = cardId,
                ProjectId = projectId,
                Type = CardType.Idea,
            }
        );

        var result = await service.CreateAsync(
            new CreatePlanCommand(projectId, cardId, null, actorId, "T", null, "C")
        );

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Plans.InvalidCardType, result.Error.Code);
    }

    [Theory]
    [InlineData(CardType.Issue)]
    [InlineData(CardType.Task)]
    public async Task CreateAsync_AllowedCardTypeWithoutSpecId_Succeeds(CardType cardType)
    {
        var (
            planRepo,
            cardRepo,
            specRepo,
            memberRepo,
            userRepo,
            auditWriter,
            snapshotRefresher,
            publisher
        ) = CreateMocks();
        var service = new PlanService(
            planRepo,
            cardRepo,
            specRepo,
            memberRepo,
            userRepo,
            auditWriter,
            snapshotRefresher,
            publisher
        );
        var projectId = NewId();
        var cardId = NewId();
        var actorId = NewId();

        memberRepo.Add(
            new ProjectMember
            {
                ProjectId = projectId,
                UserId = actorId,
                Role = MemberRole.Member,
            }
        );
        cardRepo.Cards.Add(
            new Card
            {
                Id = cardId,
                ProjectId = projectId,
                Type = cardType,
            }
        );

        var result = await service.CreateAsync(
            new CreatePlanCommand(projectId, cardId, null, actorId, "T", null, "C")
        );

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task CreateAsync_SpecIdOnIssueCard_ReturnsSpecLinkNotAllowed()
    {
        var (
            planRepo,
            cardRepo,
            specRepo,
            memberRepo,
            userRepo,
            auditWriter,
            snapshotRefresher,
            publisher
        ) = CreateMocks();
        var service = new PlanService(
            planRepo,
            cardRepo,
            specRepo,
            memberRepo,
            userRepo,
            auditWriter,
            snapshotRefresher,
            publisher
        );
        var projectId = NewId();
        var cardId = NewId();
        var actorId = NewId();

        memberRepo.Add(
            new ProjectMember
            {
                ProjectId = projectId,
                UserId = actorId,
                Role = MemberRole.Member,
            }
        );
        cardRepo.Cards.Add(
            new Card
            {
                Id = cardId,
                ProjectId = projectId,
                Type = CardType.Issue,
            }
        );
        var spec = new Spec
        {
            Id = NewId(),
            ProjectId = projectId,
            CardId = cardId,
            DocType = DocType.Report,
        };
        specRepo.Add(spec);

        var result = await service.CreateAsync(
            new CreatePlanCommand(projectId, cardId, spec.Id, actorId, "T", null, "C")
        );

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Plans.SpecLinkNotAllowed, result.Error.Code);
    }

    [Fact]
    public async Task CreateAsync_SpecIdOnNonGoalCard_ReturnsSpecLinkNotAllowed()
    {
        var (
            planRepo,
            cardRepo,
            specRepo,
            memberRepo,
            userRepo,
            auditWriter,
            snapshotRefresher,
            publisher
        ) = CreateMocks();
        var service = new PlanService(
            planRepo,
            cardRepo,
            specRepo,
            memberRepo,
            userRepo,
            auditWriter,
            snapshotRefresher,
            publisher
        );
        var projectId = NewId();
        var cardId = NewId();
        var actorId = NewId();

        memberRepo.Add(
            new ProjectMember
            {
                ProjectId = projectId,
                UserId = actorId,
                Role = MemberRole.Member,
            }
        );
        cardRepo.Cards.Add(
            new Card
            {
                Id = cardId,
                ProjectId = projectId,
                Type = CardType.Issue,
            }
        );
        var otherSpec = new Spec
        {
            Id = NewId(),
            ProjectId = projectId,
            CardId = NewId(),
            DocType = DocType.Report,
        };
        specRepo.Add(otherSpec);

        var result = await service.CreateAsync(
            new CreatePlanCommand(projectId, cardId, otherSpec.Id, actorId, "T", null, "C")
        );

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Plans.SpecLinkNotAllowed, result.Error.Code);
    }

    [Fact]
    public async Task CreateAsync_IssueWithoutSpecId_Succeeds()
    {
        var (
            planRepo,
            cardRepo,
            specRepo,
            memberRepo,
            userRepo,
            auditWriter,
            snapshotRefresher,
            publisher
        ) = CreateMocks();
        var service = new PlanService(
            planRepo,
            cardRepo,
            specRepo,
            memberRepo,
            userRepo,
            auditWriter,
            snapshotRefresher,
            publisher
        );
        var projectId = NewId();
        var cardId = NewId();
        var actorId = NewId();

        memberRepo.Add(
            new ProjectMember
            {
                ProjectId = projectId,
                UserId = actorId,
                Role = MemberRole.Member,
            }
        );
        cardRepo.Cards.Add(
            new Card
            {
                Id = cardId,
                ProjectId = projectId,
                Type = CardType.Issue,
            }
        );

        var result = await service.CreateAsync(
            new CreatePlanCommand(projectId, cardId, null, actorId, "T", null, "C")
        );

        Assert.True(result.IsSuccess);
    }

    // ─── Audit tests ───────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_WritesAuditLog()
    {
        var (
            planRepo,
            cardRepo,
            specRepo,
            memberRepo,
            userRepo,
            auditWriter,
            snapshotRefresher,
            publisher
        ) = CreateMocks();
        var service = new PlanService(
            planRepo,
            cardRepo,
            specRepo,
            memberRepo,
            userRepo,
            auditWriter,
            snapshotRefresher,
            publisher
        );
        var projectId = NewId();
        var cardId = NewId();
        var actorId = NewId();

        memberRepo.Add(
            new ProjectMember
            {
                ProjectId = projectId,
                UserId = actorId,
                Role = MemberRole.Member,
            }
        );
        cardRepo.Cards.Add(
            new Card
            {
                Id = cardId,
                ProjectId = projectId,
                Type = CardType.Task,
            }
        );

        var result = await service.CreateAsync(
            new CreatePlanCommand(projectId, cardId, null, actorId, "Plan Title", "Desc", "# Plan")
        );

        Assert.True(result.IsSuccess);
        var req = Assert.Single(auditWriter.Writes);
        Assert.Equal(actorId, req.ActorId);
        Assert.Equal(AuditLogScope.Project, req.Scope);
        Assert.Equal("Plan", req.EntityType);
        Assert.Equal(result.Value.Id, req.EntityId);
        Assert.Equal("Created", req.Action);
        Assert.Equal(projectId, req.ProjectId);
    }

    [Fact]
    public async Task UpdateAsync_WritesAuditLog()
    {
        var (
            planRepo,
            cardRepo,
            specRepo,
            memberRepo,
            userRepo,
            auditWriter,
            snapshotRefresher,
            publisher
        ) = CreateMocks();
        var service = new PlanService(
            planRepo,
            cardRepo,
            specRepo,
            memberRepo,
            userRepo,
            auditWriter,
            snapshotRefresher,
            publisher
        );
        var projectId = NewId();
        var actorId = NewId();
        var planId = NewId();

        var plan = new Plan
        {
            Id = planId,
            ProjectId = projectId,
            Title = "Original",
            Content = "V1",
            Version = 1,
            CreatedByUserId = actorId,
        };
        planRepo.Add(plan);
        planRepo.AddVersion(
            new PlanVersion
            {
                Id = NewId(),
                PlanId = planId,
                Version = 1,
                Content = "V1",
                CreatedByUserId = actorId,
            }
        );
        memberRepo.Add(
            new ProjectMember
            {
                ProjectId = projectId,
                UserId = actorId,
                Role = MemberRole.Member,
            }
        );

        var result = await service.UpdateAsync(
            new UpdatePlanCommand(projectId, planId, actorId, "Updated", null, "V2")
        );

        Assert.True(result.IsSuccess);
        var req = Assert.Single(auditWriter.Writes);
        Assert.Equal(actorId, req.ActorId);
        Assert.Equal(AuditLogScope.Project, req.Scope);
        Assert.Equal("Plan", req.EntityType);
        Assert.Equal(planId, req.EntityId);
        Assert.Equal("Updated", req.Action);
        Assert.Equal(projectId, req.ProjectId);
    }

    [Fact]
    public async Task RestoreVersionAsync_WritesAuditLog()
    {
        var (
            planRepo,
            cardRepo,
            specRepo,
            memberRepo,
            userRepo,
            auditWriter,
            snapshotRefresher,
            publisher
        ) = CreateMocks();
        var service = new PlanService(
            planRepo,
            cardRepo,
            specRepo,
            memberRepo,
            userRepo,
            auditWriter,
            snapshotRefresher,
            publisher
        );
        var projectId = NewId();
        var actorId = NewId();
        var planId = NewId();

        var plan = new Plan
        {
            Id = planId,
            ProjectId = projectId,
            Title = "Plan",
            Content = "V2",
            Version = 2,
            CreatedByUserId = actorId,
        };
        planRepo.Add(plan);
        planRepo.AddVersion(
            new PlanVersion
            {
                Id = NewId(),
                PlanId = planId,
                Version = 1,
                Content = "V1",
                CreatedByUserId = actorId,
            }
        );
        planRepo.AddVersion(
            new PlanVersion
            {
                Id = NewId(),
                PlanId = planId,
                Version = 2,
                Content = "V2",
                CreatedByUserId = actorId,
            }
        );
        memberRepo.Add(
            new ProjectMember
            {
                ProjectId = projectId,
                UserId = actorId,
                Role = MemberRole.Member,
            }
        );

        var result = await service.RestoreVersionAsync(
            new RestorePlanVersionCommand(projectId, planId, 1, actorId)
        );

        Assert.True(result.IsSuccess);
        var req = Assert.Single(auditWriter.Writes);
        Assert.Equal(actorId, req.ActorId);
        Assert.Equal(AuditLogScope.Project, req.Scope);
        Assert.Equal("Plan", req.EntityType);
        Assert.Equal(planId, req.EntityId);
        Assert.Equal("Restored", req.Action);
        Assert.Equal(projectId, req.ProjectId);
    }

    [Fact]
    public async Task UpdateAsync_WhenPlanIsDone_ReturnsEditForbiddenError()
    {
        var (
            planRepo,
            cardRepo,
            specRepo,
            memberRepo,
            userRepo,
            auditWriter,
            snapshotRefresher,
            publisher
        ) = CreateMocks();
        var service = new PlanService(
            planRepo,
            cardRepo,
            specRepo,
            memberRepo,
            userRepo,
            auditWriter,
            snapshotRefresher,
            publisher
        );
        var projectId = NewId();
        var actorId = NewId();
        var planId = NewId();

        var plan = new Plan
        {
            Id = planId,
            Status = PlanStatus.Done,
            ProjectId = projectId,
            CardId = NewId(),
        };
        planRepo.Add(plan);
        memberRepo.Add(
            new ProjectMember
            {
                ProjectId = projectId,
                UserId = actorId,
                Role = MemberRole.Member,
            }
        );

        var result = await service.UpdateAsync(
            new UpdatePlanCommand(projectId, planId, actorId, "New Title", null, "content")
        );

        Assert.False(result.IsSuccess);
        Assert.Equal(DomainErrorCodes.Plans.EditForbiddenWhenDone, result.Error.Code);
    }

    [Fact]
    public async Task RestoreVersionAsync_WhenPlanIsDone_ReturnsEditForbiddenError()
    {
        var (
            planRepo,
            cardRepo,
            specRepo,
            memberRepo,
            userRepo,
            auditWriter,
            snapshotRefresher,
            publisher
        ) = CreateMocks();
        var service = new PlanService(
            planRepo,
            cardRepo,
            specRepo,
            memberRepo,
            userRepo,
            auditWriter,
            snapshotRefresher,
            publisher
        );
        var projectId = NewId();
        var actorId = NewId();
        var planId = NewId();

        var plan = new Plan
        {
            Id = planId,
            Status = PlanStatus.Done,
            ProjectId = projectId,
            CardId = NewId(),
        };
        planRepo.Add(plan);
        planRepo.AddVersion(
            new PlanVersion
            {
                Id = NewId(),
                PlanId = planId,
                Version = 1,
                Content = "V1",
                CreatedByUserId = actorId,
            }
        );
        memberRepo.Add(
            new ProjectMember
            {
                ProjectId = projectId,
                UserId = actorId,
                Role = MemberRole.Member,
            }
        );

        var result = await service.RestoreVersionAsync(
            new RestorePlanVersionCommand(projectId, planId, 1, actorId)
        );

        Assert.False(result.IsSuccess);
        Assert.Equal(DomainErrorCodes.Plans.EditForbiddenWhenDone, result.Error.Code);
    }

    [Fact]
    public async Task SetStatusAsync_DoneToActive_TransitionsToActive()
    {
        var (
            planRepo,
            cardRepo,
            specRepo,
            memberRepo,
            userRepo,
            auditWriter,
            snapshotRefresher,
            publisher
        ) = CreateMocks();
        var service = new PlanService(
            planRepo,
            cardRepo,
            specRepo,
            memberRepo,
            userRepo,
            auditWriter,
            snapshotRefresher,
            publisher
        );
        var projectId = NewId();
        var actorId = NewId();
        var planId = NewId();

        var plan = new Plan
        {
            Id = planId,
            Status = PlanStatus.Done,
            ProjectId = projectId,
            CardId = NewId(),
        };
        planRepo.Add(plan);
        memberRepo.Add(
            new ProjectMember
            {
                ProjectId = projectId,
                UserId = actorId,
                Role = MemberRole.Member,
            }
        );

        var result = await service.SetStatusAsync(
            new SetPlanStatusCommand(projectId, planId, actorId, PlanStatus.Active)
        );

        Assert.True(result.IsSuccess);
        Assert.Equal(PlanStatus.Active, result.Value.Status);
    }

    [Fact]
    public async Task SetStatusAsync_DoneToPending_TransitionsToPending()
    {
        var (
            planRepo,
            cardRepo,
            specRepo,
            memberRepo,
            userRepo,
            auditWriter,
            snapshotRefresher,
            publisher
        ) = CreateMocks();
        var service = new PlanService(
            planRepo,
            cardRepo,
            specRepo,
            memberRepo,
            userRepo,
            auditWriter,
            snapshotRefresher,
            publisher
        );
        var projectId = NewId();
        var actorId = NewId();
        var planId = NewId();

        var plan = new Plan
        {
            Id = planId,
            Status = PlanStatus.Done,
            ProjectId = projectId,
            CardId = NewId(),
        };
        planRepo.Add(plan);
        memberRepo.Add(
            new ProjectMember
            {
                ProjectId = projectId,
                UserId = actorId,
                Role = MemberRole.Member,
            }
        );

        var result = await service.SetStatusAsync(
            new SetPlanStatusCommand(projectId, planId, actorId, PlanStatus.Pending)
        );

        Assert.True(result.IsSuccess);
        Assert.Equal(PlanStatus.Pending, result.Value.Status);
    }

    [Fact]
    public async Task SetStatusAsync_ActiveToPending_TransitionsToPending()
    {
        var (
            planRepo,
            cardRepo,
            specRepo,
            memberRepo,
            userRepo,
            auditWriter,
            snapshotRefresher,
            publisher
        ) = CreateMocks();
        var service = new PlanService(
            planRepo,
            cardRepo,
            specRepo,
            memberRepo,
            userRepo,
            auditWriter,
            snapshotRefresher,
            publisher
        );
        var projectId = NewId();
        var actorId = NewId();
        var planId = NewId();

        var plan = new Plan
        {
            Id = planId,
            Status = PlanStatus.Active,
            ProjectId = projectId,
            CardId = NewId(),
        };
        planRepo.Add(plan);
        memberRepo.Add(
            new ProjectMember
            {
                ProjectId = projectId,
                UserId = actorId,
                Role = MemberRole.Member,
            }
        );

        var result = await service.SetStatusAsync(
            new SetPlanStatusCommand(projectId, planId, actorId, PlanStatus.Pending)
        );

        Assert.True(result.IsSuccess);
        Assert.Equal(PlanStatus.Pending, result.Value.Status);
    }

    [Fact]
    public async Task SetStatusAsync_SameStatus_ReturnsSuccessWithNoChange()
    {
        var (
            planRepo,
            cardRepo,
            specRepo,
            memberRepo,
            userRepo,
            auditWriter,
            snapshotRefresher,
            publisher
        ) = CreateMocks();
        var service = new PlanService(
            planRepo,
            cardRepo,
            specRepo,
            memberRepo,
            userRepo,
            auditWriter,
            snapshotRefresher,
            publisher
        );
        var projectId = NewId();
        var actorId = NewId();
        var planId = NewId();

        var plan = new Plan
        {
            Id = planId,
            Status = PlanStatus.Active,
            ProjectId = projectId,
            CardId = NewId(),
        };
        planRepo.Add(plan);
        memberRepo.Add(
            new ProjectMember
            {
                ProjectId = projectId,
                UserId = actorId,
                Role = MemberRole.Member,
            }
        );

        var result = await service.SetStatusAsync(
            new SetPlanStatusCommand(projectId, planId, actorId, PlanStatus.Active)
        );

        Assert.True(result.IsSuccess);
        Assert.Equal(PlanStatus.Active, result.Value.Status);
    }

    [Fact]
    public async Task SetStatusAsync_WhenUserHasNoMembership_ReturnsAccessDenied()
    {
        var (
            planRepo,
            cardRepo,
            specRepo,
            memberRepo,
            userRepo,
            auditWriter,
            snapshotRefresher,
            publisher
        ) = CreateMocks();
        var service = new PlanService(
            planRepo,
            cardRepo,
            specRepo,
            memberRepo,
            userRepo,
            auditWriter,
            snapshotRefresher,
            publisher
        );
        var projectId = NewId();
        var actorId = NewId();
        var planId = NewId();

        var plan = new Plan
        {
            Id = planId,
            Status = PlanStatus.Done,
            ProjectId = projectId,
            CardId = NewId(),
        };
        planRepo.Add(plan);

        var result = await service.SetStatusAsync(
            new SetPlanStatusCommand(projectId, planId, actorId, PlanStatus.Pending)
        );

        Assert.False(result.IsSuccess);
        Assert.Equal(DomainErrorCodes.Projects.MembershipDenied, result.Error.Code);
    }

    [Fact]
    public async Task SetStatusAsync_AdminNonMember_Succeeds()
    {
        var (
            planRepo,
            cardRepo,
            specRepo,
            memberRepo,
            _,
            auditWriter,
            snapshotRefresher,
            publisher
        ) = CreateAdminMocks();
        var service = new PlanService(
            planRepo,
            cardRepo,
            specRepo,
            memberRepo,
            CreateAdminUserRepo(),
            auditWriter,
            snapshotRefresher,
            publisher
        );
        var projectId = NewId();
        var actorId = NewId();
        var planId = NewId();

        var plan = new Plan
        {
            Id = planId,
            Status = PlanStatus.Done,
            ProjectId = projectId,
            CardId = NewId(),
        };
        planRepo.Add(plan);

        var result = await service.SetStatusAsync(
            new SetPlanStatusCommand(projectId, planId, actorId, PlanStatus.Pending)
        );

        Assert.True(result.IsSuccess);
    }

    private static (
        InMemoryPlanRepository planRepo,
        InMemoryCardRepository cardRepo,
        InMemorySpecRepository specRepo,
        InMemoryProjectMemberRepository memberRepo,
        FakeUserRepositoryAdmin userRepo,
        InMemoryAuditLogWriter auditWriter,
        NullSnapshotRefresher snapshotRefresher,
        FakeProjectBoardEventPublisher publisher
    ) CreateAdminMocks()
    {
        return (
            new InMemoryPlanRepository(),
            new InMemoryCardRepository(),
            new InMemorySpecRepository(),
            new InMemoryProjectMemberRepository(),
            new FakeUserRepositoryAdmin(),
            new InMemoryAuditLogWriter(),
            new NullSnapshotRefresher(),
            new FakeProjectBoardEventPublisher()
        );
    }

    private static FakeUserRepositoryAdmin CreateAdminUserRepo() => new();

    [Fact]
    public async Task CreateAsync_SetsStatusToPending()
    {
        var (
            planRepo,
            cardRepo,
            specRepo,
            memberRepo,
            userRepo,
            auditWriter,
            snapshotRefresher,
            publisher
        ) = CreateMocks();
        var service = new PlanService(
            planRepo,
            cardRepo,
            specRepo,
            memberRepo,
            userRepo,
            auditWriter,
            snapshotRefresher,
            publisher
        );
        var projectId = NewId();
        var cardId = NewId();
        var actorId = NewId();

        memberRepo.Add(
            new ProjectMember
            {
                ProjectId = projectId,
                UserId = actorId,
                Role = MemberRole.Member,
            }
        );
        cardRepo.Cards.Add(
            new Card
            {
                Id = cardId,
                ProjectId = projectId,
                Type = CardType.Task,
            }
        );

        var result = await service.CreateAsync(
            new CreatePlanCommand(projectId, cardId, null, actorId, "T", null, "C")
        );

        Assert.True(result.IsSuccess);
        Assert.Equal(PlanStatus.Pending, result.Value.Status);
    }

    private static (
        InMemoryPlanRepository planRepo,
        InMemoryCardRepository cardRepo,
        InMemorySpecRepository specRepo,
        InMemoryProjectMemberRepository memberRepo,
        FakeUserRepositoryForAdmin userRepo,
        InMemoryAuditLogWriter auditWriter,
        NullSnapshotRefresher snapshotRefresher,
        FakeProjectBoardEventPublisher publisher
    ) CreateMocks()
    {
        return (
            new InMemoryPlanRepository(),
            new InMemoryCardRepository(),
            new InMemorySpecRepository(),
            new InMemoryProjectMemberRepository(),
            new FakeUserRepositoryForAdmin(),
            new InMemoryAuditLogWriter(),
            new NullSnapshotRefresher(),
            new FakeProjectBoardEventPublisher()
        );
    }
}

internal class InMemoryPlanRepository : IPlanRepository
{
    public List<Plan> Plans { get; } = [];
    public List<PlanVersion> Versions { get; } = [];

    public Task<Plan?> GetByIdAsync(Guid planId, CancellationToken ct = default) =>
        Task.FromResult(Plans.FirstOrDefault(p => p.Id == planId));

    public Task<IReadOnlyList<Plan>> ListByProjectAsync(
        Guid projectId,
        PlanListFilter filter,
        CancellationToken ct = default
    ) => Task.FromResult<IReadOnlyList<Plan>>([.. Plans.Where(p => p.ProjectId == projectId)]);

    public Task<PlanVersion?> GetVersionAsync(
        Guid planId,
        int version,
        CancellationToken ct = default
    ) => Task.FromResult(Versions.FirstOrDefault(v => v.PlanId == planId && v.Version == version));

    public Task<IReadOnlyList<PlanVersion>> ListVersionsAsync(
        Guid planId,
        CancellationToken ct = default
    ) =>
        Task.FromResult<IReadOnlyList<PlanVersion>>([
            .. Versions.Where(v => v.PlanId == planId).OrderBy(v => v.Version),
        ]);

    public Task<IReadOnlyList<Plan>> ListByCardAsync(
        Guid cardId,
        PlanListFilter filter,
        CancellationToken ct = default
    ) => Task.FromResult<IReadOnlyList<Plan>>([.. Plans.Where(p => p.CardId == cardId)]);

    public Task AddAsync(Plan plan, CancellationToken ct = default)
    {
        Plans.Add(plan);
        return Task.CompletedTask;
    }

    public void Add(Plan plan) => AddAsync(plan).GetAwaiter().GetResult();

    public Task AddVersionAsync(PlanVersion version, CancellationToken ct = default)
    {
        Versions.Add(version);
        return Task.CompletedTask;
    }

    public void AddVersion(PlanVersion version) =>
        AddVersionAsync(version).GetAwaiter().GetResult();

    public Task UpdateAsync(Plan plan, CancellationToken ct = default)
    {
        var idx = Plans.FindIndex(p => p.Id == plan.Id);
        if (idx >= 0)
            Plans[idx] = plan;
        return Task.CompletedTask;
    }

    public Task<int> SaveChangesAsync(CancellationToken ct = default) => Task.FromResult(1);
}

internal class InMemoryCardRepository : ICardRepository
{
    public List<Card> Cards { get; } = [];

    public Task<Card?> GetByIdAsync(Guid cardId, CancellationToken ct = default) =>
        Task.FromResult(Cards.FirstOrDefault(c => c.Id == cardId));

    public Task<IReadOnlyDictionary<Guid, Card>> GetByIdsAsync(
        IReadOnlyList<Guid> cardIds,
        CancellationToken ct = default
    ) =>
        Task.FromResult<IReadOnlyDictionary<Guid, Card>>(
            Cards.Where(c => cardIds.Contains(c.Id)).ToDictionary(c => c.Id)
        );

    public Task<Card?> GetByProjectAndNumberAsync(
        Guid projectId,
        int cardNumber,
        CancellationToken ct = default
    ) =>
        Task.FromResult(
            Cards.FirstOrDefault(c => c.ProjectId == projectId && c.CardNumber == cardNumber)
        );

    public Task<IReadOnlyList<Card>> ListByProjectAsync(
        Guid projectId,
        CardListFilter filter,
        CancellationToken ct = default
    ) => Task.FromResult<IReadOnlyList<Card>>([.. Cards.Where(c => c.ProjectId == projectId)]);

    public Task<int> GetMaxCardNumberAsync(Guid projectId, CancellationToken ct = default) =>
        Task.FromResult(
            Cards
                .Where(c => c.ProjectId == projectId)
                .Select(c => c.CardNumber)
                .DefaultIfEmpty(0)
                .Max()
        );

    public Task AddAsync(Card card, CancellationToken ct = default)
    {
        Cards.Add(card);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Card card, CancellationToken ct = default)
    {
        var idx = Cards.FindIndex(c => c.Id == card.Id);
        if (idx >= 0)
            Cards[idx] = card;
        return Task.CompletedTask;
    }

    public Task UpdateRangeAsync(IReadOnlyList<Card> cards, CancellationToken ct = default)
    {
        foreach (var card in cards)
        {
            var idx = Cards.FindIndex(c => c.Id == card.Id);
            if (idx >= 0)
                Cards[idx] = card;
        }
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid cardId, CancellationToken ct = default)
    {
        Cards.RemoveAll(c => c.Id == cardId);
        return Task.CompletedTask;
    }

    public Task CompactColumnPositionsAsync(
        Guid columnId,
        int exceptPosition,
        CancellationToken ct = default
    ) => Task.CompletedTask;

    public Task<int> CountByColumnIdAsync(Guid columnId, CancellationToken ct = default) =>
        Task.FromResult(Cards.Count(c => c.ColumnId == columnId));

    public Task<int> CountActiveChildrenAsync(Guid parentCardId, CancellationToken ct = default) =>
        Task.FromResult(Cards.Count(c => c.ParentCardId == parentCardId && c.ArchivedAt == null));
}

internal class InMemorySpecRepository : ISpecRepository
{
    public List<Spec> Specs { get; } = [];
    public List<SpecVersion> Versions { get; } = [];

    public Task<Spec?> GetByIdAsync(Guid specId, CancellationToken ct = default) =>
        Task.FromResult(Specs.FirstOrDefault(s => s.Id == specId));

    public Task<IReadOnlyList<Spec>> ListByProjectAsync(
        Guid projectId,
        SpecListFilter filter,
        CancellationToken ct = default
    ) => Task.FromResult<IReadOnlyList<Spec>>([.. Specs.Where(s => s.ProjectId == projectId)]);

    public Task<SpecVersion?> GetVersionAsync(
        Guid specId,
        int version,
        CancellationToken ct = default
    ) => Task.FromResult(Versions.FirstOrDefault(v => v.SpecId == specId && v.Version == version));

    public Task<IReadOnlyList<SpecVersion>> ListVersionsAsync(
        Guid specId,
        CancellationToken ct = default
    ) =>
        Task.FromResult<IReadOnlyList<SpecVersion>>([
            .. Versions.Where(v => v.SpecId == specId).OrderBy(v => v.Version),
        ]);

    public Task<IReadOnlyList<Spec>> ListByCardAsync(
        Guid cardId,
        SpecListFilter filter,
        CancellationToken ct = default
    ) => Task.FromResult<IReadOnlyList<Spec>>([.. Specs.Where(s => s.CardId == cardId)]);

    public Task AddAsync(Spec spec, CancellationToken ct = default)
    {
        Specs.Add(spec);
        return Task.CompletedTask;
    }

    public void Add(Spec spec) => AddAsync(spec).GetAwaiter().GetResult();

    public Task AddVersionAsync(SpecVersion version, CancellationToken ct = default)
    {
        Versions.Add(version);
        return Task.CompletedTask;
    }

    public void AddVersion(SpecVersion version) =>
        AddVersionAsync(version).GetAwaiter().GetResult();

    public Task UpdateAsync(Spec spec, CancellationToken ct = default)
    {
        var idx = Specs.FindIndex(s => s.Id == spec.Id);
        if (idx >= 0)
            Specs[idx] = spec;
        return Task.CompletedTask;
    }

    public Task<int> SaveChangesAsync(CancellationToken ct = default) => Task.FromResult(1);
}

internal class InMemoryProjectMemberRepository : IProjectMemberRepository
{
    public List<ProjectMember> Members { get; } = [];

    public Task<ProjectMember?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(Members.FirstOrDefault(m => m.Id == id));

    public Task<ProjectMember?> GetByProjectAndUserAsync(
        Guid projectId,
        Guid userId,
        CancellationToken ct = default
    ) =>
        Task.FromResult(
            Members.FirstOrDefault(m => m.ProjectId == projectId && m.UserId == userId)
        );

    public Task<IReadOnlyList<ProjectMember>> ListMembersAsync(
        Guid projectId,
        CancellationToken ct = default
    ) =>
        Task.FromResult<IReadOnlyList<ProjectMember>>([
            .. Members.Where(m => m.ProjectId == projectId),
        ]);

    public Task<IReadOnlyDictionary<Guid, int>> GetMemberCountsAsync(
        IEnumerable<Guid> projectIds,
        CancellationToken ct = default
    )
    {
        var idList = projectIds.ToList();
        var counts = Members
            .Where(m => idList.Contains(m.ProjectId))
            .GroupBy(m => m.ProjectId)
            .ToDictionary(g => g.Key, g => g.Count());
        return Task.FromResult<IReadOnlyDictionary<Guid, int>>(counts);
    }

    public Task<IReadOnlyDictionary<Guid, MemberRole>> GetRolesByProjectAndUserAsync(
        IEnumerable<Guid> projectIds,
        Guid userId,
        CancellationToken ct = default
    )
    {
        var idList = projectIds.ToList();
        var roles = Members
            .Where(m => idList.Contains(m.ProjectId) && m.UserId == userId)
            .ToDictionary(m => m.ProjectId, m => m.Role);
        return Task.FromResult<IReadOnlyDictionary<Guid, MemberRole>>(roles);
    }

    public Task AddMemberAsync(ProjectMember member, CancellationToken ct = default)
    {
        Members.Add(member);
        return Task.CompletedTask;
    }

    public void Add(ProjectMember member) => AddMemberAsync(member).GetAwaiter().GetResult();

    public Task UpdateMemberAsync(ProjectMember member, CancellationToken ct = default)
    {
        var idx = Members.FindIndex(m => m.Id == member.Id);
        if (idx >= 0)
            Members[idx] = member;
        return Task.CompletedTask;
    }

    public Task RemoveMemberAsync(Guid id, CancellationToken ct = default)
    {
        Members.RemoveAll(m => m.Id == id);
        return Task.CompletedTask;
    }
}

internal class InMemoryAuditLogWriter : IAuditLogWriter
{
    public List<AuditLogRequest> Writes { get; } = [];

    public Task<Result> WriteAsync(AuditLogRequest request, CancellationToken ct = default)
    {
        Writes.Add(request);
        return Task.FromResult(Result.Success());
    }

    public void Clear() => Writes.Clear();
}
