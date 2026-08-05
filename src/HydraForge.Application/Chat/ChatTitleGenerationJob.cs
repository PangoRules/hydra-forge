using HydraForge.Application.Realtime;
using HydraForge.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace HydraForge.Application.Chat;

/// <summary>
/// Generates and persists a session's title, run as its own Hangfire job — deliberately
/// separate from <see cref="ChatReplyGenerator"/>'s job so a slow/retried title-gen call
/// (reasoning models can take a while, see <see cref="LlmChatTitleGenerator"/>) never
/// delays the chat reply's own completion signal (<c>StreamDone</c>), which is what
/// actually unblocks the user's chat input. The two LLM calls run fully independently —
/// enqueued back-to-back from the same trigger, but on separate Hangfire workers with
/// their own scope, cancellation, and retry lifecycle.
/// </summary>
public sealed class ChatTitleGenerationJob(
    IChatSessionRepository sessionRepo,
    IChatTitleGenerator titleGenerator,
    IChatBroadcaster broadcaster,
    ILogger<ChatTitleGenerationJob> logger
)
{
    public async Task RunAsync(
        Guid sessionId,
        Guid userId,
        string userMessageContent,
        string assistantMessageContent,
        CancellationToken ct
    )
    {
        try
        {
            var session = await sessionRepo.GetByIdAsync(sessionId, ct);
            // Session may have been archived/closed by the time this runs (it's queued
            // independently of the reply job) — nothing to title in that case.
            if (session is null || session.Status != ChatSessionStatus.Active)
                return;

            var titleResult = await titleGenerator.GenerateTitleAsync(
                userId,
                userMessageContent,
                assistantMessageContent,
                ct
            );
            var title =
                titleResult.IsSuccess ? titleResult.Value
                : userMessageContent.Length <= 60 ? userMessageContent
                : userMessageContent[..60] + "…";

            session.UpdateSettings(title, null, null, null, null, null, null);
            await sessionRepo.UpdateAsync(session, ct);

            // This job runs decoupled from the chat reply's own request/response cycle
            // (that's the whole point — see the class doc comment) — without pushing the
            // result, a connected client has no way to learn the title changed short of a
            // manual refresh. Best-effort: a client that isn't connected just sees the new
            // title on its next fetch, same as before this push existed.
            await broadcaster
                .Group(sessionId)
                .SessionUpdated(sessionId, title, session.Status.ToString());
        }
        catch (Exception ex)
        {
            // External LLM call — must never surface as a failed/retried Hangfire job
            // spamming the dashboard over something as low-stakes as a chat title.
            logger.LogWarning(
                ex,
                "Chat title generation failed for session {SessionId}",
                sessionId
            );
        }
    }
}
