using HydraForge.Infrastructure.Documents;

namespace HydraForge.Infrastructure.Tests.Documents;

/// <summary>
/// HTML -&gt; Markdown (write path, ReverseMarkdownConverter) -&gt; HTML (read path,
/// MarkdigMarkdownToHtmlConverter) must agree on what a bare '\n' means, or a
/// document that looked fine right after saving loses its line breaks the next
/// time it's opened. Caught for real in Plan 18's own content — a paste that
/// rendered as three distinct lines collapsed into one run-on line after a
/// save + reload once Content round-tripped through both converters.
/// </summary>
public class DocumentFormatRoundTripTests
{
    private readonly ReverseMarkdownConverter _htmlToMarkdown = new();
    private readonly MarkdigMarkdownToHtmlConverter _markdownToHtml = new();

    [Fact]
    public void LineBreakWithinAParagraph_SurvivesHtmlToMarkdownToHtmlRoundTrip()
    {
        var originalHtml =
            "<p><strong>Branch:</strong> <code>task/web-chat-panel</code>"
            + "<br><strong>Parent branch:</strong> <code>feat/phase-7-chat</code></p>";

        var markdown = _htmlToMarkdown.Convert(originalHtml);
        var roundTrippedHtml = _markdownToHtml.Convert(markdown);

        Assert.Contains("<br", roundTrippedHtml);
        Assert.Contains("Branch:", roundTrippedHtml);
        Assert.Contains("Parent branch:", roundTrippedHtml);
    }

    [Fact]
    public void SeparateParagraphs_StaySeparateAcrossRoundTrip()
    {
        var originalHtml = "<p>First</p><p>Second</p>";

        var markdown = _htmlToMarkdown.Convert(originalHtml);
        var roundTrippedHtml = _markdownToHtml.Convert(markdown);

        Assert.Equal("<p>First</p>\n<p>Second</p>", roundTrippedHtml);
    }
}
