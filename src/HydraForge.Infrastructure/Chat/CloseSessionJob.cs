namespace HydraForge.Infrastructure.Chat;

using HydraForge.Application.Chat;
using HydraForge.Domain.Enums;
using Microsoft.Extensions.Logging;

public sealed class CloseSessionJob
{
    private readonly IChatSessionService _chatSessionService;
    private readonly ILogger<CloseSessionJob> _logger;

    public CloseSessionJob(IChatSessionService chatSessionService, ILogger<CloseSessionJob> logger)
    {
        _chatSessionService = chatSessionService;
        _logger = logger;
    }

    public async Task RunAsync(Guid sessionId, Guid actorId, CancellationToken ct)
    {
        try
        {
            var result = await _chatSessionService.CloseAsync(sessionId, actorId, ct);
            if (!result.IsSuccess)
            {
                _logger.LogWarning(
                    "CloseSessionJob failed for session {SessionId}: {ErrorCode}",
                    sessionId,
                    result.Error.Code
                );
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CloseSessionJob threw for session {SessionId}", sessionId);
        }
    }
}
