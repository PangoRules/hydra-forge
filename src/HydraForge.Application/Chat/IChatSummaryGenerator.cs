using HydraForge.Domain.Common;

namespace HydraForge.Application.Chat;

public interface IChatSummaryGenerator
{
    Task<Result<string>> GenerateSummaryAsync(Guid sessionId, CancellationToken ct = default);
}
