namespace HydraForge.Application.Shared;

/// <summary>
/// Converts stored Markdown Content back to HTML for a client that renders it in a
/// TipTap (HTML-native) editor. The read-side counterpart of
/// <see cref="IHtmlToMarkdownConverter"/> — together they let Spec/Plan Content stay
/// Markdown at rest while each client (Web UI vs TUI) gets the shape it expects,
/// driven by the same X-Content-Format request header on both read and write.
/// </summary>
public interface IMarkdownToHtmlConverter
{
    string Convert(string markdown);
}
