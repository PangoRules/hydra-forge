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

        project.Archive();

        Assert.NotNull(project.ArchivedAt);
        Assert.True(project.ArchivedAt >= beforeArchive);
        Assert.True(project.ArchivedAt <= DateTime.UtcNow);
    }

    [Fact]
    public void UpdateDetails_SetsPropertiesAndTimestamp()
    {
        var project = new Project();
        var beforeUpdate = DateTime.UtcNow;

        project.UpdateDetails("New Name", "New Desc", "https://git.example.com", "GitHub");

        Assert.Equal("New Name", project.Name);
        Assert.Equal("New Desc", project.Description);
        Assert.Equal("https://git.example.com", project.GitRemoteUrl);
        Assert.Equal("GitHub", project.GitProvider);
        Assert.True(project.UpdatedAt >= beforeUpdate);
        Assert.True(project.UpdatedAt <= DateTime.UtcNow);
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