using HydraForge.Infrastructure.Documents;

namespace HydraForge.Infrastructure.Tests.Documents;

public class ReverseMarkdownConverterTests
{
    private readonly ReverseMarkdownConverter _converter = new();

    [Fact]
    public void Convert_ConsecutiveParagraphs_SeparatedByBlankLine()
    {
        var html = "<p>First paragraph</p><p>Second paragraph</p>";

        var markdown = _converter.Convert(html);

        Assert.Equal("First paragraph\n\nSecond paragraph", markdown);
    }

    [Fact]
    public void Convert_LineBreakWithinParagraph_ProducesBareNewline()
    {
        // Matches the Web UI's client-side Turndown config and the paired
        // Markdig UseSoftlineBreakAsHardlineBreak() on the read path — both
        // sides of the round trip must agree that a bare '\n' means a real
        // line break, not "part of the same flowing paragraph".
        var html = "<p>Line one<br>Line two</p>";

        var markdown = _converter.Convert(html);

        Assert.Equal("Line one\nLine two", markdown);
    }

    [Fact]
    public void Convert_GfmTable_ProducesPipeTableWithHeaderSeparator()
    {
        var html = "<table><tr><th>A</th><th>B</th></tr><tr><td>1</td><td>2</td></tr></table>";

        var markdown = _converter.Convert(html);

        Assert.Contains("| A | B |", markdown);
        Assert.Contains("| --- | --- |", markdown);
        Assert.Contains("| 1 | 2 |", markdown);
    }

    [Fact]
    public void Convert_Bold_ProducesAsterisks()
    {
        Assert.Equal("**bold**", _converter.Convert("<p><strong>bold</strong></p>"));
    }
}
