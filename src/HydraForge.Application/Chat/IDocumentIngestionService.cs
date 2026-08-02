using HydraForge.Domain.Common;
using HydraForge.Domain.Entities.PersonalSpace;

namespace HydraForge.Application.Chat;

public interface IDocumentIngestionService
{
    Task<Result<Document>> IngestAsync(
        Guid userId,
        string title,
        string content,
        string contentType,
        Stream? stream = null,
        CancellationToken ct = default
    );
}
