namespace HydraForge.Application.Realtime;

/// <summary>
/// Lets Application-layer code (e.g. <see cref="Chat.ChatReplyGenerator"/>, which can run
/// inside a Hangfire job with no live hub connection at all) push chat stream events to
/// whichever clients happen to be connected and joined to a session, without depending on
/// the concrete SignalR hub type (that's an Infrastructure concern).
/// </summary>
public interface IChatBroadcaster
{
    IChatHub Group(Guid sessionId);
}
