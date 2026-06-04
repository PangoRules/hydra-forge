using HydraForge.Application.Audit;
using HydraForge.Domain.Common;
using HydraForge.Domain.Enums;

namespace HydraForge.Application.Tests.Audit;

public class AuditServiceTests
{
    [Fact]
    public async Task WriteAsync_ValidRequest_ReturnsSuccess()
    {
        // Arrange
        var mockWriter = new MockAuditLogWriter(success: true);
        var service = new AuditService(mockWriter);
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

        // Act
        var result = await service.WriteAsync(request);

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task WriteAsync_WriterFails_ReturnsAuditWriteFailedError()
    {
        // Arrange
        var mockWriter = new MockAuditLogWriter(success: false);
        var service = new AuditService(mockWriter);
        var request = new AuditLogRequest(
            ActorId: Guid.NewGuid(),
            Scope: AuditLogScope.Project,
            EntityType: "Card",
            EntityId: Guid.NewGuid(),
            Action: "Created",
            ProjectId: Guid.NewGuid(),
            OldValueJson: null,
            NewValueJson: null
        );

        // Act
        var result = await service.WriteAsync(request);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Infrastructure.AuditWriteFailed, result.Error.Code);
    }

    [Fact]
    public async Task WriteAsync_SystemScope_PassesWithNoProjectId()
    {
        // Arrange
        var mockWriter = new MockAuditLogWriter(success: true);
        var service = new AuditService(mockWriter);
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

        // Act
        var result = await service.WriteAsync(request);

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task WriteAsync_PersonalScope_PassesWithNoProjectId()
    {
        // Arrange
        var mockWriter = new MockAuditLogWriter(success: true);
        var service = new AuditService(mockWriter);
        var request = new AuditLogRequest(
            ActorId: Guid.NewGuid(),
            Scope: AuditLogScope.Personal,
            EntityType: "Note",
            EntityId: Guid.NewGuid(),
            Action: "Created",
            ProjectId: null,
            OldValueJson: null,
            NewValueJson: "{\"title\":\"My Note\"}"
        );

        // Act
        var result = await service.WriteAsync(request);

        // Assert
        Assert.True(result.IsSuccess);
    }
}

internal class MockAuditLogWriter : IAuditLogWriter
{
    private readonly bool _success;

    public MockAuditLogWriter(bool success) => _success = success;

    public Task<Result> WriteAsync(AuditLogRequest request, CancellationToken cancellationToken = default)
    {
        if (_success)
            return Task.FromResult(Result.Success());
        return Task.FromResult(Result.Failure(new Error(
            DomainErrorCodes.Infrastructure.AuditWriteFailed,
            "Audit log write failed.")));
    }
}