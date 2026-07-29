using HydraForge.Tui.Screens;

namespace HydraForge.Tui.Models;

public class ScreenStack
{
    private readonly Stack<IScreen> _stack = new();

    public void Push(IScreen screen) => _stack.Push(screen);

    public IScreen? Pop() => _stack.Count > 0 ? _stack.Pop() : null;

    public IScreen? Peek() => _stack.Count > 0 ? _stack.Peek() : null;

    public int Count => _stack.Count;

    public void Clear() => _stack.Clear();
}
