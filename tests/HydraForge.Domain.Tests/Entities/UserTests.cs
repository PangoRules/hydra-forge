using HydraForge.Domain.Entities.Auth;

namespace HydraForge.Domain.Tests.Entities;

public class UserTests
{
    [Fact]
    public void Create_SetsNormalizedFieldsAndDefaults()
    {
        var user = User.Create(
            "Alice",
            "Alice",
            "Smith",
            "Alice@Example.com",
            "hash",
            isAdmin: true
        );

        Assert.NotEqual(Guid.Empty, user.Id);
        Assert.Equal("alice", user.UsernameNormalized);
        Assert.Equal("alice@example.com", user.EmailNormalized);
        Assert.True(user.IsAdmin);
        Assert.False(user.IsDisabled);
        Assert.Null(user.LastLoginAt);
    }

    [Fact]
    public void Disable_SetsIsDisabledTrue()
    {
        var user = User.Create("bob", "Bob", "Jones", "bob@example.com", "hash");
        user.Disable();
        Assert.True(user.IsDisabled);
    }

    [Fact]
    public void Enable_SetsIsDisabledFalse()
    {
        var user = User.Create("bob", "Bob", "Jones", "bob@example.com", "hash");
        user.Disable();
        user.Enable();
        Assert.False(user.IsDisabled);
    }

    [Fact]
    public void SetAdminRole_TogglesIsAdmin()
    {
        var user = User.Create("bob", "Bob", "Jones", "bob@example.com", "hash");
        user.SetAdminRole(true);
        Assert.True(user.IsAdmin);
        user.SetAdminRole(false);
        Assert.False(user.IsAdmin);
    }

    [Fact]
    public void SetPasswordHash_UpdatesHash()
    {
        var user = User.Create("bob", "Bob", "Jones", "bob@example.com", "hash");
        user.SetPasswordHash("new-hash");
        Assert.Equal("new-hash", user.PasswordHash);
    }

    [Fact]
    public void RecordLogin_SetsLastLoginAt()
    {
        var user = User.Create("bob", "Bob", "Jones", "bob@example.com", "hash");
        var loginAt = DateTime.UtcNow;
        user.RecordLogin(loginAt);
        Assert.Equal(loginAt, user.LastLoginAt);
    }
}
