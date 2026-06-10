using HydraForge.Domain.Common;

namespace HydraForge.Application.Attachments;

public interface IFileStore
{
    Task<Result<string>> StoreAsync(Stream content, string contentType, string storageKey, CancellationToken ct = default);
    Task<Result<Stream>> OpenReadAsync(string storageKey, CancellationToken ct = default);
    Task<Result> DeleteAsync(string storageKey, CancellationToken ct = default);

    /// <summary>
    /// Optional one-time initialization (e.g. bucket creation for S3/MinIO).
    /// Default no-op — override only when store needs setup.
    /// </summary>
    Task<Result> InitializeAsync(CancellationToken ct = default) => Task.FromResult(Result.Success());
}