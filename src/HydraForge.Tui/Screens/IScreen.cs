namespace HydraForge.Tui.Screens;

public interface IScreen
{
    Task RenderAsync();
    Task HandleKeyAsync(ConsoleKeyInfo key);
    Task OnEnterAsync();
    Task OnExitAsync();
}
