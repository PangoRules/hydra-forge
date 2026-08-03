using HydraForge.Domain.Common;
using HydraForge.Domain.Entities.PersonalSpace;

namespace HydraForge.Application.Chat;

public class DocumentService(IDocumentRepository documentRepo) : IDocumentService
{
    private readonly IDocumentRepository _documentRepo = documentRepo;

    public async Task<Result<DocumentDto>> GetByIdAsync(
        Guid documentId,
        Guid actorId,
        CancellationToken ct = default
    )
    {
        var doc = await _documentRepo.GetByIdAsync(documentId, ct);
        if (doc is null || doc.UserId != actorId)
            return Result<DocumentDto>.Failure(
                new Error(DomainErrorCodes.Chat.DocumentNotFound, "Document not found.")
            );

        return Result<DocumentDto>.Success(MapToDto(doc));
    }

    public async Task<Result<IReadOnlyList<DocumentDto>>> ListAsync(
        Guid actorId,
        string? q = null,
        CancellationToken ct = default
    )
    {
        var documents = await _documentRepo.ListByUserAsync(actorId, q, ct);
        var dtos = documents.Select(MapToDto).ToList();
        return Result<IReadOnlyList<DocumentDto>>.Success(dtos);
    }

    public async Task<Result> ArchiveAsync(
        Guid documentId,
        Guid actorId,
        CancellationToken ct = default
    )
    {
        var doc = await _documentRepo.GetByIdAsync(documentId, ct);
        if (doc is null || doc.UserId != actorId)
            return Result.Failure(
                new Error(DomainErrorCodes.Chat.DocumentNotFound, "Document not found.")
            );

        await _documentRepo.ArchiveAsync(documentId, ct);
        return Result.Success();
    }

    private static DocumentDto MapToDto(Document doc) =>
        new(
            doc.Id,
            doc.Title,
            doc.ContentType,
            doc.Language,
            doc.Version,
            doc.CreatedAt,
            doc.UpdatedAt,
            doc.ArchivedAt
        );
}
