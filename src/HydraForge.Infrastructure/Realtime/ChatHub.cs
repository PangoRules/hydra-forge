using HydraForge.Application.Auth;
using HydraForge.Application.Chat;
using HydraForge.Application.Projects;
using HydraForge.Application.Realtime;
using HydraForge.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;

namespace HydraForge.Infrastructure.Realtime;

/// <summary>
/// JoinSession/LeaveSession/CancelStream stay live-connection-only (they're inherently
/// about *this* connection's group membership / cancelling *this* session's in-flight
/// stream). SendMessage is a thin enqueue — the actual reply generation lives in
/// <see cref="ChatReplyGenerator"/>, run as a Hangfire job so a dropped/slow connection
/// (mobile SignalR handshakes have been observed taking 90-170s+ over some networks) can't
/// cancel a reply that would otherwise complete fine. See ChatReplyGenerator's doc comment.
/// </summary>
[Authorize]
[EnableRateLimiting("SignalR")]
public class ChatHub(
    IUserRepository userRepo,
    IProjectMemberRepository memberRepo,
    IChatSessionRepository sessionRepo,
    IChatStreamRegistry streamRegistry,
    IBackgroundTaskQueue backgroundTaskQueue
) : Hub<IChatHub>
{
    internal static string SessionGroup(Guid sessionId) => $"chat-{sessionId}";

    private async Task<bool> HasSessionAccessAsync(
        Domain.Entities.Chat.ChatSession session,
        Guid userId,
        CancellationToken ct = default
    )
    {
        if (session.OwnerId == userId)
            return true;

        if (session.ProjectId.HasValue && session.IsShared)
            return await MembershipGuard.HasAccessAsync(
                userRepo,
                memberRepo,
                session.ProjectId.Value,
                userId,
                ct
            );

        return false;
    }

    public async Task JoinSession(Guid sessionId)
    {
        var userId = Context.User!.GetRequiredUserId();

        var session = await sessionRepo.GetByIdAsync(sessionId);
        if (session is null)
            throw new HubException(DomainErrorCodes.Chat.SessionNotFound);

        if (!await HasSessionAccessAsync(session, userId))
            throw new HubException(DomainErrorCodes.Chat.SessionNotOwner);

        await Groups.AddToGroupAsync(Context.ConnectionId, SessionGroup(sessionId));
    }

    public async Task LeaveSession(Guid sessionId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, SessionGroup(sessionId));
    }

    /// <summary>
    /// Enqueues the reply — does not wait for it. Kept as an alternate trigger path (parity
    /// with the REST endpoint on <c>ChatMessagesController</c>) for a caller with a live,
    /// fast connection that wants to skip the REST round trip; the current Web UI always
    /// triggers via REST instead, precisely because it can't assume a connection this good.
    /// </summary>
    public async Task SendMessage(
        Guid sessionId,
        Guid userMessageId,
        Guid? presetId,
        Guid? preferredProviderModelConfigId = null
    )
    {
        var userId = Context.User!.GetRequiredUserId();
        await backgroundTaskQueue.EnqueueJobAsync<ChatReplyGenerator>(g =>
            g.GenerateAsync(
                sessionId,
                userMessageId,
                userId,
                presetId,
                preferredProviderModelConfigId,
                CancellationToken.None
            )
        );
    }

    public async Task CancelStream(Guid sessionId)
    {
        var userId = Context.User!.GetRequiredUserId();

        var session = await sessionRepo.GetByIdAsync(sessionId);
        if (session is null || !await HasSessionAccessAsync(session, userId))
        {
            return;
        }

        streamRegistry.TryCancel(sessionId);
    }
}
