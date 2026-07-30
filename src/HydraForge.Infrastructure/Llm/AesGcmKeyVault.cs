namespace HydraForge.Infrastructure.Llm;

using System.Security.Cryptography;
using HydraForge.Application.Llm;
using Microsoft.Extensions.Options;

public class LlmOptions
{
    public const string SectionName = "Llm";
    public string? EncryptionKey { get; set; }
}

public class AesGcmKeyVault : IKeyVault
{
    private const int KeySizeBytes = 32;
    private const int NonceSizeBytes = 12;
    private const int TagSizeBytes = 16;

    private readonly byte[] _key;

    public AesGcmKeyVault(IOptions<LlmOptions> options)
    {
        var encoded =
            options.Value.EncryptionKey
            ?? throw new InvalidOperationException(
                $"Configuration '{LlmOptions.SectionName}:EncryptionKey' is required."
            );

        var keyBytes = Convert.FromBase64String(encoded);
        if (keyBytes.Length != KeySizeBytes)
            throw new InvalidOperationException(
                $"'{LlmOptions.SectionName}:EncryptionKey' must be a base64-encoded {KeySizeBytes}-byte AES-256 key."
            );

        _key = keyBytes;
    }

    public string Encrypt(string plaintext)
    {
        var plaintextBytes = System.Text.Encoding.UTF8.GetBytes(plaintext);
        var nonce = RandomNumberGenerator.GetBytes(NonceSizeBytes);
        var ciphertext = new byte[plaintextBytes.Length];
        var tag = new byte[TagSizeBytes];

        using var aes = new AesGcm(_key, TagSizeBytes);
        aes.Encrypt(nonce, plaintextBytes, ciphertext, tag);

        var nonceB64 = Convert.ToBase64String(nonce);
        var ciphertextB64 = Convert.ToBase64String(ciphertext);
        var tagB64 = Convert.ToBase64String(tag);

        return $"v1:{nonceB64}:{ciphertextB64}:{tagB64}";
    }

    public string Decrypt(string ciphertext)
    {
        const string prefix = "v1:";
        if (!ciphertext.StartsWith(prefix))
            throw new InvalidOperationException("Unsupported ciphertext version.");

        var parts = ciphertext[prefix.Length..].Split(':');
        if (parts.Length != 3)
            throw new InvalidOperationException("Invalid ciphertext format.");

        var nonce = Convert.FromBase64String(parts[0]);
        var ciphertextBytes = Convert.FromBase64String(parts[1]);
        var tag = Convert.FromBase64String(parts[2]);

        var plaintext = new byte[ciphertextBytes.Length];

        using var aes = new AesGcm(_key, TagSizeBytes);
        aes.Decrypt(nonce, ciphertextBytes, tag, plaintext);

        return System.Text.Encoding.UTF8.GetString(plaintext);
    }

    public static string EncryptStatic(string plaintext, byte[] keyBytes)
    {
        var plaintextBytes = System.Text.Encoding.UTF8.GetBytes(plaintext);
        var nonce = RandomNumberGenerator.GetBytes(NonceSizeBytes);
        var ciphertext = new byte[plaintextBytes.Length];
        var tag = new byte[TagSizeBytes];

        using var aes = new AesGcm(keyBytes, TagSizeBytes);
        aes.Encrypt(nonce, plaintextBytes, ciphertext, tag);

        var nonceB64 = Convert.ToBase64String(nonce);
        var ciphertextB64 = Convert.ToBase64String(ciphertext);
        var tagB64 = Convert.ToBase64String(tag);

        return $"v1:{nonceB64}:{ciphertextB64}:{tagB64}";
    }
}
