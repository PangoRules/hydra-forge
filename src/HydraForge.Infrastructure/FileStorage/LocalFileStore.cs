using HydraForge.Application.Attachments;
using HydraForge.Domain.Common;
using Microsoft.Extensions.Logging;

namespace HydraForge.Infrastructure.FileStorage;

public class LocalFileStore(string rootPath, ILogger<LocalFileStore> logger) : IFileStore
{
    public async Task<Result<string>> StoreAsync(
        Stream content,
        string contentType,
        string storageKey,
        CancellationToken ct = default
    )
    {
        try
        {
            var fullPath = Path.Combine(rootPath, storageKey);
            var directory = Path.GetDirectoryName(fullPath);

            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            await using var fileStream = new FileStream(
                fullPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                81920,
                true
            );
            await content.CopyToAsync(fileStream, ct);

            if (logger.IsEnabled(LogLevel.Debug))
                logger.LogDebug("Stored file at {Path}", fullPath);
            return Result<string>.Success(storageKey);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to store file");
            return Result<string>.Failure(
                new Error(
                    DomainErrorCodes.Attachments.FileStoreUnavailable,
                    "File store unavailable."
                )
            );
        }
    }

    public Task<Result<Stream>> OpenReadAsync(string storageKey, CancellationToken ct = default)
    {
        var fullPath = Path.Combine(rootPath, storageKey);
        if (!File.Exists(fullPath))
            return Task.FromResult(
                Result<Stream>.Failure(
                    new Error(DomainErrorCodes.Attachments.NotFound, "File not found.")
                )
            );

        Stream stream;
        try
        {
            stream = new FileStream(
                fullPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                81920,
                true
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to open file {Key}", storageKey);
            return Task.FromResult(
                Result<Stream>.Failure(
                    new Error(
                        DomainErrorCodes.Attachments.FileStoreUnavailable,
                        "File store unavailable."
                    )
                )
            );
        }

        return Task.FromResult(Result<Stream>.Success(stream));
    }

    public Task<Result> DeleteAsync(string storageKey, CancellationToken ct = default)
    {
        var fullPath = Path.Combine(rootPath, storageKey);
        if (!File.Exists(fullPath))
            return Task.FromResult(Result.Success());

        try
        {
            File.Delete(fullPath);
            if (logger.IsEnabled(LogLevel.Debug))
                logger.LogDebug("Deleted file at {Path}", fullPath);
            return Task.FromResult(Result.Success());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete file {Key}", storageKey);
            return Task.FromResult(
                Result.Failure(
                    new Error(
                        DomainErrorCodes.Attachments.FileStoreUnavailable,
                        "File store unavailable."
                    )
                )
            );
        }
    }
}
