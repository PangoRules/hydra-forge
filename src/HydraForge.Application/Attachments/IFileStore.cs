using HydraForge.Domain.Common;

namespace HydraForge.Application.Attachments;

public interface IFileStore
{
    Task<Result<string>> StoreAsync(Stream content, string contentType, string storageKey, CancellationToken ct = default);
    Task<Result<Stream>> OpenReadAsync(string storageKey, CancellationToken ct = default);
    Task<Result> DeleteAsync(string storageKey, CancellationToken ct = default);
}