using HydraForge.Application.Audit;
using HydraForge.Application.Attachments;
using HydraForge.Application.Cards;
using HydraForge.Application.Projects;
using HydraForge.Domain.Common;
using HydraForge.Domain.Entities.Auth;
using HydraForge.Domain.Entities.ProjectSpace;
using HydraForge.Domain.Enums;
using Result = HydraForge.Domain.Common.Result;

namespace HydraForge.Application.Tests.Attachments;

public class AttachmentServiceTests
{
    private static Guid NewId() => Guid.NewGuid();

    [Fact]
    public async Task CreateAsync_ValidFile_ReturnsAttachmentDto()
    {
        var (service, fileStore, attachmentRepo, cardRepo, memberRepo) = CreateService();
        var projectId = NewId();
        var cardId = NewId();
        var actorId = NewId();
        var stream = new MemoryStream([1, 2, 3]);
        SetupMemberAndCard(projectId, cardId, actorId, cardRepo, memberRepo);
        fileStore.NextStoreResult = Result<string>.Success("project/x/card/y/date/2026-01-01/guid");

        var result = await service.CreateAsync(new CreateAttachmentCommand(
            projectId, cardId, actorId, "test.png", "image/png", 3, stream));

        Assert.True(result.IsSuccess);
        Assert.Equal("test.png", result.Value.FileName);
        Assert.Equal("image/png", result.Value.ContentType);
        Assert.Equal(3, result.Value.Size);
        Assert.Single(attachmentRepo.Attachments);
    }

    [Fact]
    public async Task CreateAsync_NonMember_ReturnsMembershipDenied()
    {
        var (service, _, _, _, _) = CreateService();
        var result = await service.CreateAsync(new CreateAttachmentCommand(
            NewId(), NewId(), NewId(), "test.png", "image/png", 3, new MemoryStream()));

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Projects.MembershipDenied, result.Error.Code);
    }

    [Fact]
    public async Task CreateAsync_MissingCard_ReturnsCardNotFound()
    {
        var (service, _, _, cardRepo, memberRepo) = CreateService();
        var projectId = NewId();
        var actorId = NewId();
        memberRepo.Members.Add(new ProjectMember { ProjectId = projectId, UserId = actorId, Role = MemberRole.Member });

        var result = await service.CreateAsync(new CreateAttachmentCommand(
            projectId, NewId(), actorId, "test.png", "image/png", 3, new MemoryStream()));

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Cards.NotFound, result.Error.Code);
    }

    [Fact]
    public async Task CreateAsync_FileTooLarge_ReturnsFileTooLarge()
    {
        var (service, _, _, cardRepo, memberRepo) = CreateService(maxBytes: 5);
        var projectId = NewId();
        var cardId = NewId();
        var actorId = NewId();
        SetupMemberAndCard(projectId, cardId, actorId, cardRepo, memberRepo);

        var result = await service.CreateAsync(new CreateAttachmentCommand(
            projectId, cardId, actorId, "test.png", "image/png", 100, new MemoryStream()));

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Attachments.FileTooLarge, result.Error.Code);
    }

    [Fact]
    public async Task CreateAsync_UnsupportedContentType_ReturnsUnsupportedContentType()
    {
        var (service, _, _, cardRepo, memberRepo) = CreateService();
        var projectId = NewId();
        var cardId = NewId();
        var actorId = NewId();
        SetupMemberAndCard(projectId, cardId, actorId, cardRepo, memberRepo);

        var result = await service.CreateAsync(new CreateAttachmentCommand(
            projectId, cardId, actorId, "test.exe", "application/x-executable", 3, new MemoryStream()));

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Attachments.UnsupportedContentType, result.Error.Code);
    }

    [Fact]
    public async Task CreateAsync_FileStoreFails_ReturnsFileStoreUnavailable()
    {
        var (service, fileStore, _, _, _) = CreateService();
        var projectId = NewId();
        var cardId = NewId();
        var actorId = NewId();
        var cardRepo = new InMemoryCardRepository();
        var memberRepo = new InMemoryProjectMemberRepository();
        SetupMemberAndCard(projectId, cardId, actorId, cardRepo, memberRepo);
        fileStore.NextStoreResult = Result<string>.Failure(new Error("STORE_ERROR", "Store failed"));

        var service2 = new AttachmentService(
            new InMemoryAttachmentRepository(), cardRepo, memberRepo, fileStore, new InMemoryAuditLogWriter(),
            10_000_000, AttachmentContentTypes.Allowed);

        var result = await service2.CreateAsync(new CreateAttachmentCommand(
            projectId, cardId, actorId, "test.png", "image/png", 3, new MemoryStream()));

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Attachments.FileStoreUnavailable, result.Error.Code);
    }

    [Fact]
    public async Task CreateAsync_SanitizesDisplayFileName()
    {
        var (service, fileStore, attachmentRepo, cardRepo, memberRepo) = CreateService();
        var projectId = NewId();
        var cardId = NewId();
        var actorId = NewId();
        SetupMemberAndCard(projectId, cardId, actorId, cardRepo, memberRepo);

        var result = await service.CreateAsync(new CreateAttachmentCommand(
            projectId, cardId, actorId, "../../../etc/passwd", "image/png", 3, new MemoryStream()));

        Assert.True(result.IsSuccess);
        Assert.DoesNotContain("..", result.Value.FileName);
        Assert.DoesNotContain("/", result.Value.FileName);
    }

    [Fact]
    public async Task CreateAsync_GeneratesOpaqueStorageKey()
    {
        var (service, fileStore, _, cardRepo, memberRepo) = CreateService();
        var projectId = NewId();
        var cardId = NewId();
        var actorId = NewId();
        SetupMemberAndCard(projectId, cardId, actorId, cardRepo, memberRepo);

        string? capturedKey = null;
        fileStore.CaptureStoreKey = key => capturedKey = key;

        await service.CreateAsync(new CreateAttachmentCommand(
            projectId, cardId, actorId, "test.png", "image/png", 3, new MemoryStream()));

        Assert.NotNull(capturedKey);
        Assert.StartsWith($"project/{projectId}/card/{cardId}/date/", capturedKey);
        Assert.DoesNotContain("test.png", capturedKey);
    }

    [Fact]
    public async Task CreateAsync_StoresMetadataOnlyAfterFileStoreSuccess()
    {
        var (service, _, attachmentRepo, cardRepo, memberRepo) = CreateService();
        var projectId = NewId();
        var cardId = NewId();
        var actorId = NewId();
        SetupMemberAndCard(projectId, cardId, actorId, cardRepo, memberRepo);

        await service.CreateAsync(new CreateAttachmentCommand(
            projectId, cardId, actorId, "test.png", "image/png", 3, new MemoryStream()));

        Assert.Single(attachmentRepo.Attachments);
        Assert.Equal("test.png", attachmentRepo.Attachments[0].FileName);
    }

    [Fact]
    public async Task DeleteAsync_RemovesMetadataThenAttemptsFileDelete()
    {
        var (service, fileStore, attachmentRepo, cardRepo, memberRepo) = CreateService();
        var projectId = NewId();
        var cardId = NewId();
        var actorId = NewId();
        var attachmentId = NewId();
        SetupMemberAndCard(projectId, cardId, actorId, cardRepo, memberRepo);
        attachmentRepo.Attachments.Add(new Attachment
        {
            Id = attachmentId,
            CardId = cardId,
            FileName = "test.png",
            ContentType = "image/png",
            Size = 3,
            StoragePath = "some/path/key"
        });
        var deleteCalled = false;
        fileStore.DeleteCapture = () => deleteCalled = true;

        var result = await service.DeleteAsync(projectId, cardId, attachmentId, actorId);

        Assert.True(result.IsSuccess);
        Assert.Empty(attachmentRepo.Attachments);
        Assert.True(deleteCalled);
    }

    [Fact]
    public async Task DeleteAsync_FileDeleteFails_IsNonFatal()
    {
        var (service, fileStore, attachmentRepo, cardRepo, memberRepo) = CreateService();
        var projectId = NewId();
        var cardId = NewId();
        var actorId = NewId();
        var attachmentId = NewId();
        SetupMemberAndCard(projectId, cardId, actorId, cardRepo, memberRepo);
        attachmentRepo.Attachments.Add(new Attachment
        {
            Id = attachmentId,
            CardId = cardId,
            FileName = "test.png",
            ContentType = "image/png",
            Size = 3,
            StoragePath = "some/path/key"
        });
        fileStore.DeleteThrow = new Exception("delete failed");

        var result = await service.DeleteAsync(projectId, cardId, attachmentId, actorId);

        Assert.True(result.IsSuccess);
        Assert.Empty(attachmentRepo.Attachments);
    }

    [Fact]
    public async Task ListAsync_NonMember_ReturnsMembershipDenied()
    {
        var (service, _, _, _, _) = CreateService();
        var result = await service.ListAsync(NewId(), NewId(), NewId());

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Projects.MembershipDenied, result.Error.Code);
    }

    [Fact]
    public async Task ListAsync_MissingCard_ReturnsCardNotFound()
    {
        var (service, _, _, _, memberRepo) = CreateService();
        var projectId = NewId();
        var actorId = NewId();
        memberRepo.Members.Add(new ProjectMember { ProjectId = projectId, UserId = actorId, Role = MemberRole.Member });

        var result = await service.ListAsync(projectId, NewId(), actorId);

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Cards.NotFound, result.Error.Code);
    }

    [Fact]
    public async Task DownloadAsync_NotFound_ReturnsNotFound()
    {
        var (service, _, _, cardRepo, memberRepo) = CreateService();
        var projectId = NewId();
        var cardId = NewId();
        var actorId = NewId();
        SetupMemberAndCard(projectId, cardId, actorId, cardRepo, memberRepo);

        var result = await service.DownloadAsync(projectId, cardId, NewId(), actorId);

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Attachments.NotFound, result.Error.Code);
    }

    private static (AttachmentService service, FakeFileStore fileStore, InMemoryAttachmentRepository attachmentRepo, InMemoryCardRepository cardRepo, InMemoryProjectMemberRepository memberRepo) CreateService(
        long maxBytes = 10_000_000)
    {
        var attachmentRepo = new InMemoryAttachmentRepository();
        var cardRepo = new InMemoryCardRepository();
        var memberRepo = new InMemoryProjectMemberRepository();
        var fileStore = new FakeFileStore();
        var auditWriter = new InMemoryAuditLogWriter();
        var allowedTypes = AttachmentContentTypes.Allowed;

        var service = new AttachmentService(
            attachmentRepo, cardRepo, memberRepo, fileStore, auditWriter, maxBytes, allowedTypes);

        return (service, fileStore, attachmentRepo, cardRepo, memberRepo);
    }

    private static void SetupMemberAndCard(Guid projectId, Guid cardId, Guid userId, InMemoryCardRepository cardRepo, InMemoryProjectMemberRepository memberRepo)
    {
        memberRepo.Members.Add(new ProjectMember { ProjectId = projectId, UserId = userId, Role = MemberRole.Member });
        cardRepo.Cards.Add(new Card { Id = cardId, ProjectId = projectId, ColumnId = Guid.NewGuid(), CardNumber = 1, Title = "Card" });
    }

    private sealed class FakeFileStore : IFileStore
    {
        public Result<string>? NextStoreResult;
        public Action<string>? CaptureStoreKey;
        public Exception? DeleteThrow;
        public Action? DeleteCapture;

        public Task<Result<string>> StoreAsync(Stream content, string contentType, string storageKey, CancellationToken ct = default)
        {
            if (NextStoreResult != null)
                return Task.FromResult(NextStoreResult);
            CaptureStoreKey?.Invoke(storageKey);
            return Task.FromResult(Result<string>.Success(storageKey));
        }

        public Task<Result<Stream>> OpenReadAsync(string storageKey, CancellationToken ct = default)
            => Task.FromResult(Result<Stream>.Success(new MemoryStream([1, 2, 3])));

        public Task<Result> DeleteAsync(string storageKey, CancellationToken ct = default)
        {
            if (DeleteThrow != null)
                throw DeleteThrow;
            DeleteCapture?.Invoke();
            return Task.FromResult(Result.Success());
        }
    }

    private sealed class InMemoryAttachmentRepository : IAttachmentRepository
    {
        public List<Attachment> Attachments { get; } = [];

        public Task<Attachment?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(Attachments.FirstOrDefault(a => a.Id == id));

        public Task<IReadOnlyList<Attachment>> ListByCardAsync(Guid cardId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Attachment>>(Attachments.Where(a => a.CardId == cardId).ToList());

        public Task AddAsync(Attachment attachment, CancellationToken ct = default)
        {
            Attachments.Add(attachment);
            return Task.CompletedTask;
        }

        public Task DeleteAsync(Guid id, CancellationToken ct = default)
        {
            Attachments.RemoveAll(a => a.Id == id);
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryCardRepository : ICardRepository
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
        public Task UpdateAsync(Card card, CancellationToken ct = default)
        {
            var idx = Cards.FindIndex(c => c.Id == card.Id);
            if (idx >= 0) Cards[idx] = card;
            return Task.CompletedTask;
        }
        public Task UpdateRangeAsync(IReadOnlyList<Card> cards, CancellationToken ct = default)
        {
            foreach (var c in cards)
            {
                var idx = Cards.FindIndex(x => x.Id == c.Id);
                if (idx >= 0) Cards[idx] = c;
            }
            return Task.CompletedTask;
        }
        public Task DeleteAsync(Guid cardId, CancellationToken ct = default) { Cards.RemoveAll(c => c.Id == cardId); return Task.CompletedTask; }
        public Task CompactColumnPositionsAsync(Guid columnId, int exceptPosition, CancellationToken ct = default) => Task.CompletedTask;
        public Task<int> CountByColumnIdAsync(Guid columnId, CancellationToken ct = default)
            => Task.FromResult(Cards.Count(c => c.ColumnId == columnId && c.ArchivedAt == null));
    }

    private sealed class InMemoryProjectMemberRepository : IProjectMemberRepository
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
        public Task UpdateMemberAsync(ProjectMember member, CancellationToken ct = default)
        {
            var idx = Members.FindIndex(m => m.Id == member.Id);
            if (idx >= 0) Members[idx] = member;
            return Task.CompletedTask;
        }
        public Task RemoveMemberAsync(Guid id, CancellationToken ct = default) { Members.RemoveAll(m => m.Id == id); return Task.CompletedTask; }
    }

    private sealed class InMemoryAuditLogWriter : IAuditLogWriter
    {
        public Task<Result> WriteAsync(AuditLogRequest request, CancellationToken ct = default) => Task.FromResult(Result.Success());
    }
}