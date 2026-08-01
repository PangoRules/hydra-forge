namespace HydraForge.Infrastructure.Tests.Llm;

using HydraForge.Application.Llm;
using HydraForge.Domain.Entities.Admin;
using HydraForge.Domain.Enums;
using HydraForge.Infrastructure.Llm;
using HydraForge.Infrastructure.Llm.Adapters;
using Microsoft.Extensions.Logging;

public class LlmClientFactoryTests
{
    private static LlmProvider CreateProvider(
        Guid? id = null,
        AdapterType adapterType = AdapterType.OpenAiCompatible,
        string baseUrl = "https://api.example.com"
    )
    {
        return new LlmProvider
        {
            Id = id ?? Guid.NewGuid(),
            Name = "Test Provider",
            BaseUrl = baseUrl,
            ApiKeyEncrypted = "",
            AdapterType = adapterType,
            ProviderType = ProviderType.Text,
            Tier = ModelTier.Standard,
            IsEnabled = true,
        };
    }

    private class FakeKeyVault : IKeyVault
    {
        public string Decrypt(string _) => "decrypted-key";

        public string Encrypt(string plaintext) => plaintext;
    }

    private class NullLogger : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => false;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter
        ) { }
    }

    private class FakeLoggerFactory : ILoggerFactory
    {
        public void AddProvider(ILoggerProvider provider) { }

        public ILogger CreateLogger(string categoryName) => new NullLogger();

        public void Dispose() { }
    }

    private class RecordingHttpClientFactory : IHttpClientFactory
    {
        public string? LastClientName { get; private set; }

        public HttpClient CreateClient(string name)
        {
            LastClientName = name;
            return new HttpClient();
        }
    }

    [Fact]
    public void For_OpenAiCompatible_ReturnsOpenAiCompatibleAdapter()
    {
        var factory = new LlmClientFactory(
            new RecordingHttpClientFactory(),
            new FakeKeyVault(),
            new FakeLoggerFactory(),
            new FakeServiceProvider()
        );
        var provider = CreateProvider(adapterType: AdapterType.OpenAiCompatible);

        var client = factory.For(provider);

        Assert.IsType<OpenAiCompatibleAdapter>(client);
        Assert.Equal(AdapterType.OpenAiCompatible, client.AdapterType);
    }

    [Fact]
    public void For_Anthropic_ReturnsAnthropicAdapter()
    {
        var factory = new LlmClientFactory(
            new RecordingHttpClientFactory(),
            new FakeKeyVault(),
            new FakeLoggerFactory(),
            new FakeServiceProvider()
        );
        var provider = CreateProvider(adapterType: AdapterType.Anthropic);

        var client = factory.For(provider);

        Assert.IsType<AnthropicAdapter>(client);
        Assert.Equal(AdapterType.Anthropic, client.AdapterType);
    }

    [Fact]
    public void For_Ollama_ReturnsOllamaAdapter()
    {
        var factory = new LlmClientFactory(
            new RecordingHttpClientFactory(),
            new FakeKeyVault(),
            new FakeLoggerFactory(),
            new FakeServiceProvider()
        );
        var provider = CreateProvider(adapterType: AdapterType.Ollama);

        var client = factory.For(provider);

        Assert.IsType<OllamaAdapter>(client);
        Assert.Equal(AdapterType.Ollama, client.AdapterType);
    }

    [Fact]
    public void For_UnknownAdapterType_ThrowsNotSupportedException()
    {
        var factory = new LlmClientFactory(
            new RecordingHttpClientFactory(),
            new FakeKeyVault(),
            new FakeLoggerFactory(),
            new FakeServiceProvider()
        );
        var provider = CreateProvider(adapterType: AdapterType.DallE);

        var act = () => factory.For(provider);

        Assert.Throws<NotSupportedException>(act);
    }

    [Fact]
    public void For_SameProviderId_ReturnsCachedInstance()
    {
        var httpFactory = new RecordingHttpClientFactory();
        var factory = new LlmClientFactory(
            httpFactory,
            new FakeKeyVault(),
            new FakeLoggerFactory(),
            new FakeServiceProvider()
        );
        var providerId = Guid.NewGuid();
        var provider = CreateProvider(id: providerId, adapterType: AdapterType.OpenAiCompatible);

        var client1 = factory.For(provider);
        var client2 = factory.For(provider);

        Assert.Same(client1, client2);
    }

    [Fact]
    public void For_DifferentProviderIds_ReturnsDifferentInstances()
    {
        var factory = new LlmClientFactory(
            new RecordingHttpClientFactory(),
            new FakeKeyVault(),
            new FakeLoggerFactory(),
            new FakeServiceProvider()
        );
        var provider1 = CreateProvider(
            id: Guid.NewGuid(),
            adapterType: AdapterType.OpenAiCompatible
        );
        var provider2 = CreateProvider(id: Guid.NewGuid(), adapterType: AdapterType.Anthropic);

        var client1 = factory.For(provider1);
        var client2 = factory.For(provider2);

        Assert.NotSame(client1, client2);
    }

    [Fact]
    public void ImageFor_DallE_ReturnsDallEAdapter()
    {
        var factory = new LlmClientFactory(
            new RecordingHttpClientFactory(),
            new FakeKeyVault(),
            new FakeLoggerFactory(),
            new FakeServiceProvider()
        );
        var provider = CreateProvider(adapterType: AdapterType.DallE);

        var client = factory.ImageFor(provider);

        Assert.IsType<DallEAdapter>(client);
        Assert.Equal(AdapterType.DallE, client.AdapterType);
    }

    [Fact]
    public void ImageFor_StabilityAi_ReturnsStabilityAiAdapter()
    {
        var factory = new LlmClientFactory(
            new RecordingHttpClientFactory(),
            new FakeKeyVault(),
            new FakeLoggerFactory(),
            new FakeServiceProvider()
        );
        var provider = CreateProvider(adapterType: AdapterType.StabilityAi);

        var client = factory.ImageFor(provider);

        Assert.IsType<StabilityAiAdapter>(client);
        Assert.Equal(AdapterType.StabilityAi, client.AdapterType);
    }

    [Fact]
    public void ImageFor_ComfyUi_ReturnsComfyUiAdapter()
    {
        var factory = new LlmClientFactory(
            new RecordingHttpClientFactory(),
            new FakeKeyVault(),
            new FakeLoggerFactory(),
            new FakeServiceProvider()
        );
        var provider = CreateProvider(adapterType: AdapterType.ComfyUi);

        var client = factory.ImageFor(provider);

        Assert.IsType<ComfyUiAdapter>(client);
        Assert.Equal(AdapterType.ComfyUi, client.AdapterType);
    }

    [Fact]
    public void ImageFor_Diffusers_ReturnsComfyUiAdapter()
    {
        var factory = new LlmClientFactory(
            new RecordingHttpClientFactory(),
            new FakeKeyVault(),
            new FakeLoggerFactory(),
            new FakeServiceProvider()
        );
        var provider = CreateProvider(adapterType: AdapterType.Diffusers);

        var client = factory.ImageFor(provider);

        Assert.IsType<ComfyUiAdapter>(client);
        Assert.Equal(AdapterType.Diffusers, client.AdapterType);
    }

    [Fact]
    public void ImageFor_SameProviderId_ReturnsCachedInstance()
    {
        var factory = new LlmClientFactory(
            new RecordingHttpClientFactory(),
            new FakeKeyVault(),
            new FakeLoggerFactory(),
            new FakeServiceProvider()
        );
        var providerId = Guid.NewGuid();
        var provider = CreateProvider(id: providerId, adapterType: AdapterType.DallE);

        var client1 = factory.ImageFor(provider);
        var client2 = factory.ImageFor(provider);

        Assert.Same(client1, client2);
    }

    [Fact]
    public void ImageFor_DifferentProviderIds_ReturnsDifferentInstances()
    {
        var factory = new LlmClientFactory(
            new RecordingHttpClientFactory(),
            new FakeKeyVault(),
            new FakeLoggerFactory(),
            new FakeServiceProvider()
        );
        var provider1 = CreateProvider(id: Guid.NewGuid(), adapterType: AdapterType.DallE);
        var provider2 = CreateProvider(id: Guid.NewGuid(), adapterType: AdapterType.StabilityAi);

        var client1 = factory.ImageFor(provider1);
        var client2 = factory.ImageFor(provider2);

        Assert.NotSame(client1, client2);
    }

    [Fact]
    public void EmbeddingFor_NonOpenAiCompatible_ThrowsNotSupportedException()
    {
        var factory = new LlmClientFactory(
            new RecordingHttpClientFactory(),
            new FakeKeyVault(),
            new FakeLoggerFactory(),
            new FakeServiceProvider()
        );
        var provider = CreateProvider(adapterType: AdapterType.Anthropic);

        var act = () => factory.EmbeddingFor(provider);

        Assert.Throws<NotSupportedException>(act);
    }

    [Fact]
    public void EmbeddingFor_OpenAiCompatible_ReturnsIEmbeddingClient()
    {
        var factory = new LlmClientFactory(
            new RecordingHttpClientFactory(),
            new FakeKeyVault(),
            new FakeLoggerFactory(),
            new FakeServiceProvider()
        );
        var provider = CreateProvider(adapterType: AdapterType.OpenAiCompatible);

        var client = factory.EmbeddingFor(provider);

        Assert.IsType<OpenAiCompatibleAdapter>(client);
        Assert.IsAssignableFrom<IEmbeddingClient>(client);
    }

    [Fact]
    public void Invalidate_EvictsCachedClient_NextForCallReturnsNewInstance()
    {
        var factory = new LlmClientFactory(
            new RecordingHttpClientFactory(),
            new FakeKeyVault(),
            new FakeLoggerFactory(),
            new FakeServiceProvider()
        );
        var providerId = Guid.NewGuid();
        var provider = CreateProvider(id: providerId, adapterType: AdapterType.OpenAiCompatible);

        var client1 = factory.For(provider);
        factory.Invalidate(providerId);
        var client2 = factory.For(provider);

        Assert.NotSame(client1, client2);
    }

    [Fact]
    public void Invalidate_EvictsCachedImageClient_NextImageForCallReturnsNewInstance()
    {
        var factory = new LlmClientFactory(
            new RecordingHttpClientFactory(),
            new FakeKeyVault(),
            new FakeLoggerFactory(),
            new FakeServiceProvider()
        );
        var providerId = Guid.NewGuid();
        var provider = CreateProvider(id: providerId, adapterType: AdapterType.DallE);

        var client1 = factory.ImageFor(provider);
        factory.Invalidate(providerId);
        var client2 = factory.ImageFor(provider);

        Assert.NotSame(client1, client2);
    }

    [Fact]
    public void Invalidate_UnknownProviderId_DoesNotThrow()
    {
        var factory = new LlmClientFactory(
            new RecordingHttpClientFactory(),
            new FakeKeyVault(),
            new FakeLoggerFactory(),
            new FakeServiceProvider()
        );

        factory.Invalidate(Guid.NewGuid());
    }

    [Fact]
    public void ImageFor_UnknownAdapterType_ThrowsNotSupportedException()
    {
        var factory = new LlmClientFactory(
            new RecordingHttpClientFactory(),
            new FakeKeyVault(),
            new FakeLoggerFactory(),
            new FakeServiceProvider()
        );
        var provider = CreateProvider(adapterType: AdapterType.Ollama);

        var act = () => factory.ImageFor(provider);

        Assert.Throws<NotSupportedException>(act);
    }

    private sealed class FakeServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}
