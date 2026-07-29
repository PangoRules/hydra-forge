using System.Net;
using HydraForge.Application.Settings;
using HydraForge.Domain.Entities.PersonalSpace;
using HydraForge.Infrastructure.Notifications;
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

    private class CountingFakeSettingsProvider : ISettingsProvider
    {
        public int CallCount { get; private set; }
        private string? _ntfyUrl;

        public CountingFakeSettingsProvider(string? initialUrl) => _ntfyUrl = initialUrl;

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

    [Fact]
    public async Task PublishAsync_WithNullServerUrl_MakesNoHttpCall()
    {
        var handler = new RecordingHandler();
        var http = new HttpClient(handler);
        var client = new NtfyClient(http, Options.Create(new NtfyOptions()), new FakeSettingsProvider(null));

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
            new FakeSettingsProvider("http://unreachable.invalid")
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
        var client = new NtfyClient(http, Options.Create(new NtfyOptions()), new FakeSettingsProvider("http://ntfy.local"));

        await client.PublishAsync(userId, "Title", "Body");

        Assert.Equal(1, handler.CallCount);
        Assert.Equal(
            $"http://ntfy.local/hydraforge-{userId}",
            handler.LastRequest!.RequestUri!.ToString()
        );
    }

    [Fact]
    public async Task PublishAsync_CallsGetAsyncOnEveryCall()
    {
        var countingProvider = new CountingFakeSettingsProvider("http://ntfy.local");
        var handler = new RecordingHandler();
        var http = new HttpClient(handler);
        var client = new NtfyClient(http, Options.Create(new NtfyOptions()), countingProvider);

        await client.PublishAsync(Guid.NewGuid(), "Title", "Body");
        await client.PublishAsync(Guid.NewGuid(), "Title", "Body");

        Assert.Equal(2, countingProvider.CallCount);
    }

    [Fact]
    public async Task PublishAsync_UrlChangeBetweenCalls_DoesNotThrow()
    {
        var countingProvider = new CountingFakeSettingsProvider("http://ntfy.local");
        var handler = new RecordingHandler();
        var http = new HttpClient(handler);
        var client = new NtfyClient(http, Options.Create(new NtfyOptions()), countingProvider);

        await client.PublishAsync(Guid.NewGuid(), "Title", "Body");

        countingProvider.SetUrl(null);
        var exception = await Record.ExceptionAsync(() =>
            client.PublishAsync(Guid.NewGuid(), "Title", "Body")
        );

        Assert.Null(exception);
        Assert.Equal(2, countingProvider.CallCount);
        Assert.Equal(1, handler.CallCount);
    }
}
