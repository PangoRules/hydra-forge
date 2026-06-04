using HydraForge.Application.Audit;
using HydraForge.Domain.Common;

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
            ProjectId: Guid.NewGuid(),
            EntityType: "Card",
            EntityId: Guid.NewGuid(),
            Action: "Created",
            OldValueJson: null,
            NewValueJson: "{\"title\":\"Test Card\"}"
        );

        // Act
        var result = await service.WriteAsync(request);

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task WriteAsync_MissingActorId_ReturnsFailure()
    {
        // Arrange
        var mockWriter = new MockAuditLogWriter(success: true);
        var service = new AuditService(mockWriter);
        var request = new AuditLogRequest(
            ActorId: Guid.Empty,
            ProjectId: Guid.NewGuid(),
            EntityType: "Card",
            EntityId: Guid.NewGuid(),
            Action: "Created",
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
    public async Task WriteAsync_MissingEntityType_ReturnsFailure()
    {
        // Arrange
        var mockWriter = new MockAuditLogWriter(success: true);
        var service = new AuditService(mockWriter);
        var request = new AuditLogRequest(
            ActorId: Guid.NewGuid(),
            ProjectId: Guid.NewGuid(),
            EntityType: "",
            EntityId: Guid.NewGuid(),
            Action: "Created",
            OldValueJson: null,
            NewValueJson: null
        );

        // Act
        var result = await service.WriteAsync(request);

        // Assert
        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task WriteAsync_MissingAction_ReturnsFailure()
    {
        // Arrange
        var mockWriter = new MockAuditLogWriter(success: true);
        var service = new AuditService(mockWriter);
        var request = new AuditLogRequest(
            ActorId: Guid.NewGuid(),
            ProjectId: Guid.NewGuid(),
            EntityType: "Card",
            EntityId: Guid.NewGuid(),
            Action: "",
            OldValueJson: null,
            NewValueJson: null
        );

        // Act
        var result = await service.WriteAsync(request);

        // Assert
        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task WriteAsync_WriterFails_ReturnsAuditWriteFailedError()
    {
        // Arrange
        var mockWriter = new MockAuditLogWriter(success: false);
        var service = new AuditService(mockWriter);
        var request = new AuditLogRequest(
            ActorId: Guid.NewGuid(),
            ProjectId: Guid.NewGuid(),
            EntityType: "Card",
            EntityId: Guid.NewGuid(),
            Action: "Created",
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
    public async Task WriteAsync_EmptyProjectId_PassesValidation()
    {
        // Arrange
        var mockWriter = new MockAuditLogWriter(success: true);
        var service = new AuditService(mockWriter);
        var request = new AuditLogRequest(
            ActorId: Guid.NewGuid(),
            ProjectId: null, // ProjectId is optional
            EntityType: "Card",
            EntityId: Guid.NewGuid(),
            Action: "Created",
            OldValueJson: null,
            NewValueJson: null
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