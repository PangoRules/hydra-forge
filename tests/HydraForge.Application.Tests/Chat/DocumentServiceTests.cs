namespace HydraForge.Application.Tests.Chat;

using HydraForge.Application.Chat;
using HydraForge.Domain.Common;
using HydraForge.Domain.Entities.PersonalSpace;

public class DocumentServiceTests
{
    private static Guid NewId() => Guid.NewGuid();

    private sealed class FakeDocumentRepo : IDocumentRepository
    {
        public Dictionary<Guid, Document> Documents { get; } = [];

        public Task<Document?> GetByIdAsync(Guid documentId, CancellationToken ct = default) =>
            Task.FromResult(Documents.TryGetValue(documentId, out var d) ? d : null);

        public Task<IReadOnlyList<Document>> ListByUserAsync(
            Guid userId,
            string? q = null,
            CancellationToken ct = default
        ) =>
            Task.FromResult<IReadOnlyList<Document>>(
                Documents
                    .Values.Where(d =>
                        d.UserId == userId
                        && d.ArchivedAt == null
                        && (string.IsNullOrWhiteSpace(q) || d.Title.Contains(q))
                    )
                    .ToList()
            );

        public Task AddAsync(Document document, CancellationToken ct = default)
        {
            Documents[document.Id] = document;
            return Task.CompletedTask;
        }

        public Task ArchiveAsync(Guid documentId, CancellationToken ct = default)
        {
            if (Documents.TryGetValue(documentId, out var doc))
                doc.ArchivedAt = DateTime.UtcNow;
            return Task.CompletedTask;
        }
    }

    private static (DocumentService service, FakeDocumentRepo repo) CreateSut()
    {
        var repo = new FakeDocumentRepo();
        return (new DocumentService(repo), repo);
    }

    [Fact]
    public async Task GetByIdAsync_Owner_ReturnsDocument()
    {
        var (service, repo) = CreateSut();
        var userId = NewId();
        var doc = new Document
        {
            Id = NewId(),
            UserId = userId,
            Title = "Notes",
            ContentType = "text/plain",
        };
        repo.Documents[doc.Id] = doc;

        var result = await service.GetByIdAsync(doc.Id, userId);

        Assert.True(result.IsSuccess);
        Assert.Equal(doc.Title, result.Value.Title);
    }

    [Fact]
    public async Task GetByIdAsync_NotOwner_ReturnsNotFound()
    {
        var (service, repo) = CreateSut();
        var doc = new Document
        {
            Id = NewId(),
            UserId = NewId(),
            Title = "Notes",
            ContentType = "text/plain",
        };
        repo.Documents[doc.Id] = doc;

        var result = await service.GetByIdAsync(doc.Id, NewId());

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Chat.DocumentNotFound, result.Error.Code);
    }

    [Fact]
    public async Task GetByIdAsync_DocumentMissing_ReturnsNotFound()
    {
        var (service, _) = CreateSut();

        var result = await service.GetByIdAsync(NewId(), NewId());

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Chat.DocumentNotFound, result.Error.Code);
    }

    [Fact]
    public async Task ListAsync_ReturnsOnlyOwnersDocuments()
    {
        var (service, repo) = CreateSut();
        var userId = NewId();
        repo.Documents[NewId()] = new Document
        {
            Id = NewId(),
            UserId = userId,
            Title = "Mine",
            ContentType = "text/plain",
        };
        repo.Documents[NewId()] = new Document
        {
            Id = NewId(),
            UserId = NewId(),
            Title = "Someone Else's",
            ContentType = "text/plain",
        };

        var result = await service.ListAsync(userId);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value);
        Assert.Equal("Mine", result.Value[0].Title);
    }

    [Fact]
    public async Task ArchiveAsync_Owner_ArchivesDocument()
    {
        var (service, repo) = CreateSut();
        var userId = NewId();
        var doc = new Document
        {
            Id = NewId(),
            UserId = userId,
            Title = "Notes",
            ContentType = "text/plain",
        };
        repo.Documents[doc.Id] = doc;

        var result = await service.ArchiveAsync(doc.Id, userId);

        Assert.True(result.IsSuccess);
        Assert.NotNull(doc.ArchivedAt);
    }

    [Fact]
    public async Task ArchiveAsync_NotOwner_ReturnsNotFound()
    {
        var (service, repo) = CreateSut();
        var doc = new Document
        {
            Id = NewId(),
            UserId = NewId(),
            Title = "Notes",
            ContentType = "text/plain",
        };
        repo.Documents[doc.Id] = doc;

        var result = await service.ArchiveAsync(doc.Id, NewId());

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Chat.DocumentNotFound, result.Error.Code);
        Assert.Null(doc.ArchivedAt);
    }
}
