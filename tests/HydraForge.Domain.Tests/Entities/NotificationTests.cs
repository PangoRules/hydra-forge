using HydraForge.Domain.Entities.PersonalSpace;

namespace HydraForge.Domain.Tests.Entities;

public class NotificationTests
{
    [Fact]
    public void Create_SetsAllProperties()
    {
        var userId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var projectId = Guid.NewGuid();

        var notif = Notification.Create(
            userId,
            "Test Title",
            "Test Body",
            "Test Message",
            cardId,
            projectId,
            "/projects/123"
        );

        Assert.NotEqual(Guid.Empty, notif.Id);
        Assert.Equal(userId, notif.UserId);
        Assert.Equal("Test Title", notif.Title);
        Assert.Equal("Test Body", notif.Body);
        Assert.Equal("Test Message", notif.Message);
        Assert.Equal(cardId, notif.CardId);
        Assert.Equal(projectId, notif.ProjectId);
        Assert.Equal("/projects/123", notif.ActionUrl);
        Assert.False(notif.IsRead);
        Assert.True(notif.CreatedAt > DateTime.UtcNow.AddMinutes(-1));
    }

    [Fact]
    public void MarkRead_SetsIsReadToTrue()
    {
        var notif = Notification.Create(Guid.NewGuid(), "Title", null, "Message", null, null, null);

        notif.MarkRead();

        Assert.True(notif.IsRead);
    }

    [Fact]
    public void Create_WithNullOptionals_Works()
    {
        var notif = Notification.Create(Guid.NewGuid(), "Title", null, "Message", null, null, null);

        Assert.Null(notif.Body);
        Assert.Null(notif.CardId);
        Assert.Null(notif.ProjectId);
        Assert.Null(notif.ActionUrl);
    }
}
