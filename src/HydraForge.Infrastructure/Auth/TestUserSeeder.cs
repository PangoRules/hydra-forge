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
    private static readonly IReadOnlyList<(
        string username,
        string password,
        bool isAdmin
    )> TestUsers =
    [
        ("testadmin", "TestAdmin123!", true),
        ("testuser1", "TestUser123!", false),
        ("testuser2", "TestUser123!", false),
        ("nonmember", "NonMember123!", false),
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

            var user = User.Create(
                username,
                $"Test{username}",
                "User",
                $"{username}@localhost",
                passwordHasher.HashPassword(password),
                isAdmin);

            await userRepository.CreateAsync(user);
            logger.LogInformation(
                "Test user '{Username}' created (admin={IsAdmin})",
                username,
                isAdmin
            );
        }
    }
}
