namespace HydraForge.Infrastructure.Llm;

using HydraForge.Application.Llm;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public static class LlmServiceCollectionExtensions
{
    public static IServiceCollection AddLlmInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.Configure<LlmOptions>(
            LlmOptions.SectionName,
            configuration.GetSection(LlmOptions.SectionName)
        );

        ValidateEncryptionKey(configuration);
        services.AddSingleton<IKeyVault, AesGcmKeyVault>();

        return services;
    }

    private static byte[] ValidateEncryptionKey(IConfiguration configuration)
    {
        var encoded = configuration[$"{LlmOptions.SectionName}:{nameof(LlmOptions.EncryptionKey)}"];
        if (string.IsNullOrWhiteSpace(encoded))
            throw new InvalidOperationException(
                "Llm:EncryptionKey must be a base64-encoded 32-byte AES-256 key."
            );

        byte[] keyBytes;
        try
        {
            keyBytes = Convert.FromBase64String(encoded);
        }
        catch (FormatException)
        {
            throw new InvalidOperationException(
                "Llm:EncryptionKey must be a base64-encoded 32-byte AES-256 key."
            );
        }

        if (keyBytes.Length != 32)
            throw new InvalidOperationException(
                "Llm:EncryptionKey must be a base64-encoded 32-byte AES-256 key."
            );

        return keyBytes;
    }
}
