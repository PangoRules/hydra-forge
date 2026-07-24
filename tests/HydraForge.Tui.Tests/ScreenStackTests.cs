using HydraForge.Tui.Models;
using HydraForge.Tui.Screens;

namespace HydraForge.Tui.Tests;

public class ScreenStackTests
{
    private sealed class TestScreen : IScreen
    {
        public Task RenderAsync() => Task.CompletedTask;
        public Task HandleKeyAsync(ConsoleKeyInfo key) => Task.CompletedTask;
        public Task OnEnterAsync() => Task.CompletedTask;
        public Task OnExitAsync() => Task.CompletedTask;
    }

    private static TestScreen CreateScreen() => new();

    [Fact]
    public void Push_AddsScreenToStack()
    {
        var stack = new ScreenStack();
        var screen = CreateScreen();

        stack.Push(screen);

        Assert.Equal(1, stack.Count);
        Assert.Same(screen, stack.Peek());
    }

    [Fact]
    public void Pop_RemovesAndReturnsTopScreen()
    {
        var stack = new ScreenStack();
        var screen1 = CreateScreen();
        var screen2 = CreateScreen();
        stack.Push(screen1);
        stack.Push(screen2);

        var popped = stack.Pop();

        Assert.Same(screen2, popped);
        Assert.Same(screen1, stack.Peek());
        Assert.Equal(1, stack.Count);
    }

    [Fact]
    public void Pop_FromEmptyStack_ReturnsNull()
    {
        var stack = new ScreenStack();

        var popped = stack.Pop();

        Assert.Null(popped);
        Assert.Equal(0, stack.Count);
    }

    [Fact]
    public void Peek_ReturnsTopScreenWithoutRemoving()
    {
        var stack = new ScreenStack();
        var screen1 = CreateScreen();
        var screen2 = CreateScreen();
        stack.Push(screen1);
        stack.Push(screen2);

        var peeked = stack.Peek();

        Assert.Same(screen2, peeked);
        Assert.Same(screen2, stack.Peek()); // Still on top
        Assert.Equal(2, stack.Count);
    }

    [Fact]
    public void Peek_FromEmptyStack_ReturnsNull()
    {
        var stack = new ScreenStack();

        var peeked = stack.Peek();

        Assert.Null(peeked);
        Assert.Equal(0, stack.Count);
    }

    [Fact]
    public void Clear_EmptiesStack()
    {
        var stack = new ScreenStack();
        stack.Push(CreateScreen());
        stack.Push(CreateScreen());

        stack.Clear();

        Assert.Equal(0, stack.Count);
        Assert.Null(stack.Pop());
        Assert.Null(stack.Peek());
    }
}
