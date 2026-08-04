using HydraForge.Domain.Common;

namespace HydraForge.Application.Chat;

public interface IChatTitleGenerator
{
    /// <summary>
    /// One cheap LLM call producing a short (3-6 word) title from a session's opening
    /// exchange — analogous to <see cref="IChatSummaryGenerator"/> but for the title,
    /// triggered on the first message of a session rather than on close.
    /// </summary>
    Task<Result<string>> GenerateTitleAsync(
        Guid userId,
        string userMessage,
        string assistantMessage,
        CancellationToken ct = default
    );
}
