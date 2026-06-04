using HydraForge.Domain.Entities.ProjectSpace;
using HydraForge.Domain.Enums;

namespace HydraForge.Domain.Tests.Entities;

public class AuditLogEntryTests
{
    [Fact]
    public void Create_ProjectScope_SetsAllFields()
    {
        var actorId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var entityId = Guid.NewGuid();

        var entry = AuditLogEntry.Create(
            actorId: actorId,
            scope: AuditLogScope.Project,
            entityType: "Card",
            entityId: entityId,
            action: "Created",
            projectId: projectId,
            oldValue: null,
            newValue: "{\"title\":\"Test\"}"
        );

        Assert.NotEqual(Guid.Empty, entry.Id);
        Assert.Equal(actorId, entry.ActorId);
        Assert.Equal(projectId, entry.ProjectId);
        Assert.Equal(AuditLogScope.Project, entry.Scope);
        Assert.Equal("Card", entry.EntityType);
        Assert.Equal(entityId, entry.EntityId);
        Assert.Equal("Created", entry.Action);
        Assert.Null(entry.OldValue);
        Assert.Equal("{\"title\":\"Test\"}", entry.NewValue);
        Assert.True(entry.Timestamp <= DateTime.UtcNow);
        Assert.True(entry.CreatedAt <= DateTime.UtcNow);
    }

    [Fact]
    public void Create_SystemScope_SetsNullProjectId()
    {
        var actorId = Guid.NewGuid();
        var entityId = Guid.NewGuid();

        var entry = AuditLogEntry.Create(
            actorId: actorId,
            scope: AuditLogScope.System,
            entityType: "SystemSettings",
            entityId: entityId,
            action: "Updated",
            projectId: null
        );

        Assert.Equal(AuditLogScope.System, entry.Scope);
        Assert.Null(entry.ProjectId);
    }

    [Fact]
    public void Create_PersonalScope_SetsNullProjectId()
    {
        var actorId = Guid.NewGuid();
        var entityId = Guid.NewGuid();

        var entry = AuditLogEntry.Create(
            actorId: actorId,
            scope: AuditLogScope.Personal,
            entityType: "Note",
            entityId: entityId,
            action: "Created",
            projectId: null
        );

        Assert.Equal(AuditLogScope.Personal, entry.Scope);
        Assert.Null(entry.ProjectId);
    }

    [Fact]
    public void Create_ProjectScopeWithNullProjectId_Throws()
    {
        var actorId = Guid.NewGuid();

        var exception = Assert.Throws<ArgumentException>(() =>
            AuditLogEntry.Create(
                actorId: actorId,
                scope: AuditLogScope.Project,
                entityType: "Card",
                entityId: Guid.NewGuid(),
                action: "Created",
                projectId: null
            )
        );

        Assert.Contains("ProjectId is required for Project scope", exception.Message);
    }

    [Fact]
    public void Create_SystemScopeWithProjectId_Throws()
    {
        var actorId = Guid.NewGuid();
        var projectId = Guid.NewGuid();

        var exception = Assert.Throws<ArgumentException>(() =>
            AuditLogEntry.Create(
                actorId: actorId,
                scope: AuditLogScope.System,
                entityType: "Card",
                entityId: Guid.NewGuid(),
                action: "Created",
                projectId: projectId
            )
        );

        Assert.Contains("ProjectId must be null for System or Personal scope", exception.Message);
    }

    [Fact]
    public void Create_EmptyActorId_Throws()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            AuditLogEntry.Create(
                actorId: Guid.Empty,
                scope: AuditLogScope.Project,
                entityType: "Card",
                entityId: Guid.NewGuid(),
                action: "Created",
                projectId: Guid.NewGuid()
            )
        );

        Assert.Contains("ActorId is required", exception.Message);
    }

    [Fact]
    public void Create_EmptyEntityType_Throws()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            AuditLogEntry.Create(
                actorId: Guid.NewGuid(),
                scope: AuditLogScope.Project,
                entityType: "",
                entityId: Guid.NewGuid(),
                action: "Created",
                projectId: Guid.NewGuid()
            )
        );

        Assert.Contains("EntityType is required", exception.Message);
    }

    [Fact]
    public void Create_EmptyAction_Throws()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            AuditLogEntry.Create(
                actorId: Guid.NewGuid(),
                scope: AuditLogScope.Project,
                entityType: "Card",
                entityId: Guid.NewGuid(),
                action: "",
                projectId: Guid.NewGuid()
            )
        );

        Assert.Contains("Action is required", exception.Message);
    }
}