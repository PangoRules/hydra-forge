namespace HydraForge.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Pgvector.EntityFrameworkCore;

public class DesignTimeHydraForgeDbContextFactory : IDesignTimeDbContextFactory<HydraForgeDbContext>
{
    public HydraForgeDbContext CreateDbContext(string[] args)
    {
        var connectionString = BuildConfiguration().GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Connection string 'Default' is not configured for EF Core design-time operations.");
        }

        var optionsBuilder = new DbContextOptionsBuilder<HydraForgeDbContext>();
        optionsBuilder.UseNpgsql(connectionString, o => o.UseVector());

        return new HydraForgeDbContext(optionsBuilder.Options);
    }

    private static IConfiguration BuildConfiguration()
    {
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? "Development";

        return new ConfigurationBuilder()
            .SetBasePath(ResolveServerConfigPath())
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile($"appsettings.{environment}.json", optional: true)
            .AddEnvironmentVariables()
            .Build();
    }

    private static string ResolveServerConfigPath()
    {
        var current = new DirectoryInfo(Directory.GetCurrentDirectory());

        for (var directory = current; directory is not null; directory = directory.Parent)
        {
            var serverPath = Path.Combine(directory.FullName, "src", "HydraForge.Server");
            if (File.Exists(Path.Combine(serverPath, "appsettings.json")))
            {
                return serverPath;
            }

            if (directory.Name == "HydraForge.Server" && File.Exists(Path.Combine(directory.FullName, "appsettings.json")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("Could not find HydraForge.Server appsettings.json for EF Core design-time configuration.");
    }
}
