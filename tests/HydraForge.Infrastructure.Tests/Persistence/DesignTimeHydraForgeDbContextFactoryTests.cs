namespace HydraForge.Infrastructure.Tests.Persistence;

using HydraForge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

public class DesignTimeHydraForgeDbContextFactoryTests
{
    [Fact]
    public void CreateDbContext_UsesConnectionStringFromConfiguration()
    {
        const string variableName = "ConnectionStrings__Default";
        const string expected = "Host=example.local;Port=15432;Database=hydraforge_test;Username=test_user;Password=test_password";
        var original = Environment.GetEnvironmentVariable(variableName);

        try
        {
            Environment.SetEnvironmentVariable(variableName, expected);

            using var context = new DesignTimeHydraForgeDbContextFactory().CreateDbContext([]);

            var connectionString = new NpgsqlConnectionStringBuilder(context.Database.GetDbConnection().ConnectionString);
            Assert.Equal("example.local", connectionString.Host);
            Assert.Equal(15432, connectionString.Port);
            Assert.Equal("hydraforge_test", connectionString.Database);
            Assert.Equal("test_user", connectionString.Username);
        }
        finally
        {
            Environment.SetEnvironmentVariable(variableName, original);
        }
    }
}
