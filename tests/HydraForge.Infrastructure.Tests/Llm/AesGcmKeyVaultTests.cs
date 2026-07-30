namespace HydraForge.Infrastructure.Tests.Llm;

using HydraForge.Infrastructure.Llm;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;

public class AesGcmKeyVaultTests
{
    private const int KeySizeBytes = 32;

    private static byte[] GenerateKey()
    {
        return RandomNumberGenerator.GetBytes(KeySizeBytes);
    }

    private static IOptions<LlmOptions> CreateOptions(byte[] keyBytes)
    {
        var options = new LlmOptions { EncryptionKey = Convert.ToBase64String(keyBytes) };
        return Options.Create(options);
    }

    [Fact]
    public void Encrypt_ProducesVersionedFormat()
    {
        var keyBytes = GenerateKey();
        var vault = new AesGcmKeyVault(CreateOptions(keyBytes));

        var ciphertext = vault.Encrypt("hello world");

        Assert.StartsWith("v1:", ciphertext);
        var parts = ciphertext["v1:".Length..].Split(':');
        Assert.Equal(3, parts.Length);
    }

    [Fact]
    public void Decrypt_RoundTrips()
    {
        var keyBytes = GenerateKey();
        var vault = new AesGcmKeyVault(CreateOptions(keyBytes));

        var plaintext = "my-secret-api-key-12345";
        var ciphertext = vault.Encrypt(plaintext);
        var decrypted = vault.Decrypt(ciphertext);

        Assert.Equal(plaintext, decrypted);
    }

    [Fact]
    public void Decrypt_TamperedCiphertext_Throws()
    {
        var keyBytes = GenerateKey();
        var vault = new AesGcmKeyVault(CreateOptions(keyBytes));

        var ciphertext = vault.Encrypt("secret");
        var parts = ciphertext.Split(':');
        var nonceB64 = parts[1];
        var cipherB64 = parts[2];
        var tagB64 = parts[3];

        var tamperedCipherB64 = Convert.ToBase64String(
            Convert.FromBase64String(cipherB64).Select(b => (byte)(b ^ 0xFF)).ToArray());

        var tampered = $"v1:{nonceB64}:{tamperedCipherB64}:{tagB64}";

        Assert.ThrowsAny<Exception>(() => vault.Decrypt(tampered));
    }

    [Fact]
    public void Decrypt_WrongKey_Throws()
    {
        var keyBytes1 = GenerateKey();
        var keyBytes2 = GenerateKey();
        var vault1 = new AesGcmKeyVault(CreateOptions(keyBytes1));
        var vault2 = new AesGcmKeyVault(CreateOptions(keyBytes2));

        var ciphertext = vault1.Encrypt("secret");
        Assert.ThrowsAny<Exception>(() => vault2.Decrypt(ciphertext));
    }

    [Fact]
    public void EncryptStatic_ProducesSameFormatAsInstance()
    {
        var keyBytes = GenerateKey();
        var vault = new AesGcmKeyVault(CreateOptions(keyBytes));
        var plaintext = "static-test-value";

        var instanceResult = vault.Encrypt(plaintext);
        var staticResult = AesGcmKeyVault.EncryptStatic(plaintext, keyBytes);

        Assert.StartsWith("v1:", instanceResult);
        Assert.StartsWith("v1:", staticResult);
        var instanceParts = instanceResult["v1:".Length..].Split(':');
        var staticParts = staticResult["v1:".Length..].Split(':');
        Assert.Equal(3, instanceParts.Length);
        Assert.Equal(3, staticParts.Length);
    }

    [Fact]
    public void Constructor_InvalidBase64_Throws()
    {
        var badOptions = Options.Create(new LlmOptions { EncryptionKey = "not-base64!!!" });
        Assert.ThrowsAny<Exception>(() => new AesGcmKeyVault(badOptions));
    }

    [Fact]
    public void Constructor_WrongKeyLength_Throws()
    {
        var shortKey = new byte[16];
        var options = Options.Create(new LlmOptions { EncryptionKey = Convert.ToBase64String(shortKey) });
        var ex = Assert.Throws<InvalidOperationException>(() => new AesGcmKeyVault(options));
        Assert.Contains("32-byte", ex.Message);
    }

    [Fact]
    public void Decrypt_UnsupportedVersion_Throws()
    {
        var keyBytes = GenerateKey();
        var vault = new AesGcmKeyVault(CreateOptions(keyBytes));

        Assert.Throws<InvalidOperationException>(() => vault.Decrypt("v0:abc:def:ghi"));
    }
}
