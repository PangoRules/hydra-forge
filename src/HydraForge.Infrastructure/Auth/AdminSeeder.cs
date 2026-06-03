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
}

public class AdminSeeder
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ILogger<AdminSeeder> _logger;
    private readonly AdminSeederOptions _options;

    public AdminSeeder(IUserRepository userRepository, IPasswordHasher passwordHasher, ILogger<AdminSeeder> logger, IOptions<AdminSeederOptions> options)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _logger = logger;
        _options = options.Value;
    }

    public async Task<Result> SeedIfNeededAsync()
    {
        var anyAdminExists = await _userRepository.AnyAdminExistsAsync();
        if (anyAdminExists)
        {
            _logger.LogInformation("Admin already exists, skipping seed");
            return Result.Success();
        }

        var username = _options.Username;
        var password = _options.Password;

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            _logger.LogWarning("Admin seed not configured: AdminSeed:Username and AdminSeed:Password env vars or config required");
            return Result.Failure(new Error(DomainErrorCodes.Auth.AdminSeedNotConfigured, "Admin seed not configured. Set AdminSeed:Username and AdminSeed:Password environment variables or configuration."));
        }

        var normalizedUsername = username.ToLowerInvariant();
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
            EmailNormalized = "admin@localhost",
            PasswordHash = _passwordHasher.HashPassword(password),
            IsAdmin = true,
            IsDisabled = false
        };

        await _userRepository.CreateAsync(user);
        _logger.LogInformation("Admin user created successfully");
        return Result.Success();
    }
}