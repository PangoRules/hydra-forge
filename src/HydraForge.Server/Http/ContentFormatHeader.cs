namespace HydraForge.Server.Http;

/// <summary>
/// Explicit signal for whether a Spec/Plan Content submission is TipTap HTML (the
/// Web UI's editor) or already Markdown (the TUI's $EDITOR, or any client sending
/// Markdown directly). Content is a Markdown contract end-to-end — the header lets
/// the write path convert HTML to Markdown at the boundary instead of guessing from
/// the string's shape. Absent header (or any value other than "Html") = stored
/// verbatim as Markdown, which is the TUI's existing behavior unchanged.
/// </summary>
public static class ContentFormatHeader
{
    public const string Name = "X-Content-Format";
    public const string Html = "Html";

    public static bool IsHtml(HttpRequest request) =>
        string.Equals(request.Headers[Name].ToString(), Html, StringComparison.OrdinalIgnoreCase);
}
