namespace HydraForge.Application.Tests.Specs;

using HydraForge.Application.Audit;
using HydraForge.Application.Auth;
using HydraForge.Application.Cards;
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

public class SpecServiceTests
{
    private static Guid NewId() => Guid.NewGuid();

    [Fact]
    public async Task CreateAsync_CreatesSpecAndVersion1InSameTransaction()
    {
        var (specRepo, cardRepo, memberRepo, userRepo, auditWriter, snapshotRefresher, publisher) =
            CreateMocks();
        var service = new SpecService(
            specRepo,
            cardRepo,
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
                Type = CardType.Goal,
            }
        );

        var result = await service.CreateAsync(
            new CreateSpecCommand(
                projectId,
                cardId,
                actorId,
                DocType.Specification,
                "Spec Title",
                "Desc",
                "# Hello"
            )
        );

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
        var (specRepo, cardRepo, memberRepo, userRepo, auditWriter, snapshotRefresher, publisher) =
            CreateMocks();
        var service = new SpecService(
            specRepo,
            cardRepo,
            memberRepo,
            userRepo,
            auditWriter,
            snapshotRefresher,
            publisher
        );
        var projectId = NewId();
        var actorId = NewId();
        var specId = NewId();

        var spec = new Spec
        {
            Id = specId,
            ProjectId = projectId,
            Title = "Original",
            Content = "V1",
            Version = 1,
            CreatedByUserId = actorId,
        };
        specRepo.Add(spec);
        specRepo.AddVersion(
            new SpecVersion
            {
                Id = NewId(),
                SpecId = specId,
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
            new UpdateSpecCommand(projectId, specId, actorId, "Updated", null, "V2")
        );

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
        var (specRepo, cardRepo, memberRepo, userRepo, auditWriter, snapshotRefresher, publisher) =
            CreateMocks();
        var service = new SpecService(
            specRepo,
            cardRepo,
            memberRepo,
            userRepo,
            auditWriter,
            snapshotRefresher,
            publisher
        );
        var projectId = NewId();
        var actorId = NewId();
        var specId = NewId();

        var spec = new Spec
        {
            Id = specId,
            ProjectId = projectId,
            Title = "Spec",
            Content = "V2",
            Version = 2,
            CreatedByUserId = actorId,
        };
        specRepo.Add(spec);
        specRepo.AddVersion(
            new SpecVersion
            {
                Id = NewId(),
                SpecId = specId,
                Version = 1,
                Content = "V1",
                CreatedByUserId = actorId,
            }
        );
        specRepo.AddVersion(
            new SpecVersion
            {
                Id = NewId(),
                SpecId = specId,
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
            new RestoreSpecVersionCommand(projectId, specId, 1, actorId)
        );

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
        var (specRepo, cardRepo, memberRepo, userRepo, auditWriter, snapshotRefresher, publisher) =
            CreateMocks();
        var service = new SpecService(
            specRepo,
            cardRepo,
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
                Type = CardType.Goal,
            }
        );
        var largeContent = new string('x', 1_000_001);

        var result = await service.CreateAsync(
            new CreateSpecCommand(
                projectId,
                cardId,
                actorId,
                DocType.Specification,
                "Big",
                null,
                largeContent
            )
        );

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Specs.MarkdownPayloadTooLarge, result.Error.Code);
    }

    // ─── Card-type / doc-type validation (D-44) ───────────────────────────────

    [Fact]
    public async Task CreateAsync_CardNotFound_ReturnsCardNotFound()
    {
        var (specRepo, cardRepo, memberRepo, userRepo, auditWriter, snapshotRefresher, publisher) =
            CreateMocks();
        var service = new SpecService(
            specRepo,
            cardRepo,
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
            new CreateSpecCommand(
                projectId,
                NewId(),
                actorId,
                DocType.Specification,
                "T",
                null,
                "C"
            )
        );

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Cards.NotFound, result.Error.Code);
    }

    [Fact]
    public async Task CreateAsync_CardInDifferentProject_ReturnsProjectMismatch()
    {
        var (specRepo, cardRepo, memberRepo, userRepo, auditWriter, snapshotRefresher, publisher) =
            CreateMocks();
        var service = new SpecService(
            specRepo,
            cardRepo,
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
                Type = CardType.Goal,
            }
        );

        var result = await service.CreateAsync(
            new CreateSpecCommand(projectId, cardId, actorId, DocType.Specification, "T", null, "C")
        );

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Specs.CardDocumentProjectMismatch, result.Error.Code);
    }

    [Fact]
    public async Task CreateAsync_TaskCard_ReturnsInvalidCardType()
    {
        var (specRepo, cardRepo, memberRepo, userRepo, auditWriter, snapshotRefresher, publisher) =
            CreateMocks();
        var service = new SpecService(
            specRepo,
            cardRepo,
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
            new CreateSpecCommand(projectId, cardId, actorId, DocType.Specification, "T", null, "C")
        );

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Specs.InvalidCardType, result.Error.Code);
    }

    [Theory]
    [InlineData(CardType.Goal, DocType.Specification)]
    [InlineData(CardType.Idea, DocType.Concept)]
    [InlineData(CardType.Issue, DocType.Report)]
    public async Task CreateAsync_MatchingDocTypeForCardType_Succeeds(
        CardType cardType,
        DocType docType
    )
    {
        var (specRepo, cardRepo, memberRepo, userRepo, auditWriter, snapshotRefresher, publisher) =
            CreateMocks();
        var service = new SpecService(
            specRepo,
            cardRepo,
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
            new CreateSpecCommand(projectId, cardId, actorId, docType, "T", null, "C")
        );

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task CreateAsync_DocTypeMismatchForCardType_ReturnsDocTypeMismatch()
    {
        var (specRepo, cardRepo, memberRepo, userRepo, auditWriter, snapshotRefresher, publisher) =
            CreateMocks();
        var service = new SpecService(
            specRepo,
            cardRepo,
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
                Type = CardType.Goal,
            }
        );

        var result = await service.CreateAsync(
            new CreateSpecCommand(projectId, cardId, actorId, DocType.Report, "T", null, "C")
        );

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Specs.DocTypeMismatch, result.Error.Code);
    }

    [Fact]
    public async Task CreateAsync_CardAlreadyHasSpec_ReturnsAlreadyExists()
    {
        var (specRepo, cardRepo, memberRepo, userRepo, auditWriter, snapshotRefresher, publisher) =
            CreateMocks();
        var service = new SpecService(
            specRepo,
            cardRepo,
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
                Type = CardType.Goal,
            }
        );
        specRepo.Add(
            new Spec
            {
                Id = NewId(),
                ProjectId = projectId,
                CardId = cardId,
                DocType = DocType.Specification,
            }
        );

        var result = await service.CreateAsync(
            new CreateSpecCommand(
                projectId,
                cardId,
                actorId,
                DocType.Specification,
                "Second",
                null,
                "C"
            )
        );

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Specs.AlreadyExists, result.Error.Code);
    }

    [Fact]
    public async Task CreateAsync_AdminNonMember_Succeeds()
    {
        var (specRepo, cardRepo, memberRepo, _, auditWriter, snapshotRefresher, publisher) =
            CreateMocks();
        var adminUserRepo = new FakeUserRepositoryAdmin();
        var service = new SpecService(
            specRepo,
            cardRepo,
            memberRepo,
            adminUserRepo,
            auditWriter,
            snapshotRefresher,
            publisher
        );
        var projectId = NewId();
        var cardId = NewId();
        var actorId = NewId();

        cardRepo.Cards.Add(
            new Card
            {
                Id = cardId,
                ProjectId = projectId,
                Type = CardType.Goal,
            }
        );

        var result = await service.CreateAsync(
            new CreateSpecCommand(
                projectId,
                cardId,
                actorId,
                DocType.Specification,
                "Admin Spec",
                null,
                "# Spec"
            )
        );

        Assert.True(result.IsSuccess);
    }

    // ─── Audit tests ───────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_WritesAuditLog()
    {
        var (specRepo, cardRepo, memberRepo, userRepo, auditWriter, snapshotRefresher, publisher) =
            CreateMocks();
        var service = new SpecService(
            specRepo,
            cardRepo,
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
                Type = CardType.Goal,
            }
        );

        var result = await service.CreateAsync(
            new CreateSpecCommand(
                projectId,
                cardId,
                actorId,
                DocType.Specification,
                "Spec Title",
                "Desc",
                "# Hello"
            )
        );

        Assert.True(result.IsSuccess);
        var req = Assert.Single(auditWriter.Writes);
        Assert.Equal(actorId, req.ActorId);
        Assert.Equal(AuditLogScope.Project, req.Scope);
        Assert.Equal("Spec", req.EntityType);
        Assert.Equal(result.Value.Id, req.EntityId);
        Assert.Equal("Created", req.Action);
        Assert.Equal(projectId, req.ProjectId);
    }

    [Fact]
    public async Task UpdateAsync_WritesAuditLog()
    {
        var (specRepo, cardRepo, memberRepo, userRepo, auditWriter, snapshotRefresher, publisher) =
            CreateMocks();
        var service = new SpecService(
            specRepo,
            cardRepo,
            memberRepo,
            userRepo,
            auditWriter,
            snapshotRefresher,
            publisher
        );
        var projectId = NewId();
        var actorId = NewId();
        var specId = NewId();

        var spec = new Spec
        {
            Id = specId,
            ProjectId = projectId,
            Title = "Original",
            Content = "V1",
            Version = 1,
            CreatedByUserId = actorId,
        };
        specRepo.Add(spec);
        specRepo.AddVersion(
            new SpecVersion
            {
                Id = NewId(),
                SpecId = specId,
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
            new UpdateSpecCommand(projectId, specId, actorId, "Updated", null, "V2")
        );

        Assert.True(result.IsSuccess);
        var req = Assert.Single(auditWriter.Writes);
        Assert.Equal(actorId, req.ActorId);
        Assert.Equal(AuditLogScope.Project, req.Scope);
        Assert.Equal("Spec", req.EntityType);
        Assert.Equal(specId, req.EntityId);
        Assert.Equal("Updated", req.Action);
        Assert.Equal(projectId, req.ProjectId);
    }

    [Fact]
    public async Task RestoreVersionAsync_WritesAuditLog()
    {
        var (specRepo, cardRepo, memberRepo, userRepo, auditWriter, snapshotRefresher, publisher) =
            CreateMocks();
        var service = new SpecService(
            specRepo,
            cardRepo,
            memberRepo,
            userRepo,
            auditWriter,
            snapshotRefresher,
            publisher
        );
        var projectId = NewId();
        var actorId = NewId();
        var specId = NewId();

        var spec = new Spec
        {
            Id = specId,
            ProjectId = projectId,
            Title = "Spec",
            Content = "V2",
            Version = 2,
            CreatedByUserId = actorId,
        };
        specRepo.Add(spec);
        specRepo.AddVersion(
            new SpecVersion
            {
                Id = NewId(),
                SpecId = specId,
                Version = 1,
                Content = "V1",
                CreatedByUserId = actorId,
            }
        );
        specRepo.AddVersion(
            new SpecVersion
            {
                Id = NewId(),
                SpecId = specId,
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
            new RestoreSpecVersionCommand(projectId, specId, 1, actorId)
        );

        Assert.True(result.IsSuccess);
        var req = Assert.Single(auditWriter.Writes);
        Assert.Equal(actorId, req.ActorId);
        Assert.Equal(AuditLogScope.Project, req.Scope);
        Assert.Equal("Spec", req.EntityType);
        Assert.Equal(specId, req.EntityId);
        Assert.Equal("Restored", req.Action);
        Assert.Equal(projectId, req.ProjectId);
    }

    private static (
        InMemorySpecRepository specRepo,
        InMemoryCardRepository cardRepo,
        InMemoryProjectMemberRepository memberRepo,
        FakeUserRepositoryForAdmin userRepo,
        InMemoryAuditLogWriter auditWriter,
        NullSnapshotRefresher snapshotRefresher,
        FakeProjectBoardEventPublisher publisher
    ) CreateMocks()
    {
        return (
            new InMemorySpecRepository(),
            new InMemoryCardRepository(),
            new InMemoryProjectMemberRepository(),
            new FakeUserRepositoryForAdmin(),
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
