namespace HydraForge.Infrastructure.Chat;

using HydraForge.Application.Chat;
using HydraForge.Domain.Common;
using Microsoft.Extensions.Logging;

/// <summary>
/// Stub implementation of IChatSummaryGenerator for Infrastructure layer registration.
/// Production use requires replacing with LlmChatSummaryGenerator that calls the LLM API.
/// </summary>
public sealed class StubChatSummaryGenerator : IChatSummaryGenerator
{
    private readonly ILogger<StubChatSummaryGenerator> _logger;

    public StubChatSummaryGenerator(ILogger<StubChatSummaryGenerator> logger)
    {
        _logger = logger;
    }

    public Task<Result<string>> GenerateSummaryAsync(Guid sessionId, CancellationToken ct = default)
    {
        _logger.LogWarning(
            "StubChatSummaryGenerator invoked for session {SessionId}. Replace with LlmChatSummaryGenerator in production.",
            sessionId
        );
        return Task.FromResult(Result<string>.Success("Session summary (stub)"));
    }
}
