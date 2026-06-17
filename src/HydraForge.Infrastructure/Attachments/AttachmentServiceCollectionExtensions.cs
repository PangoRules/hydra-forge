using Amazon;
using Amazon.S3;
using HydraForge.Application.Attachments;
using HydraForge.Infrastructure.Attachments;
using HydraForge.Infrastructure.FileStorage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HydraForge.Infrastructure.Attachments;

public static class AttachmentServiceCollectionExtensions
{
    public static IServiceCollection AddAttachmentServices(this IServiceCollection services, IConfiguration configuration)
    {
        var provider = Environment.GetEnvironmentVariable("FILE_STORAGE_PROVIDER")
            ?? configuration["FileStorage:Provider"]
            ?? "Local";
        var maxBytes = long.TryParse(configuration["FileStorage:MaxBytes"], out var mb) ? mb : 10_000_000L;
        var allowedTypes = AttachmentContentTypes.Allowed;

        if (provider.Equals("S3", StringComparison.OrdinalIgnoreCase))
        {
            var bucketName = configuration["FileStorage:S3:BucketName"]
                ?? throw new InvalidOperationException("FileStorage:S3:BucketName is required when FileStorage:Provider=S3");
            var region = configuration["FileStorage:S3:Region"]
                ?? throw new InvalidOperationException("FileStorage:S3:Region is required");
            var accessKey = configuration["FileStorage:S3:AccessKey"]
                ?? throw new InvalidOperationException("FileStorage:S3:AccessKey is required");
            var secretKey = configuration["FileStorage:S3:SecretKey"]
                ?? throw new InvalidOperationException("FileStorage:S3:SecretKey is required");
            var endpoint = configuration["FileStorage:S3:Endpoint"];
            var forcePathStyle = bool.TryParse(configuration["FileStorage:S3:ForcePathStyle"], out var fps) && fps;

            services.AddSingleton<IAmazonS3>(sp =>
            {
                var config = new AmazonS3Config
                {
                    RegionEndpoint = RegionEndpoint.GetBySystemName(region),
                    ForcePathStyle = forcePathStyle,
                };
                if (!string.IsNullOrEmpty(endpoint))
                    config.ServiceURL = endpoint;

                return new AmazonS3Client(accessKey, secretKey, config);
            });
            services.AddSingleton<IFileStore>(sp =>
                new S3FileStore(
                    sp.GetRequiredService<IAmazonS3>(),
                    bucketName,
                    sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<S3FileStore>>()));
        }
        else
        {
            var localPath = Environment.GetEnvironmentVariable("FILE_STORAGE_PATH")
                ?? configuration["FileStorage:LocalPath"]
                ?? Path.Combine(AppContext.BaseDirectory, "App_Data", "attachments");
            services.AddSingleton<IFileStore>(sp =>
                new LocalFileStore(
                    localPath,
                    sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<LocalFileStore>>()));
        }

        services.AddScoped<IAttachmentRepository, EfAttachmentRepository>();
        services.AddScoped(sp =>
        {
            var fileStore = sp.GetRequiredService<IFileStore>();
            var attachmentRepo = sp.GetRequiredService<IAttachmentRepository>();
            var cardRepo = sp.GetRequiredService<HydraForge.Application.Cards.ICardRepository>();
            var memberRepo = sp.GetRequiredService<HydraForge.Application.Projects.IProjectMemberRepository>();
            var auditWriter = sp.GetRequiredService<HydraForge.Application.Audit.IAuditLogWriter>();
            return new AttachmentService(attachmentRepo, cardRepo, memberRepo, fileStore, auditWriter, maxBytes, allowedTypes);
        });

        return services;
    }
}
