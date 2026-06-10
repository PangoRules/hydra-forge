namespace HydraForge.Application.Tests.Comments;

public class MentionExtractorTests
{
    [Fact]
    public void Extract_SingleMention_ReturnsUsername()
    {
        var result = MentionExtractor.Extract("Hello @alice how are you?");
        Assert.Single(result);
        Assert.Equal("alice", result[0]);
    }

    [Fact]
    public void Extract_MultipleMentions_ReturnsDistinct()
    {
        var result = MentionExtractor.Extract("Hey @alice and @bob and @alice again");
        Assert.Equal(2, result.Count);
        Assert.Contains("alice", result);
        Assert.Contains("bob", result);
    }

    [Fact]
    public void Extract_MentionsAreCaseInsensitive()
    {
        var result = MentionExtractor.Extract("@Alice and @ALICE and @alice");
        Assert.Single(result);
    }

    [Fact]
    public void Extract_EmptyString_ReturnsEmpty()
    {
        var result = MentionExtractor.Extract("");
        Assert.Empty(result);
    }

    [Fact]
    public void Extract_Null_ReturnsEmpty()
    {
        var result = MentionExtractor.Extract(null!);
        Assert.Empty(result);
    }

    [Fact]
    public void Extract_NoMentions_ReturnsEmpty()
    {
        var result = MentionExtractor.Extract("Hello world without mentions");
        Assert.Empty(result);
    }

    [Fact]
    public void Extract_MentionWithDotAndDash_ReturnsUsername()
    {
        var result = MentionExtractor.Extract("cc @john.doe or @jane-doe");
        Assert.Equal(2, result.Count);
        Assert.Contains("john.doe", result);
        Assert.Contains("jane-doe", result);
    }

    [Fact]
    public void Extract_MentionWithUnderscore_ReturnsUsername()
    {
        var result = MentionExtractor.Extract("Hey @john_doe");
        Assert.Single(result);
        Assert.Equal("john_doe", result[0]);
    }

    [Fact]
    public void Extract_EmailNotExtracted()
    {
        var result = MentionExtractor.Extract("Contact user@example.com");
        Assert.Empty(result);
    }

    [Fact]
    public void Extract_MentionInParens_ReturnsUsername()
    {
        var result = MentionExtractor.Extract("(see @alice)");
        Assert.Single(result);
        Assert.Equal("alice", result[0]);
    }

    [Fact]
    public void Extract_MentionAtStart_ReturnsUsername()
    {
        var result = MentionExtractor.Extract("@alice please review");
        Assert.Single(result);
        Assert.Equal("alice", result[0]);
    }

    [Fact]
    public void Extract_MentionAtEnd_ReturnsUsername()
    {
        var result = MentionExtractor.Extract("please review @alice");
        Assert.Single(result);
        Assert.Equal("alice", result[0]);
    }

    [Fact]
    public void Extract_MentionPrecededByPunctuation_IsExtracted()
    {
        var result = MentionExtractor.Extract("Hello! @alice? What's up @bob.");
        Assert.Equal(2, result.Count);
    }
}