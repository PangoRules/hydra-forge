using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Util;
using HydraForge.Application.Attachments;
using HydraForge.Domain.Common;
using Microsoft.Extensions.Logging;

namespace HydraForge.Infrastructure.FileStorage;

public class S3FileStore(IAmazonS3 s3Client, string bucketName, ILogger<S3FileStore> logger)
    : IFileStore
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
            var request = new PutObjectRequest
            {
                BucketName = bucketName,
                Key = storageKey,
                InputStream = content,
                ContentType = contentType,
            };
            await s3Client.PutObjectAsync(request, ct);
            logger.LogDebug("Stored S3 object {Key} in bucket {Bucket}", storageKey, bucketName);
            return Result<string>.Success(storageKey);
        }
        catch (AmazonS3Exception ex)
        {
            logger.LogError(ex, "S3 store failed for key {Key}", storageKey);
            return Result<string>.Failure(
                new Error(
                    DomainErrorCodes.Attachments.FileStoreUnavailable,
                    "File store unavailable."
                )
            );
        }
    }

    public async Task<Result> InitializeAsync(CancellationToken ct = default)
    {
        try
        {
            var exists = await AmazonS3Util.DoesS3BucketExistV2Async(s3Client, bucketName);
            if (!exists)
            {
                await s3Client.PutBucketAsync(bucketName, ct);
                logger.LogInformation("Created S3 bucket {Bucket}", bucketName);
            }
            return Result.Success();
        }
        catch (AmazonS3Exception ex)
        {
            logger.LogError(ex, "S3 bucket init failed for {Bucket}", bucketName);
            return Result.Failure(
                new Error(
                    DomainErrorCodes.Attachments.FileStoreUnavailable,
                    "File store unavailable."
                )
            );
        }
    }

    public async Task<Result<Stream>> OpenReadAsync(
        string storageKey,
        CancellationToken ct = default
    )
    {
        try
        {
            var response = await s3Client.GetObjectAsync(bucketName, storageKey, ct);
            return Result<Stream>.Success(response.ResponseStream);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return Result<Stream>.Failure(
                new Error(DomainErrorCodes.Attachments.NotFound, "File not found.")
            );
        }
        catch (AmazonS3Exception ex)
        {
            logger.LogError(ex, "S3 read failed for key {Key}", storageKey);
            return Result<Stream>.Failure(
                new Error(
                    DomainErrorCodes.Attachments.FileStoreUnavailable,
                    "File store unavailable."
                )
            );
        }
    }

    public async Task<Result> DeleteAsync(string storageKey, CancellationToken ct = default)
    {
        try
        {
            await s3Client.DeleteObjectAsync(bucketName, storageKey, ct);
            logger.LogDebug("Deleted S3 object {Key}", storageKey);
            return Result.Success();
        }
        catch (AmazonS3Exception ex)
        {
            logger.LogError(ex, "S3 delete failed for key {Key}", storageKey);
            return Result.Failure(
                new Error(
                    DomainErrorCodes.Attachments.FileStoreUnavailable,
                    "File store unavailable."
                )
            );
        }
    }
}
