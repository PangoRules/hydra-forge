using HydraForge.Application.Auth;
using HydraForge.Domain.Common;
using HydraForge.Domain.Entities.Auth;
using Microsoft.Extensions.Logging;

namespace HydraForge.Infrastructure.Auth;

public class AdminSeeder
{
    private readonly EfUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ILogger<AdminSeeder> _logger;

    public AdminSeeder(EfUserRepository userRepository, IPasswordHasher passwordHasher, ILogger<AdminSeeder> logger)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _logger = logger;
    }

    public async Task<Result> SeedIfNeededAsync()
    {
        var anyAdminExists = await _userRepository.AnyAdminExistsAsync();
        if (anyAdminExists)
        {
            _logger.LogInformation("Admin already exists, skipping seed");
            return Result.Success();
        }

        var username = Environment.GetEnvironmentVariable("HYDRA_ADMIN_USERNAME");
        var password = Environment.GetEnvironmentVariable("HYDRA_ADMIN_PASSWORD");

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            _logger.LogWarning("Admin seed not configured: HYDRA_ADMIN_USERNAME and HYDRA_ADMIN_PASSWORD env vars required");
            return Result.Failure(new Error(DomainErrorCodes.Auth.AdminSeedNotConfigured, "Admin seed not configured. Set HYDRA_ADMIN_USERNAME and HYDRA_ADMIN_PASSWORD environment variables."));
        }

        var normalizedUsername = username.ToUpperInvariant();
        var existingUser = await _userRepository.FindByUsernameAsync(normalizedUsername);
        if (existingUser != null)
        {
            _logger.LogInformation("Admin user already exists, skipping seed");
            return Result.Success();
        }

        var user = new User
        {
            Name = "Admin",
            LastName = "",
            Username = username,
            UsernameNormalized = normalizedUsername,
            Email = "admin@localhost",
            EmailNormalized = "ADMIN@LOCALHOST",
            PasswordHash = _passwordHasher.HashPassword(password),
            IsAdmin = true,
            IsDisabled = false
        };

        await _userRepository.CreateAsync(user);
        _logger.LogInformation("Admin user created successfully");
        return Result.Success();
    }
}