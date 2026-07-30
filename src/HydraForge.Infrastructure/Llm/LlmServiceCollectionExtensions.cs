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
        services.Configure<LlmOptions>(LlmOptions.SectionName, configuration.GetSection(LlmOptions.SectionName));

        var keyValid = ValidateEncryptionKey(configuration);
        if (keyValid)
            services.AddSingleton<IKeyVault, AesGcmKeyVault>();

        return services;
    }

    private static bool ValidateEncryptionKey(IConfiguration configuration)
    {
        var encoded = configuration[$"{LlmOptions.SectionName}:{nameof(LlmOptions.EncryptionKey)}"];
        if (string.IsNullOrWhiteSpace(encoded))
            return false;

        var keyBytes = Convert.FromBase64String(encoded);
        if (keyBytes.Length != 32)
            throw new InvalidOperationException(
                $"'{LlmOptions.SectionName}:{nameof(LlmOptions.EncryptionKey)}' must be a base64-encoded 32-byte AES-256 key.");

        return true;
    }
}
