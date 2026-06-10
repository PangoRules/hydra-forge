using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Util;
using HydraForge.Application.Attachments;
using HydraForge.Domain.Common;
using Microsoft.Extensions.Logging;

namespace HydraForge.Infrastructure.FileStorage;

public class S3FileStore : IFileStore
{
    private readonly IAmazonS3 _s3Client;
    private readonly string _bucketName;
    private readonly ILogger<S3FileStore> _logger;

    public S3FileStore(IAmazonS3 s3Client, string bucketName, ILogger<S3FileStore> logger)
    {
        _s3Client = s3Client;
        _bucketName = bucketName;
        _logger = logger;
    }

    public async Task<Result<string>> StoreAsync(Stream content, string contentType, string storageKey, CancellationToken ct = default)
    {
        var key = Guid.NewGuid().ToString();
        try
        {
            var request = new PutObjectRequest
            {
                BucketName = _bucketName,
                Key = key,
                InputStream = content,
                ContentType = contentType,
            };
            await _s3Client.PutObjectAsync(request, ct);
            _logger.LogDebug("Stored S3 object {Key} in bucket {Bucket}", key, _bucketName);
            return Result<string>.Success(key);
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "S3 store failed for key {Key}", key);
            return Result<string>.Failure(new Error(DomainErrorCodes.Attachments.FileStoreUnavailable, "File store unavailable."));
        }
    }

    public async Task<Result> InitializeAsync(CancellationToken ct = default)
    {
        try
        {
            var exists = await AmazonS3Util.DoesS3BucketExistV2Async(_s3Client, _bucketName);
            if (!exists)
            {
                await _s3Client.PutBucketAsync(_bucketName, ct);
                _logger.LogInformation("Created S3 bucket {Bucket}", _bucketName);
            }
            return Result.Success();
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "S3 bucket init failed for {Bucket}", _bucketName);
            return Result.Failure(new Error(DomainErrorCodes.Attachments.FileStoreUnavailable, "File store unavailable."));
        }
    }

    public async Task<Result<Stream>> OpenReadAsync(string storageKey, CancellationToken ct = default)
    {
        try
        {
            var response = await _s3Client.GetObjectAsync(_bucketName, storageKey, ct);
            return Result<Stream>.Success(response.ResponseStream);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return Result<Stream>.Failure(new Error(DomainErrorCodes.Attachments.NotFound, "File not found."));
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "S3 read failed for key {Key}", storageKey);
            return Result<Stream>.Failure(new Error(DomainErrorCodes.Attachments.FileStoreUnavailable, "File store unavailable."));
        }
    }

    public async Task<Result> DeleteAsync(string storageKey, CancellationToken ct = default)
    {
        try
        {
            await _s3Client.DeleteObjectAsync(_bucketName, storageKey, ct);
            _logger.LogDebug("Deleted S3 object {Key}", storageKey);
            return Result.Success();
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "S3 delete failed for key {Key}", storageKey);
            return Result.Failure(new Error(DomainErrorCodes.Attachments.FileStoreUnavailable, "File store unavailable."));
        }
    }
}