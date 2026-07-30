namespace HydraForge.Domain.Entities.Auth;

public class User
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Name { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public string Username { get; private set; } = string.Empty;
    public string UsernameNormalized { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string EmailNormalized { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public bool IsAdmin { get; private set; }
    public bool IsDisabled { get; private set; }
    public DateTime? LastLoginAt { get; private set; }
    public int FailedLoginAttempts { get; private set; }
    public DateTime? LockedOutUntil { get; private set; }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; private set; } = DateTime.UtcNow;

    private User() { }

    public static User Create(
        string username,
        string name,
        string lastName,
        string email,
        string passwordHash,
        bool isAdmin = false,
        Guid? id = null
    )
    {
        var now = DateTime.UtcNow;
        return new User
        {
            Id = id ?? Guid.NewGuid(),
            Name = name,
            LastName = lastName,
            Username = username,
            UsernameNormalized = username.ToLowerInvariant(),
            Email = email,
            EmailNormalized = email.ToLowerInvariant(),
            PasswordHash = passwordHash,
            IsAdmin = isAdmin,
            IsDisabled = false,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    public void Disable()
    {
        IsDisabled = true;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Enable()
    {
        IsDisabled = false;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetAdminRole(bool isAdmin)
    {
        IsAdmin = isAdmin;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetPasswordHash(string hash)
    {
        PasswordHash = hash;
        UpdatedAt = DateTime.UtcNow;
    }

    public void RecordLogin(DateTime loginAt)
    {
        LastLoginAt = loginAt;
        UpdatedAt = DateTime.UtcNow;
    }

    public void RecordFailedLogin()
    {
        FailedLoginAttempts++;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Lockout(TimeSpan duration)
    {
        LockedOutUntil = DateTime.UtcNow.Add(duration);
        UpdatedAt = DateTime.UtcNow;
    }

    public void ResetFailedAttempts()
    {
        FailedLoginAttempts = 0;
        LockedOutUntil = null;
        UpdatedAt = DateTime.UtcNow;
    }
}
