using System.Security.Cryptography;
using System.Text;
using HydraForge.Application.Auth;
using Konscious.Security.Cryptography;
using Microsoft.Extensions.Options;

namespace HydraForge.Infrastructure.Auth;

public class Argon2Options
{
    public int MemorySizeKiB { get; set; } = 65536;
    public int Iterations { get; set; } = 3;
    public int Parallelism { get; set; } = 4;
}

public class Argon2PasswordHasher : IPasswordHasher
{
    private readonly int _memorySizeKiB;
    private readonly int _iterations;
    private readonly int _parallelism;

    public Argon2PasswordHasher(IOptions<Argon2Options> options)
    {
        _memorySizeKiB = options.Value.MemorySizeKiB;
        _iterations = options.Value.Iterations;
        _parallelism = options.Value.Parallelism;
    }

    public string HashPassword(string password)
    {
        var salt = new byte[16];
        RandomNumberGenerator.Fill(salt);

        var hash = new Argon2id(Encoding.UTF8.GetBytes(password))
        {
            Salt = salt,
            MemorySize = _memorySizeKiB,
            Iterations = _iterations,
            DegreeOfParallelism = _parallelism
        }.GetBytes(32);

        return Convert.ToBase64String(salt) + "$" + Convert.ToBase64String(hash);
    }

    public bool VerifyPassword(string password, string encodedHash)
    {
        try
        {
            var parts = encodedHash.Split('$');
            if (parts.Length != 2) return false;

            var salt = Convert.FromBase64String(parts[0]);
            var storedHash = Convert.FromBase64String(parts[1]);

            var hash = new Argon2id(Encoding.UTF8.GetBytes(password))
            {
                Salt = salt,
                MemorySize = _memorySizeKiB,
                Iterations = _iterations,
                DegreeOfParallelism = _parallelism
            }.GetBytes(32);

            return CryptographicOperations.FixedTimeEquals(hash, storedHash);
        }
        catch
        {
            return false;
        }
    }
}