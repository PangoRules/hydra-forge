namespace HydraForge.Infrastructure.Llm;

using System.Collections.Concurrent;
using HydraForge.Application.Llm;
using HydraForge.Domain.Entities.Admin;
using HydraForge.Domain.Enums;
using HydraForge.Infrastructure.Llm.Adapters;
using Microsoft.Extensions.Logging;

public sealed class LlmClientFactory(
    IHttpClientFactory httpClientFactory,
    IKeyVault keyVault,
    ILoggerFactory loggerFactory
) : ILlmClientFactory
{
    private readonly ConcurrentDictionary<Guid, ILlmClient> _clients = new();
    private readonly ConcurrentDictionary<Guid, IImageClient> _imageClients = new();
    private readonly ILogger<LlmClientFactory> _logger =
        loggerFactory.CreateLogger<LlmClientFactory>();

    public ILlmClient For(LlmProvider provider)
    {
        return _clients.GetOrAdd(
            provider.Id,
            id =>
            {
                var http = httpClientFactory.CreateClient(NamedHttpClientFor(provider.AdapterType));
                return CreateClient(http, provider);
            }
        );
    }

    public IImageClient ImageFor(LlmProvider provider)
    {
        return _imageClients.GetOrAdd(
            provider.Id,
            id =>
            {
                var http = httpClientFactory.CreateClient(
                    NamedHttpClientForImage(provider.AdapterType)
                );
                return CreateImageClient(http, provider);
            }
        );
    }

    public IEmbeddingClient EmbeddingFor(LlmProvider provider)
    {
        if (provider.AdapterType != AdapterType.OpenAiCompatible)
        {
            _logger.LogWarning(
                "EmbeddingFor called with non-OpenAiCompatible provider {AdapterType}. Embedding is only supported for OpenAI-compatible providers.",
                provider.AdapterType
            );
            throw new NotSupportedException(
                $"Embedding is only supported for OpenAiCompatible adapters. Got: {provider.AdapterType}"
            );
        }

        var client = (OpenAiCompatibleAdapter)For(provider);
        return client;
    }

    public void Invalidate(Guid providerId)
    {
        _clients.TryRemove(providerId, out _);
        _imageClients.TryRemove(providerId, out _);
    }

    private ILlmClient CreateClient(HttpClient http, LlmProvider provider)
    {
        return provider.AdapterType switch
        {
            AdapterType.OpenAiCompatible => new OpenAiCompatibleAdapter(http, keyVault, provider),
            AdapterType.Anthropic => new AnthropicAdapter(
                http,
                keyVault,
                provider,
                loggerFactory.CreateLogger<AnthropicAdapter>()
            ),
            AdapterType.Ollama => new OllamaAdapter(
                http,
                provider,
                loggerFactory.CreateLogger<OllamaAdapter>()
            ),
            _ => throw new NotSupportedException(
                $"No LLM client adapter for provider type: {provider.AdapterType}"
            ),
        };
    }

    private IImageClient CreateImageClient(HttpClient http, LlmProvider provider)
    {
        return provider.AdapterType switch
        {
            AdapterType.DallE => new DallEAdapter(
                http,
                keyVault,
                provider,
                loggerFactory.CreateLogger<DallEAdapter>()
            ),
            AdapterType.StabilityAi => new StabilityAiAdapter(
                http,
                keyVault,
                provider,
                loggerFactory.CreateLogger<StabilityAiAdapter>()
            ),
            AdapterType.ComfyUi or AdapterType.Diffusers => new ComfyUiAdapter(
                http,
                keyVault,
                provider,
                loggerFactory.CreateLogger<ComfyUiAdapter>()
            ),
            _ => throw new NotSupportedException(
                $"No image client adapter for provider type: {provider.AdapterType}"
            ),
        };
    }

    private static string NamedHttpClientFor(AdapterType type) =>
        type switch
        {
            AdapterType.OpenAiCompatible => "openai-compatible",
            AdapterType.Anthropic => "anthropic",
            AdapterType.Ollama => "ollama",
            _ => throw new NotSupportedException($"No HTTP client name for adapter type: {type}"),
        };

    private static string NamedHttpClientForImage(AdapterType type) =>
        type switch
        {
            AdapterType.DallE => "dalle",
            AdapterType.StabilityAi => "stability-ai",
            AdapterType.ComfyUi or AdapterType.Diffusers => "comfyui",
            _ => throw new NotSupportedException(
                $"No HTTP client name for image adapter type: {type}"
            ),
        };
}
