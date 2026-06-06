using HydraForge.Application.Auth;
using HydraForge.Domain.Entities.Auth;
using Microsoft.Extensions.Logging;

namespace HydraForge.Infrastructure.Auth;

public class TestUserSeeder(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    ILogger<TestUserSeeder> logger
)
{
    private static readonly IReadOnlyList<(string username, string password, bool isAdmin)> TestUsers =
    [
        ("testadmin", "TestAdmin123!", true),
        ("testuser",  "TestUser123!",  false),
    ];

    public async Task SeedIfNeededAsync()
    {
        foreach (var (username, password, isAdmin) in TestUsers)
        {
            var normalized = username.ToLowerInvariant();
            var existing = await userRepository.FindByUsernameAsync(normalized);
            if (existing != null)
            {
                logger.LogInformation("Test user '{Username}' already exists, skipping", username);
                continue;
            }

            var user = new User
            {
                Name = $"Test{username}",
                LastName = "User",
                Username = username,
                UsernameNormalized = normalized,
                Email = $"{username}@localhost",
                EmailNormalized = $"{username}@localhost",
                PasswordHash = passwordHasher.HashPassword(password),
                IsAdmin = isAdmin,
                IsDisabled = false,
            };

            await userRepository.CreateAsync(user);
            logger.LogInformation("Test user '{Username}' created (admin={IsAdmin})", username, isAdmin);
        }
    }
}
