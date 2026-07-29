using HydraForge.Application.Audit;
using HydraForge.Infrastructure.Audit;
using HydraForge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HydraForge.Infrastructure.Tests.Audit;

public class EfAuditLogReaderTests
{
    private static DbContextOptions<HydraForgeDbContext> CreateOptions(
        string? connectionString = null
    )
    {
        var connString =
            connectionString
            ?? "Host=localhost;Database=hydraforge_test;Username=postgres;Password=password";

        return new DbContextOptionsBuilder<HydraForgeDbContext>()
            .UseNpgsql(connString, o => o.UseVector())
            .Options;
    }

    [Fact]
    public async Task QueryAsync_EmptyDb_ReturnsEmptyResult()
    {
        string? connectionString = Environment.GetEnvironmentVariable(
            "HYDRAFORGE_TEST_CONNECTION_STRING"
        );
        if (string.IsNullOrWhiteSpace(connectionString))
            return;

        var options = CreateOptions(connectionString);
        using var db = new HydraForgeDbContext(options);
        var reader = new EfAuditLogReader(db);

        var result = await reader.QueryAsync(new AuditLogQuery());

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
    }

    [Fact]
    public async Task QueryAsync_FilterByEntityType_ReturnsMatching()
    {
        string? connectionString = Environment.GetEnvironmentVariable(
            "HYDRAFORGE_TEST_CONNECTION_STRING"
        );
        if (string.IsNullOrWhiteSpace(connectionString))
            return;

        var options = CreateOptions(connectionString);
        using var db = new HydraForgeDbContext(options);
        db.AuditLogEntries.Add(Domain.Entities.ProjectSpace.AuditLogEntry.Create(
            Guid.NewGuid(), Domain.Enums.AuditLogScope.Project, "Card", Guid.NewGuid(), "Created"));
        db.AuditLogEntries.Add(Domain.Entities.ProjectSpace.AuditLogEntry.Create(
            Guid.NewGuid(), Domain.Enums.AuditLogScope.Project, "Column", Guid.NewGuid(), "Created"));
        await db.SaveChangesAsync();

        var reader = new EfAuditLogReader(db);
        var result = await reader.QueryAsync(new AuditLogQuery { EntityType = "Card" });

        Assert.Single(result.Items);
        Assert.Equal("Card", result.Items[0].EntityType);
    }
}
