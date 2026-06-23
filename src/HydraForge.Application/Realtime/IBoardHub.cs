namespace HydraForge.Application.Realtime;

public interface IBoardHub
{
    Task OnBoardEvent(ProjectBoardEventEnvelope envelope);
}
