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

        services.AddHttpClient(
            "openai-compatible",
            client =>
            {
                client.Timeout = TimeSpan.FromSeconds(60);
            }
        );

        services.AddHttpClient(
            "anthropic",
            client =>
            {
                client.Timeout = TimeSpan.FromSeconds(60);
            }
        );

        services.AddHttpClient(
            "ollama",
            client =>
            {
                client.Timeout = TimeSpan.FromSeconds(60);
            }
        );

        services.AddHttpClient(
            "dalle",
            client =>
            {
                client.Timeout = TimeSpan.FromSeconds(120);
            }
        );

        services.AddHttpClient(
            "stability-ai",
            client =>
            {
                client.Timeout = TimeSpan.FromSeconds(120);
            }
        );

        services.AddHttpClient(
            "comfyui",
            client =>
            {
                client.Timeout = TimeSpan.FromSeconds(300);
            }
        );

        services.AddSingleton<ILlmClientFactory, LlmClientFactory>();

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
