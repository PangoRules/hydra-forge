namespace HydraForge.Application.Llm;

public interface IUsageRecorder
{
    Task RecordTokenAsync(TokenUsageRecordInput input, CancellationToken ct = default);

    Task RecordImageAsync(ImageUsageRecordInput input, CancellationToken ct = default);

    Task<int> AccrueTokenUsageAsync(Guid userId, int tokens, CancellationToken ct = default);

    Task<int> AccrueImageUsageAsync(Guid userId, int count, CancellationToken ct = default);
}
