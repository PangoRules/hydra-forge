using HydraForge.Tui.Models;

namespace HydraForge.Tui.Tests;

public class AppStateTests
{
    [Fact]
    public void IncrementUnreadNotifications_AtomicallyIncrements()
    {
        var state = new AppState();

        Assert.Equal(0, state.UnreadNotifications);

        var first = state.IncrementUnreadNotifications();
        Assert.Equal(1, first);
        Assert.Equal(1, state.UnreadNotifications);

        var second = state.IncrementUnreadNotifications();
        Assert.Equal(2, second);
        Assert.Equal(2, state.UnreadNotifications);
    }

    [Fact]
    public void UnreadNotifications_VolatileReadWrite()
    {
        var state = new AppState();
        state.UnreadNotifications = 99;

        Assert.Equal(99, state.UnreadNotifications);
    }

    [Fact]
    public async Task IncrementUnreadNotifications_ConcurrentSafe()
    {
        var state = new AppState();
        var tasks = new Task<int>[10];
        for (var i = 0; i < 10; i++)
        {
            tasks[i] = Task.Run(() => state.IncrementUnreadNotifications());
        }

        await Task.WhenAll(tasks);

        Assert.Equal(10, state.UnreadNotifications);
    }
}
