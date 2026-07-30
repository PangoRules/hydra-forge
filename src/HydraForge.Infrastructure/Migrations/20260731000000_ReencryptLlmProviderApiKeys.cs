using HydraForge.Infrastructure.Llm;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using System.Text;

#nullable disable

namespace HydraForge.Infrastructure.Migrations;

public partial class ReencryptLlmProviderApiKeys : Migration
{
    private const string VersionPrefix = "v1:";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        var connectionString = GetConnectionStringOrThrow();
        var encryptionKey = GetEncryptionKeyOrThrow();
        var keyBytes = Convert.FromBase64String(encryptionKey);

        using var connection = new NpgsqlConnection(connectionString);
        connection.Open();

        using var selectCmd = new NpgsqlCommand(
            $@"SELECT ""id"", ""api_key_encrypted"" FROM ""llm_providers""
                WHERE ""api_key_encrypted"" = '' OR ""api_key_encrypted"" IS NULL
                   OR (""api_key_encrypted"" <> '' AND NOT ""api_key_encrypted"" LIKE '{VersionPrefix}%')",
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
                $"'Llm:EncryptionKey' environment variable must be set to a base64-encoded 32-byte AES-256 key.");

        var keyBytes = Convert.FromBase64String(key);
        if (keyBytes.Length != 32)
            throw new InvalidOperationException(
                $"'Llm:EncryptionKey' must be a base64-encoded 32-byte AES-256 key.");

        return key;
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
}
