using System.Net;
using HydraForge.Application.Settings;
using HydraForge.Domain.Entities.PersonalSpace;
using HydraForge.Infrastructure.Notifications;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace HydraForge.Infrastructure.Tests.Notifications;

public class NtfyClientTests
{
    private class FakeSettingsProvider(string? ntfyUrl) : ISettingsProvider
    {
        public Task<SystemSettings> GetAsync(CancellationToken ct = default)
        {
            var settings = new SystemSettings();
            settings.UpdateSettings(ntfyServerUrl: ntfyUrl);
            return Task.FromResult(settings);
        }

        public void Invalidate() { }
    }

    private class CountingFakeSettingsProvider(string? initialUrl) : ISettingsProvider
    {
        public int CallCount { get; private set; }
        private string? _ntfyUrl = initialUrl;

        public void SetUrl(string? url) => _ntfyUrl = url;

        public Task<SystemSettings> GetAsync(CancellationToken ct = default)
        {
            CallCount++;
            var settings = new SystemSettings();
            settings.UpdateSettings(ntfyServerUrl: _ntfyUrl);
            return Task.FromResult(settings);
        }

        public void Invalidate() { }
    }

    private class RecordingHandler : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }
        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken ct
        )
        {
            LastRequest = request;
            CallCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }

    private class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken ct
        ) => throw new HttpRequestException("connection refused");
    }

    // Records every Log<TState> call's level so tests can assert a Warning was (or wasn't)
    // emitted, without pulling in a mocking library this test project doesn't already
    // reference.
    private class FakeLogger<T> : ILogger<T>
    {
        public List<LogLevel> LoggedLevels { get; } = [];

        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter
        ) => LoggedLevels.Add(logLevel);

        private class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();

            public void Dispose() { }
        }
    }

    [Fact]
    public async Task PublishAsync_WithNullServerUrl_MakesNoHttpCall()
    {
        var handler = new RecordingHandler();
        var http = new HttpClient(handler);
        var client = new NtfyClient(
            http,
            Options.Create(new NtfyOptions()),
            new FakeSettingsProvider(null),
            NullLogger<NtfyClient>.Instance
        );

        await client.PublishAsync(Guid.NewGuid(), "Title", "Body");

        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task PublishAsync_WithUnreachableServer_SwallowsException()
    {
        var http = new HttpClient(new ThrowingHandler());
        var client = new NtfyClient(
            http,
            Options.Create(new NtfyOptions()),
            new FakeSettingsProvider("https://unreachable.invalid"),
            NullLogger<NtfyClient>.Instance
        );

        var exception = await Record.ExceptionAsync(() =>
            client.PublishAsync(Guid.NewGuid(), "Title", "Body")
        );

        Assert.Null(exception);
    }

    [Fact]
    public async Task PublishAsync_WithServerUrl_PostsToPerUserTopic()
    {
        var handler = new RecordingHandler();
        var http = new HttpClient(handler);
        var userId = Guid.NewGuid();
        var client = new NtfyClient(
            http,
            Options.Create(new NtfyOptions()),
            new FakeSettingsProvider("https://ntfy.example.com"),
            NullLogger<NtfyClient>.Instance
        );

        await client.PublishAsync(userId, "Title", "Body");

        Assert.Equal(1, handler.CallCount);
        Assert.Equal(
            $"https://ntfy.example.com/hydraforge-{userId}",
            handler.LastRequest!.RequestUri!.ToString()
        );
    }

    [Fact]
    public async Task PublishAsync_CallsGetAsyncOnEveryCall()
    {
        var countingProvider = new CountingFakeSettingsProvider("http://ntfy.local");
        var handler = new RecordingHandler();
        var http = new HttpClient(handler);
        var client = new NtfyClient(
            http,
            Options.Create(new NtfyOptions()),
            countingProvider,
            NullLogger<NtfyClient>.Instance
        );

        await client.PublishAsync(Guid.NewGuid(), "Title", "Body");
        await client.PublishAsync(Guid.NewGuid(), "Title", "Body");

        Assert.Equal(2, countingProvider.CallCount);
    }

    [Fact]
    public async Task PublishAsync_UrlChangeBetweenCalls_DoesNotThrow()
    {
        var countingProvider = new CountingFakeSettingsProvider("https://ntfy.example.com");
        var handler = new RecordingHandler();
        var http = new HttpClient(handler);
        var client = new NtfyClient(
            http,
            Options.Create(new NtfyOptions()),
            countingProvider,
            NullLogger<NtfyClient>.Instance
        );

        await client.PublishAsync(Guid.NewGuid(), "Title", "Body");

        countingProvider.SetUrl(null);
        var exception = await Record.ExceptionAsync(() =>
            client.PublishAsync(Guid.NewGuid(), "Title", "Body")
        );

        Assert.Null(exception);
        Assert.Equal(2, countingProvider.CallCount);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task PublishAsync_WithHttpsUrl_MakesHttpCall()
    {
        var handler = new RecordingHandler();
        var http = new HttpClient(handler);
        var client = new NtfyClient(
            http,
            Options.Create(new NtfyOptions()),
            new FakeSettingsProvider("https://ntfy.example.com"),
            NullLogger<NtfyClient>.Instance
        );

        await client.PublishAsync(Guid.NewGuid(), "Title", "Body");

        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task PublishAsync_WithHttpLocalhostUrl_MakesHttpCall()
    {
        var handler = new RecordingHandler();
        var http = new HttpClient(handler);
        var client = new NtfyClient(
            http,
            Options.Create(new NtfyOptions()),
            new FakeSettingsProvider("http://localhost:8080"),
            NullLogger<NtfyClient>.Instance
        );

        await client.PublishAsync(Guid.NewGuid(), "Title", "Body");

        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task PublishAsync_WithHttp127001Url_MakesHttpCall()
    {
        var handler = new RecordingHandler();
        var http = new HttpClient(handler);
        var client = new NtfyClient(
            http,
            Options.Create(new NtfyOptions()),
            new FakeSettingsProvider("http://127.0.0.1:8080"),
            NullLogger<NtfyClient>.Instance
        );

        await client.PublishAsync(Guid.NewGuid(), "Title", "Body");

        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task PublishAsync_WithHttpSingleLabelHostUrl_MakesHttpCall()
    {
        // Docker Compose service names (this repo ships an `ntfy` service reachable at
        // http://ntfy on the compose network) resolve as single-label hostnames — no '.' —
        // which can never resolve on the public internet by DNS convention, so http:// is
        // safe to allow for them the same way it is for localhost/127.0.0.1.
        var handler = new RecordingHandler();
        var http = new HttpClient(handler);
        var client = new NtfyClient(
            http,
            Options.Create(new NtfyOptions()),
            new FakeSettingsProvider("http://ntfy"),
            NullLogger<NtfyClient>.Instance
        );

        await client.PublishAsync(Guid.NewGuid(), "Title", "Body");

        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task PublishAsync_WithHttpRemoteHostUrl_MakesNoHttpCall()
    {
        var handler = new RecordingHandler();
        var http = new HttpClient(handler);
        var client = new NtfyClient(
            http,
            Options.Create(new NtfyOptions()),
            new FakeSettingsProvider("http://ntfy.example.com"),
            NullLogger<NtfyClient>.Instance
        );

        await client.PublishAsync(Guid.NewGuid(), "Title", "Body");

        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task PublishAsync_WithHttpPublicHostUrl_MakesNoHttpCall()
    {
        // A dotted hostname is a real, publicly-resolvable domain — must stay rejected even
        // though single-label Docker service names are now allowed.
        var handler = new RecordingHandler();
        var http = new HttpClient(handler);
        var client = new NtfyClient(
            http,
            Options.Create(new NtfyOptions()),
            new FakeSettingsProvider("http://some.public.host"),
            NullLogger<NtfyClient>.Instance
        );

        await client.PublishAsync(Guid.NewGuid(), "Title", "Body");

        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task PublishAsync_WithMalformedUrl_MakesNoHttpCall()
    {
        var handler = new RecordingHandler();
        var http = new HttpClient(handler);
        var client = new NtfyClient(
            http,
            Options.Create(new NtfyOptions()),
            new FakeSettingsProvider("not a valid url at all"),
            NullLogger<NtfyClient>.Instance
        );

        await client.PublishAsync(Guid.NewGuid(), "Title", "Body");

        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task PublishAsync_WithUnsupportedScheme_MakesNoHttpCall()
    {
        var handler = new RecordingHandler();
        var http = new HttpClient(handler);
        var client = new NtfyClient(
            http,
            Options.Create(new NtfyOptions()),
            new FakeSettingsProvider("ftp://ntfy.example.com"),
            NullLogger<NtfyClient>.Instance
        );

        await client.PublishAsync(Guid.NewGuid(), "Title", "Body");

        Assert.Equal(0, handler.CallCount);
    }

    [Theory]
    [InlineData("not a valid url at all")]
    [InlineData("ftp://ntfy.example.com")]
    [InlineData("http://some.public.host")]
    public async Task PublishAsync_WithRejectedUrl_LogsWarning(string rejectedUrl)
    {
        var handler = new RecordingHandler();
        var http = new HttpClient(handler);
        var logger = new FakeLogger<NtfyClient>();
        var client = new NtfyClient(
            http,
            Options.Create(new NtfyOptions()),
            new FakeSettingsProvider(rejectedUrl),
            logger
        );

        await client.PublishAsync(Guid.NewGuid(), "Title", "Body");

        Assert.Equal(0, handler.CallCount);
        Assert.Contains(LogLevel.Warning, logger.LoggedLevels);
    }
}
