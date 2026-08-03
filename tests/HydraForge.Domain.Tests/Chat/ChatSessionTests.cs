using HydraForge.Domain.Entities.Chat;
using HydraForge.Domain.Enums;

namespace HydraForge.Domain.Tests.Chat;

public class ChatSessionTests
{
    [Fact]
    public void Open_SetsStatusToActive()
    {
        var session = new ChatSession();

        session.Open(Guid.NewGuid(), Guid.NewGuid(), AiEditMode.PerMutation);

        Assert.Equal(ChatSessionStatus.Active, session.Status);
    }

    [Fact]
    public void Open_SetsPersonalityIdAndOpenCardIdAndAiEditMode()
    {
        var session = new ChatSession();
        var personalityId = Guid.NewGuid();
        var openCardId = Guid.NewGuid();

        session.Open(personalityId, openCardId, AiEditMode.Blanket);

        Assert.Equal(personalityId, session.PersonalityId);
        Assert.Equal(openCardId, session.OpenCardId);
        Assert.Equal(AiEditMode.Blanket, session.AiEditMode);
    }

    [Fact]
    public void Open_ClearsClosedAtAndSummary()
    {
        var session = new ChatSession();
        session.Close("some summary");

        session.Open(null, null, AiEditMode.PerMutation);

        Assert.Null(session.ClosedAt);
        Assert.Null(session.Summary);
    }

    [Fact]
    public void Close_SetsStatusToClosed()
    {
        var session = new ChatSession();

        session.Close(null);

        Assert.Equal(ChatSessionStatus.Closed, session.Status);
    }

    [Fact]
    public void Close_SetsClosedAtAndSummary()
    {
        var session = new ChatSession();
        var summary = "Session summary";

        session.Close(summary);

        Assert.NotNull(session.ClosedAt);
        Assert.Equal(summary, session.Summary);
    }

    [Fact]
    public void Close_IsIdempotent()
    {
        var session = new ChatSession();
        session.Close("first");

        session.Close("second");

        Assert.Equal(ChatSessionStatus.Closed, session.Status);
        Assert.Equal("first", session.Summary);
    }

    [Fact]
    public void SetAiEditMode_RejectedWhenClosed()
    {
        var session = new ChatSession();
        session.Close(null);

        var act = () => session.SetAiEditMode(AiEditMode.Blanket);

        Assert.Throws<InvalidOperationException>(act);
    }

    [Fact]
    public void SetAiEditMode_UpdatesAiEditMode()
    {
        var session = new ChatSession();

        session.SetAiEditMode(AiEditMode.Blanket);

        Assert.Equal(AiEditMode.Blanket, session.AiEditMode);
    }

    [Fact]
    public void ToggleSearchAllMyDocs_TogglesFlag()
    {
        var session = new ChatSession();
        Assert.False(session.SearchAllMyDocs);

        session.ToggleSearchAllMyDocs(true);

        Assert.True(session.SearchAllMyDocs);

        session.ToggleSearchAllMyDocs(false);

        Assert.False(session.SearchAllMyDocs);
    }

    [Fact]
    public void ToggleSearchAllMyDocs_RejectedWhenClosed()
    {
        var session = new ChatSession();
        session.Close(null);

        var act = () => session.ToggleSearchAllMyDocs(true);

        Assert.Throws<InvalidOperationException>(act);
    }

    [Fact]
    public void Close_RevokesAiEditMode()
    {
        var session = new ChatSession();
        session.SetAiEditMode(AiEditMode.Blanket);

        session.Close(null);

        Assert.Equal(AiEditMode.PerMutation, session.AiEditMode);
    }

    [Fact]
    public void UpdateSettings_UpdatesProvidedFields()
    {
        var session = new ChatSession { Title = "Old" };
        var folderId = Guid.NewGuid();
        var personalityId = Guid.NewGuid();

        session.UpdateSettings("New", folderId, personalityId, AiEditMode.Blanket, true);

        Assert.Equal("New", session.Title);
        Assert.Equal(folderId, session.FolderId);
        Assert.Equal(personalityId, session.PersonalityId);
        Assert.Equal(AiEditMode.Blanket, session.AiEditMode);
        Assert.True(session.SearchAllMyDocs);
    }

    [Fact]
    public void UpdateSettings_LeavesUnspecifiedFieldsUnchanged()
    {
        var folderId = Guid.NewGuid();
        var session = new ChatSession
        {
            Title = "Keep",
            FolderId = folderId,
            SearchAllMyDocs = true,
        };

        session.UpdateSettings(null, null, null, null, null);

        Assert.Equal("Keep", session.Title);
        Assert.Equal(folderId, session.FolderId);
        Assert.True(session.SearchAllMyDocs);
    }

    [Fact]
    public void UpdateSettings_RejectedWhenClosed()
    {
        var session = new ChatSession();
        session.Close(null);

        var act = () => session.UpdateSettings("New", null, null, null, null);

        Assert.Throws<InvalidOperationException>(act);
    }

    [Fact]
    public void Archive_SetsArchivedAt()
    {
        var session = new ChatSession();

        session.Archive();

        Assert.NotNull(session.ArchivedAt);
    }
}
