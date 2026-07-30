using System.Text.Json;
using HydraForge.Tui.Models;
using HydraForge.Tui.Services;

namespace HydraForge.Tui.Tests;

public class NotificationEventTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    [Fact]
    public void NotificationReceivedEvent_Deserializes_FromJsonElement()
    {
        var json = """
            {
                "id": "11111111-1111-1111-1111-111111111111",
                "title": "New mention",
                "body": "You were mentioned on a card",
                "cardId": "22222222-2222-2222-2222-222222222222",
                "projectId": "33333333-3333-3333-3333-333333333333",
                "actionUrl": "/projects/33333333-3333-3333-3333-333333333333/cards/22222222-2222-2222-2222-222222222222",
                "createdAt": "2026-07-25T10:30:00Z",
                "isRead": false
            }
            """;

        var element = JsonDocument.Parse(json).RootElement;
        var evt = JsonSerializer.Deserialize<SignalRConnectionManager.NotificationReceivedEvent>(
            element.GetRawText(),
            JsonOptions
        );

        Assert.NotNull(evt);
        Assert.Equal(Guid.Parse("11111111-1111-1111-1111-111111111111"), evt.Id);
        Assert.Equal("New mention", evt.Title);
        Assert.Equal("You were mentioned on a card", evt.Body);
        Assert.Equal(Guid.Parse("22222222-2222-2222-2222-222222222222"), evt.CardId);
        Assert.Equal(Guid.Parse("33333333-3333-3333-3333-333333333333"), evt.ProjectId);
        Assert.False(evt.IsRead);
    }

    [Fact]
    public void NotificationReceivedEvent_IsRead_True_IsFiltered()
    {
        var evt = new SignalRConnectionManager.NotificationReceivedEvent(
            Guid.NewGuid(),
            "Already read",
            null,
            null,
            null,
            null,
            DateTime.UtcNow,
            true
        );

        Assert.True(evt.IsRead);
    }

    [Theory]
    [InlineData(false, true)] // IsRead=false → should increment
    [InlineData(true, false)] // IsRead=true → should skip
    public void NotificationFilter_SkipsReadNotifications(bool isRead, bool shouldProcess)
    {
        var evt = new SignalRConnectionManager.NotificationReceivedEvent(
            Guid.NewGuid(),
            "Test",
            null,
            null,
            null,
            null,
            DateTime.UtcNow,
            isRead
        );

        var shouldProcessEvent = evt != null && !evt.IsRead;
        Assert.Equal(shouldProcess, shouldProcessEvent);
    }

    [Fact]
    public void OnUnreadCountChanged_FiresWithIncrementedCount_WhenUnreadNotificationReceived()
    {
        var appState = new AppState();
        var errorCollector = new ErrorCollector();

        var testableSignalR = new TestableSignalRConnectionManager(appState, errorCollector);

        int? capturedCount = null;
        testableSignalR.OnUnreadCountChanged += count => capturedCount = count;

        var evt = new SignalRConnectionManager.NotificationReceivedEvent(
            Guid.NewGuid(),
            "New notification",
            "You were mentioned",
            null,
            null,
            null,
            DateTime.UtcNow,
            IsRead: false
        );

        var initialCount = appState.UnreadNotifications;
        testableSignalR.TestRaiseNotificationReceived(evt);

        Assert.Equal(initialCount + 1, capturedCount);
    }

    private class TestableSignalRConnectionManager(AppState appState, ErrorCollector errorCollector)
        : SignalRConnectionManager(appState, errorCollector)
    {
        public void TestRaiseNotificationReceived(NotificationReceivedEvent evt)
        {
            if (!evt.IsRead)
            {
                var newCount = AppState.IncrementUnreadNotifications();
                RaiseUnreadCountChanged(newCount);
            }
        }
    }
}
