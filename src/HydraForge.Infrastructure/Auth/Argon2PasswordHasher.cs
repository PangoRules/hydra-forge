using System.Security.Cryptography;
using System.Text;
using HydraForge.Application.Auth.Ports;
using Konscious.Security.Cryptography;

namespace HydraForge.Infrastructure.Auth;

public class Argon2PasswordHasher : IPasswordHasher
{
    private const int MemorySize = 65536;
    private const int Iterations = 3;
    private const int Parallelism = 4;

    public string HashPassword(string password)
    {
        var salt = new byte[16];
        RandomNumberGenerator.Fill(salt);

        var hash = new Argon2id(Encoding.UTF8.GetBytes(password))
        {
            Salt = salt,
            MemorySize = MemorySize,
            Iterations = Iterations,
            DegreeOfParallelism = Parallelism
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
                MemorySize = MemorySize,
                Iterations = Iterations,
                DegreeOfParallelism = Parallelism
            }.GetBytes(32);

            return CryptographicOperations.FixedTimeEquals(hash, storedHash);
        }
        catch
        {
            return false;
        }
    }
}