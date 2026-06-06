namespace HydraForge.Infrastructure.Tests.Projects;

using HydraForge.Infrastructure.Persistence;
using HydraForge.Domain.Entities.ProjectSpace;
using Microsoft.EntityFrameworkCore;

public class EfColumnRepositoryTests
{
    private static DbContextOptions<HydraForgeDbContext> CreateOptions()
    {
        return new DbContextOptionsBuilder<HydraForgeDbContext>()
            .UseNpgsql("Host=localhost;Database=hydraforge_test;Username=postgres;Password=password", o => o.UseVector())
            .Options;
    }

    [Fact]
    public void FindEntityType_Column_ReturnsNotNull()
    {
        using var context = new HydraForgeDbContext(CreateOptions());
        var model = context.Model;

        var entity = model.FindEntityType(typeof(Column));
        Assert.NotNull(entity);
    }

    [Fact]
    public void FindEntityType_Column_HasRequiredProperties()
    {
        using var context = new HydraForgeDbContext(CreateOptions());
        var model = context.Model;

        var entity = model.FindEntityType(typeof(Column));
        Assert.NotNull(entity);

        var requiredProps = new[] { "Id", "ProjectId", "Name", "Position", "WipLimit", "Color", "CreatedAt", "UpdatedAt" };
        foreach (var propName in requiredProps)
        {
            Assert.True(
                entity.GetProperties().Any(p => p.Name == propName),
                $"Column missing property: {propName}");
        }
    }

    [Fact]
    public void GetIndexes_Column_ProjectIdAndPosition_IsNotUnique()
    {
        using var context = new HydraForgeDbContext(CreateOptions());
        var model = context.Model;

        var entity = model.FindEntityType(typeof(Column));
        Assert.NotNull(entity);

        var compositeIndex = entity.GetIndexes()
            .FirstOrDefault(i => i.Properties.Any(p => p.Name == "ProjectId") &&
                                  i.Properties.Any(p => p.Name == "Position"));

        Assert.True(compositeIndex != null, "Expected index on (ProjectId, Position)");
        Assert.False(compositeIndex!.IsUnique, "Index on (ProjectId, Position) should not be unique");
    }

    [Fact]
    public void GetIndexes_Column_ProjectIdAndPosition_CoversPositionOrdering()
    {
        using var context = new HydraForgeDbContext(CreateOptions());
        var model = context.Model;

        var entity = model.FindEntityType(typeof(Column));
        Assert.NotNull(entity);

        var compositeIndex = entity.GetIndexes()
            .FirstOrDefault(i => i.Properties.Any(p => p.Name == "ProjectId") &&
                                  i.Properties.All(p => p.Name == "ProjectId" || p.Name == "Position"));

        Assert.True(compositeIndex != null, "Expected index on (ProjectId, Position)");
        var projectIdProp = compositeIndex.Properties.FirstOrDefault(p => p.Name == "ProjectId");
        var positionProp = compositeIndex.Properties.FirstOrDefault(p => p.Name == "Position");
        Assert.True(projectIdProp != null, "ProjectId property not found");
        Assert.True(positionProp != null, "Position property not found");
    }

    [Fact]
    public void GetQueryFilter_Column_HasNoGlobalQueryFilter()
    {
        using var context = new HydraForgeDbContext(CreateOptions());
        var model = context.Model;

        var entity = model.FindEntityType(typeof(Column));
        Assert.NotNull(entity);

        var queryFilter = entity.GetQueryFilter();
        Assert.True(queryFilter == null, "Column should not have a global query filter");
    }

    [Fact]
    public void GetTableName_Column_IsSnakeCasePlural()
    {
        using var context = new HydraForgeDbContext(CreateOptions());
        var model = context.Model;

        var entity = model.FindEntityType(typeof(Column));
        Assert.NotNull(entity);

        var tableName = entity.GetTableName();
        Assert.Equal("columns", tableName);
    }
}