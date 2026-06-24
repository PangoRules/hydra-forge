using HydraForge.Application.Attachments;
using HydraForge.Infrastructure.Attachments;
using HydraForge.Infrastructure.FileStorage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HydraForge.Infrastructure.Tests.Attachments;

[Collection("Environment variable tests")]
public class AttachmentServiceCollectionExtensionsTests
{
    [Fact]
    public void AddAttachmentServices_FileStorageProviderEnvironmentVariableOverridesConfiguration()
    {
        var previousProvider = Environment.GetEnvironmentVariable("FILE_STORAGE_PROVIDER");

        try
        {
            Environment.SetEnvironmentVariable("FILE_STORAGE_PROVIDER", "S3");

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["FileStorage:Provider"] = "Local",
                    ["FileStorage:S3:BucketName"] = "hydraforge-attachments",
                    ["FileStorage:S3:Region"] = "us-east-1",
                    ["FileStorage:S3:AccessKey"] = "minioadmin",
                    ["FileStorage:S3:SecretKey"] = "minioadmin",
                    ["FileStorage:S3:Endpoint"] = "http://localhost:9000",
                    ["FileStorage:S3:ForcePathStyle"] = "true",
                })
                .Build();

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddAttachmentServices(configuration);

            using var provider = services.BuildServiceProvider();

            Assert.IsType<S3FileStore>(provider.GetRequiredService<IFileStore>());
        }
        finally
        {
            Environment.SetEnvironmentVariable("FILE_STORAGE_PROVIDER", previousProvider);
        }
    }

    [Fact]
    public async Task AddAttachmentServices_FileStoragePathEnvironmentVariableOverridesConfiguration()
    {
        var previousPath = Environment.GetEnvironmentVariable("FILE_STORAGE_PATH");
        var envPath = Path.Combine(Path.GetTempPath(), "hydraforge-tests", Guid.NewGuid().ToString());
        var configPath = Path.Combine(Path.GetTempPath(), "hydraforge-tests", Guid.NewGuid().ToString());
        var storageKey = "user/cards/card/file";

        try
        {
            Environment.SetEnvironmentVariable("FILE_STORAGE_PATH", envPath);

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["FileStorage:Provider"] = "Local",
                    ["FileStorage:LocalPath"] = configPath,
                })
                .Build();

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddAttachmentServices(configuration);

            await using var provider = services.BuildServiceProvider();
            var fileStore = provider.GetRequiredService<IFileStore>();

            var result = await fileStore.StoreAsync(
                new MemoryStream([1, 2, 3]),
                "application/octet-stream",
                storageKey
            );

            Assert.True(result.IsSuccess);
            Assert.True(File.Exists(Path.Combine(envPath, storageKey)));
            Assert.False(File.Exists(Path.Combine(configPath, storageKey)));
        }
        finally
        {
            Environment.SetEnvironmentVariable("FILE_STORAGE_PATH", previousPath);
            if (Directory.Exists(envPath))
                Directory.Delete(envPath, recursive: true);
            if (Directory.Exists(configPath))
                Directory.Delete(configPath, recursive: true);
        }
    }
}

[CollectionDefinition("Environment variable tests", DisableParallelization = true)]
public class EnvironmentVariableTestCollection;
