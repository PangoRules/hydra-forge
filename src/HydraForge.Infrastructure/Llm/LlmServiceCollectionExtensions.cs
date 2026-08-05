namespace HydraForge.Infrastructure.Llm;

using HydraForge.Application.Admin;
using HydraForge.Application.Llm;
using HydraForge.Application.Logging;
using HydraForge.Infrastructure.Admin;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

public static class LlmServiceCollectionExtensions
{
    public static IServiceCollection AddLlmInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddOptions<LlmOptions>().Bind(configuration.GetSection(LlmOptions.SectionName));
        services.AddOptions<RagOptions>().Bind(configuration.GetSection("Llm:Rag"));

        ValidateEncryptionKey(configuration);
        services.AddSingleton<IKeyVault, AesGcmKeyVault>();

        services.AddHttpClient(
            "openai-compatible",
            client =>
            {
                // Local models (Ollama/LM Studio via OpenAI-compat endpoint) routinely exceed
                // 60s for cold-model-load + generation. 600s matches the dedicated ollama client.
                client.Timeout = TimeSpan.FromSeconds(600);
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
                // Local models: cold model load into RAM/VRAM plus CPU-bound token
                // generation routinely exceeds the 60s used for cloud adapters below —
                // that mismatch silently dropped narrative-job output (HttpClient.Timeout
                // cancels the whole request including the streamed read, not just the
                // header wait, so a slow local reply looked like "cancelled unexpectedly"
                // instead of an explicit timeout error). Bounded, not infinite — no
                // internal retry loop rides on top of this, so a stuck request still
                // fails after 10 minutes rather than hanging forever.
                client.Timeout = TimeSpan.FromSeconds(600);
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
                // Local diffusion (ComfyUI + Diffusers, D-62) on CPU or a modest GPU can
                // take several minutes per image. Same bounded-not-infinite reasoning as
                // the ollama client above.
                client.Timeout = TimeSpan.FromSeconds(600);
            }
        );

        services.AddSingleton<ILlmClientFactory, LlmClientFactory>();

        services.AddSingleton<IWarnLogger, NullWarnLogger>();
        services.AddScoped<IRoutingConfigProvider, DbContextRoutingConfigProvider>();
        services.AddScoped<IModelRouter, ModelRouter>();
        services.AddScoped<IContextCompressor, ContextCompressor>();
        services.AddScoped<IUsageRecorder, EfUsageRecorder>();
        services.AddScoped<IUserTokenBudgetRepository, EfUserTokenBudgetRepository>();
        services.AddScoped<ILlmAdminRepository, EfLlmAdminRepository>();
        services.AddScoped<ILlmAdminService, LlmAdminService>();
        services.AddScoped<LlmCallGuard>();

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
