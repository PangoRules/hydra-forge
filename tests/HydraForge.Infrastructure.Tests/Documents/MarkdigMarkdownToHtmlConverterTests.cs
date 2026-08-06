using HydraForge.Infrastructure.Documents;

namespace HydraForge.Infrastructure.Tests.Documents;

public class MarkdigMarkdownToHtmlConverterTests
{
    private readonly MarkdigMarkdownToHtmlConverter _converter = new();

    [Fact]
    public void Convert_BlankLineSeparatedParagraphs_ProducesTwoParagraphTags()
    {
        var html = _converter.Convert("First paragraph\n\nSecond paragraph");

        Assert.Equal("<p>First paragraph</p>\n<p>Second paragraph</p>", html);
    }

    [Fact]
    public void Convert_BareNewlineWithinParagraph_ProducesBrTag()
    {
        // The regression this guards: without UseSoftlineBreakAsHardlineBreak(),
        // CommonMark treats a bare '\n' as an invisible soft break — two lines
        // that should stay visually distinct collapse into one flowing line.
        var html = _converter.Convert("Line one\nLine two");

        Assert.Contains("<br", html);
    }

    [Fact]
    public void Convert_Bold_ProducesStrongTag()
    {
        Assert.Equal("<p><strong>bold</strong></p>", _converter.Convert("**bold**"));
    }

    [Fact]
    public void Convert_GfmTable_ProducesTableTag()
    {
        var html = _converter.Convert("| A | B |\n| --- | --- |\n| 1 | 2 |");

        Assert.Contains("<table>", html);
        Assert.Contains("<th>A</th>", html);
        Assert.Contains("<td>1</td>", html);
    }
}
