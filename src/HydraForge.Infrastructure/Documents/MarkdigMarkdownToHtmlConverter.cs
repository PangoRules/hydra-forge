using HydraForge.Application.Shared;
using Markdig;

namespace HydraForge.Infrastructure.Documents;

public class MarkdigMarkdownToHtmlConverter : IMarkdownToHtmlConverter
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        // ReverseMarkdownConverter (the write-path counterpart) round-trips <br>
        // as a bare '\n', matching the Web UI's own Turndown config and marked's
        // breaks:true parse option — without this, CommonMark treats that bare
        // '\n' as a soft break (invisible, no <br>), silently swallowing every
        // line break within a paragraph on the way back out.
        .UseSoftlineBreakAsHardlineBreak()
        .Build();

    public string Convert(string markdown)
    {
        return Markdown.ToHtml(markdown, Pipeline).Trim();
    }
}
