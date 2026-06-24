namespace HydraForge.Application.Realtime;

public interface IProjectBoardEventPublisher
{
    Task PublishAsync(ProjectBoardEventEnvelope envelope, CancellationToken ct = default);
}
