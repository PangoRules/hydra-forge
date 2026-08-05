using HydraForge.Application.Shared;
using Markdig;

namespace HydraForge.Infrastructure.Documents;

public class MarkdigMarkdownToHtmlConverter : IMarkdownToHtmlConverter
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();

    public string Convert(string markdown)
    {
        return Markdown.ToHtml(markdown, Pipeline).Trim();
    }
}
