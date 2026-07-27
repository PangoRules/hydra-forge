using System.Net;
using HydraForge.Infrastructure.Notifications;
using Microsoft.Extensions.Options;
using Xunit;

namespace HydraForge.Infrastructure.Tests.Notifications;

public class NtfyClientTests
{
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
        var client = new NtfyClient(http, Options.Create(new NtfyOptions()), serverUrl: null);

        await client.PublishAsync(Guid.NewGuid(), "Title", "Body");

        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task PublishAsync_WithUnreachableServer_SwallowsException()
    {
        var http = new HttpClient(new ThrowingHandler());
        var client = new NtfyClient(http, Options.Create(new NtfyOptions()), "http://unreachable.invalid");

        var exception = await Record.ExceptionAsync(() =>
            client.PublishAsync(Guid.NewGuid(), "Title", "Body"));

        Assert.Null(exception);
    }

    [Fact]
    public async Task PublishAsync_WithServerUrl_PostsToPerUserTopic()
    {
        var handler = new RecordingHandler();
        var http = new HttpClient(handler);
        var userId = Guid.NewGuid();
        var client = new NtfyClient(http, Options.Create(new NtfyOptions()), "http://ntfy.local");

        await client.PublishAsync(userId, "Title", "Body");

        Assert.Equal(1, handler.CallCount);
        Assert.Equal($"http://ntfy.local/hydraforge-{userId}", handler.LastRequest!.RequestUri!.ToString());
    }
}
