using HydraForge.Domain.Entities.ProjectSpace;
using HydraForge.Domain.Enums;

namespace HydraForge.Domain.Tests.Entities;

public class ProjectTests
{
    [Fact]
    public void Create_DefaultValues_SetsIdAndTimestamps()
    {
        var project = new Project();

        Assert.NotEqual(Guid.Empty, project.Id);
        Assert.True(project.CreatedAt <= DateTime.UtcNow);
        Assert.True(project.UpdatedAt <= DateTime.UtcNow);
    }

    [Fact]
    public void ArchivedAt_DefaultValue_IsNull()
    {
        var project = new Project();
        Assert.Null(project.ArchivedAt);
    }

    [Fact]
    public void Archive_SetsArchivedAtToNow()
    {
        var project = new Project { Name = "Test", Description = "Test project" };
        var beforeArchive = DateTime.UtcNow;

        project.ArchivedAt = DateTime.UtcNow;
        var afterArchive = DateTime.UtcNow;

        Assert.NotNull(project.ArchivedAt);
        Assert.True(project.ArchivedAt >= beforeArchive);
        Assert.True(project.ArchivedAt <= afterArchive);
    }

    [Fact]
    public void Unarchive_ClearsArchivedAt()
    {
        var project = new Project { ArchivedAt = DateTime.UtcNow };

        project.ArchivedAt = null;

        Assert.Null(project.ArchivedAt);
    }

    [Fact]
    public void Columns_DefaultValue_IsEmptyList()
    {
        var project = new Project();
        Assert.NotNull(project.Columns);
        Assert.Empty(project.Columns);
    }
}