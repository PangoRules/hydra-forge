namespace HydraForge.Application.Shared;

/// <summary>
/// Converts TipTap-authored HTML into Markdown before it's persisted as Spec/Plan
/// Content. Content is a Markdown contract end-to-end (TUI reads it as plain
/// Markdown text) — this is the boundary where a client that authors in HTML
/// (the Web UI's TipTap editor) gets normalized to that contract.
/// </summary>
public interface IHtmlToMarkdownConverter
{
    string Convert(string html);
}
