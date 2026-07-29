namespace HydraForge.Infrastructure.Tests.Auth;

using HydraForge.Domain.Entities.Auth;
using HydraForge.Infrastructure.Auth;
using HydraForge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public class EfUserRepositoryTests
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
    public async Task CreateAsync_PersistsUserAsIs()
    {
        string? connectionString = Environment.GetEnvironmentVariable(
            "HYDRAFORGE_TEST_CONNECTION_STRING"
        );
        if (string.IsNullOrWhiteSpace(connectionString))
            return;

        var options = CreateOptions(connectionString);
        using var context = new HydraForgeDbContext(options);
        var repo = new EfUserRepository(context);

        var user = User.Create(
            "createuser",
            "Create",
            "User",
            "createuser@localhost",
            "hashplaceholder",
            isAdmin: false
        );

        await repo.CreateAsync(user);

        var saved = await context.Users.FindAsync(user.Id);
        Assert.NotNull(saved);
        Assert.Equal("createuser", saved.Username);
        Assert.Equal("Create", saved.Name);
        Assert.Equal("User", saved.LastName);
        Assert.Equal("createuser@localhost", saved.Email);
        Assert.False(saved.IsAdmin);
        Assert.False(saved.IsDisabled);
    }

    [Fact]
    public async Task ListAsync_FiltersBySearch()
    {
        string? connectionString = Environment.GetEnvironmentVariable(
            "HYDRAFORGE_TEST_CONNECTION_STRING"
        );
        if (string.IsNullOrWhiteSpace(connectionString))
            return;

        var options = CreateOptions(connectionString);
        using var context = new HydraForgeDbContext(options);
        var repo = new EfUserRepository(context);

        var alice = User.Create("alice", "Alice", "Smith", "alice@localhost", "hash", false);
        var bob = User.Create("bob", "Bob", "Jones", "bob@localhost", "hash", false);
        var carol = User.Create("carol", "Carol", "White", "carol@localhost", "hash", false);
        await repo.CreateAsync(alice);
        await repo.CreateAsync(bob);
        await repo.CreateAsync(carol);

        var results = await repo.ListAsync(0, 10, "bob", CancellationToken.None);

        Assert.Single(results);
        Assert.Equal("bob", results[0].Username);
    }

    [Fact]
    public async Task CountAsync_MatchesListAsyncFilter()
    {
        string? connectionString = Environment.GetEnvironmentVariable(
            "HYDRAFORGE_TEST_CONNECTION_STRING"
        );
        if (string.IsNullOrWhiteSpace(connectionString))
            return;

        var options = CreateOptions(connectionString);
        using var context = new HydraForgeDbContext(options);
        var repo = new EfUserRepository(context);

        var dave = User.Create("dave", "Dave", "Brown", "dave@localhost", "hash", false);
        var eve = User.Create("eve", "Eve", "Green", "eve@localhost", "hash", false);
        var frank = User.Create("frank", "Frank", "Black", "frank@localhost", "hash", false);
        await repo.CreateAsync(dave);
        await repo.CreateAsync(eve);
        await repo.CreateAsync(frank);

        var count = await repo.CountAsync("e", CancellationToken.None);
        var list = await repo.ListAsync(0, 100, "e", CancellationToken.None);

        Assert.Equal(2, count);
        Assert.Equal(2, list.Count);
    }

    [Fact]
    public async Task UpdateAsync_PersistsInstanceMethodChanges()
    {
        string? connectionString = Environment.GetEnvironmentVariable(
            "HYDRAFORGE_TEST_CONNECTION_STRING"
        );
        if (string.IsNullOrWhiteSpace(connectionString))
            return;

        var options = CreateOptions(connectionString);
        using var context = new HydraForgeDbContext(options);
        var repo = new EfUserRepository(context);

        var user = User.Create(
            "updateme",
            "Update",
            "User",
            "updateme@localhost",
            "oldhash",
            isAdmin: false
        );

        await repo.CreateAsync(user);

        user.Disable();
        user.SetAdminRole(true);
        user.SetPasswordHash("newhash");
        await repo.UpdateAsync(user, CancellationToken.None);

        var saved = await context.Users.FindAsync(user.Id);
        Assert.NotNull(saved);
        Assert.True(saved.IsDisabled);
        Assert.True(saved.IsAdmin);
        Assert.Equal("newhash", saved.PasswordHash);
    }
}
