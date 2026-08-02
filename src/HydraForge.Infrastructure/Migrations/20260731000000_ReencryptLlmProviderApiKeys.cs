using HydraForge.Infrastructure.Llm;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

#nullable disable

namespace HydraForge.Infrastructure.Migrations;

public partial class ReencryptLlmProviderApiKeys : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        var connectionString = GetConnectionStringOrThrow();
        var encryptionKey = GetEncryptionKeyOrThrow();
        var keyBytes = Convert.FromBase64String(encryptionKey);

        using var connection = new NpgsqlConnection(connectionString);
        connection.Open();

        using var selectCmd = new NpgsqlCommand(
            @"SELECT ""id"", ""api_key_encrypted"" FROM ""llm_providers""
              WHERE ""api_key_encrypted"" IN ('not-configured', 'placeholder', 'dummy', 'test', 'sk_test')
                 OR (""api_key_encrypted"" <> '' AND NOT ""api_key_encrypted"" LIKE 'v1:%')",
            connection);

        using var reader = selectCmd.ExecuteReader();
        var rowsToUpdate = new List<(Guid Id, string ApiKeyEncrypted)>();
        while (reader.Read())
        {
            rowsToUpdate.Add((reader.GetGuid(0), reader.GetString(1)));
        }
        reader.Close();

        foreach (var (id, apiKeyEncrypted) in rowsToUpdate)
        {
            if (string.IsNullOrEmpty(apiKeyEncrypted))
                continue;

            var encrypted = AesGcmKeyVault.EncryptStatic(apiKeyEncrypted, keyBytes);

            using var updateCmd = new NpgsqlCommand(
                @"UPDATE ""llm_providers"" SET ""api_key_encrypted"" = @enc WHERE ""id"" = @id",
                connection);
            updateCmd.Parameters.AddWithValue("enc", encrypted);
            updateCmd.Parameters.AddWithValue("id", id);
            updateCmd.ExecuteNonQuery();
        }
    }

    private static string GetConnectionStringOrThrow()
    {
        var cs = Environment.GetEnvironmentVariable("ConnectionStrings__Default");
        if (string.IsNullOrWhiteSpace(cs))
            throw new InvalidOperationException("Connection string 'Default' not found. Set ConnectionStrings__Default environment variable.");
        return cs;
    }

    private static string GetEncryptionKeyOrThrow()
    {
        var key = Environment.GetEnvironmentVariable("Llm__EncryptionKey");
        if (string.IsNullOrWhiteSpace(key))
            throw new InvalidOperationException(
                "'Llm:EncryptionKey' environment variable must be set to a base64-encoded 32-byte AES-256 key.");

        byte[] keyBytes;
        try
        {
            keyBytes = Convert.FromBase64String(key);
        }
        catch (FormatException)
        {
            throw new InvalidOperationException("Llm:EncryptionKey must be a base64-encoded 32-byte AES-256 key.");
        }

        if (keyBytes.Length != 32)
            throw new InvalidOperationException("Llm:EncryptionKey must be a base64-encoded 32-byte AES-256 key.");

        return key;
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
}
