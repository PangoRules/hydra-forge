using HydraForge.Application.Shared;
using ReverseMarkdown;

namespace HydraForge.Infrastructure.Documents;

public class ReverseMarkdownConverter : IHtmlToMarkdownConverter
{
    private readonly Converter _converter = new(
        new Config
        {
            UnknownTags = Config.UnknownTagsOption.PassThrough,
            GithubFlavored = true,
            RemoveComments = true,
        }
    );

    public string Convert(string html)
    {
        return _converter.Convert(html).Trim();
    }
}
