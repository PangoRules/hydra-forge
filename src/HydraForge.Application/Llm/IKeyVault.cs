namespace HydraForge.Application.Llm;

public interface IKeyVault
{
    string Encrypt(string plaintext);
    string Decrypt(string ciphertext);
}
