using HydraForge.Application.Auth;
using HydraForge.Domain.Common;
using HydraForge.Domain.Entities.Auth;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HydraForge.Infrastructure.Auth;

public class AdminSeederOptions
{
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? Name { get; set; }
    public string? LastName { get; set; }
    public string? Email { get; set; }
}

public class AdminSeeder(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    ILogger<AdminSeeder> logger,
    IOptions<AdminSeederOptions> options
)
{
    private readonly AdminSeederOptions _options = options.Value;

    public async Task<Result> SeedIfNeededAsync()
    {
        var anyAdminExists = await userRepository.AnyAdminExistsAsync();
        if (anyAdminExists)
        {
            logger.LogInformation("Admin already exists, skipping seed");
            return Result.Success();
        }

        var username = _options.Username;
        var password = _options.Password;

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning(
                "Admin seed not configured: AdminSeed:Username and AdminSeed:Password env vars or config required"
            );
            return Result.Failure(
                new Error(
                    DomainErrorCodes.Auth.AdminSeedNotConfigured,
                    "Admin seed not configured. Set AdminSeed:Username and AdminSeed:Password environment variables or configuration."
                )
            );
        }

        var normalizedUsername = username.ToLowerInvariant();
        var existingUser = await userRepository.FindByUsernameAsync(normalizedUsername);
        if (existingUser != null)
        {
            logger.LogInformation("Admin user already exists, skipping seed");
            return Result.Success();
        }

        var user = new User
        {
            Name = _options.Name ?? "Admin",
            LastName = _options.LastName ?? "Admin",
            Username = username,
            UsernameNormalized = normalizedUsername,
            Email = _options.Email ?? "admin@localhost",
            EmailNormalized = _options.Email != null ? _options.Email.ToLower() : "admin@localhost",
            PasswordHash = passwordHasher.HashPassword(password),
            IsAdmin = true,
            IsDisabled = false,
        };

        await userRepository.CreateAsync(user);
        logger.LogInformation("Admin user created successfully");
        return Result.Success();
    }
}
