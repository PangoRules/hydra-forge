namespace HydraForge.Infrastructure.Tests.Audit;

using HydraForge.Application.Audit;
using HydraForge.Domain.Common;
using HydraForge.Domain.Entities.ProjectSpace;
using HydraForge.Domain.Enums;
using HydraForge.Infrastructure.Audit;
using HydraForge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

public class EfAuditLogWriterTests
{
    private static DbContextOptions<HydraForgeDbContext> CreateOptions(string? connectionString = null)
    {
        var connString = connectionString
            ?? "Host=localhost;Database=hydraforge_test;Username=postgres;Password=password";

        return new DbContextOptionsBuilder<HydraForgeDbContext>()
            .UseNpgsql(connString, o => o.UseVector())
            .Options;
    }

    [Fact]
    public void Implements_IAuditLogWriter()
    {
        var options = CreateOptions();
        using var context = new HydraForgeDbContext(options);
        var logger = NullLogger<EfAuditLogWriter>.Instance;

        var writer = new EfAuditLogWriter(context, logger);

        Assert.True(writer is IAuditLogWriter);
    }

    [Fact]
    public async Task WriteAsync_ProjectScope_AddsEntryToContext()
    {
        // Skip if no test connection string configured
        string? connectionString = Environment.GetEnvironmentVariable("HYDRAFORGE_TEST_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var options = CreateOptions(connectionString);
        using var context = new HydraForgeDbContext(options);
        var logger = NullLogger<EfAuditLogWriter>.Instance;
        var writer = new EfAuditLogWriter(context, logger);

        var request = new AuditLogRequest(
            ActorId: Guid.NewGuid(),
            Scope: AuditLogScope.Project,
            EntityType: "Card",
            EntityId: Guid.NewGuid(),
            Action: "Created",
            ProjectId: Guid.NewGuid(),
            OldValueJson: null,
            NewValueJson: "{\"title\":\"Test Card\"}"
        );

        var result = await writer.WriteAsync(request);

        Assert.True(result.IsSuccess);

        var entries = context.AuditLogEntries.Local.Where(e => e.EntityId == request.EntityId).ToList();
        Assert.Single(entries);
        Assert.Equal(request.EntityType, entries[0].EntityType);
        Assert.Equal(request.Action, entries[0].Action);
        Assert.Equal(AuditLogScope.Project, entries[0].Scope);
    }

    [Fact]
    public async Task WriteAsync_SystemScope_AddsEntryWithNullProjectId()
    {
        // Skip if no test connection string configured
        string? connectionString = Environment.GetEnvironmentVariable("HYDRAFORGE_TEST_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var options = CreateOptions(connectionString);
        using var context = new HydraForgeDbContext(options);
        var logger = NullLogger<EfAuditLogWriter>.Instance;
        var writer = new EfAuditLogWriter(context, logger);

        var request = new AuditLogRequest(
            ActorId: Guid.NewGuid(),
            Scope: AuditLogScope.System,
            EntityType: "SystemSettings",
            EntityId: Guid.NewGuid(),
            Action: "Updated",
            ProjectId: null,
            OldValueJson: null,
            NewValueJson: null
        );

        var result = await writer.WriteAsync(request);

        Assert.True(result.IsSuccess);

        var entries = context.AuditLogEntries.Local.Where(e => e.EntityId == request.EntityId).ToList();
        Assert.Single(entries);
        Assert.Equal(AuditLogScope.System, entries[0].Scope);
        Assert.Null(entries[0].ProjectId);
    }

    [Fact]
    public async Task WriteAsync_ProjectScopeWithoutProjectId_ReturnsFailure()
    {
        var options = CreateOptions();
        using var context = new HydraForgeDbContext(options);
        var logger = NullLogger<EfAuditLogWriter>.Instance;
        var writer = new EfAuditLogWriter(context, logger);

        var request = new AuditLogRequest(
            ActorId: Guid.NewGuid(),
            Scope: AuditLogScope.Project,
            EntityType: "Card",
            EntityId: Guid.NewGuid(),
            Action: "Created",
            ProjectId: null // Project scope requires ProjectId
        );

        var result = await writer.WriteAsync(request);

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Infrastructure.AuditWriteFailed, result.Error.Code);
    }
}