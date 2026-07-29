using HydraForge.Tui.Generated;
using HydraForge.Tui.Models;
using HydraForge.Tui.Rendering;
using Spectre.Console;

namespace HydraForge.Tui.Services;

// Unread count + notification list, shared between every screen that shows the status bar
// (ProjectListScreen, BoardScreen) so both stay in sync off the same AppState.UnreadNotifications
// instead of each screen keeping its own local mirror.
public class NotificationCenter(
    ApiClientFactory apiClientFactory,
    AppState appState,
    ErrorCollector errorCollector
)
{
    private HydraForgeApiClient Client => apiClientFactory.GetClient();

    public async Task FetchUnreadCountAsync()
    {
        try
        {
            var response = await Client.UnreadCountAsync();
            appState.UnreadNotifications = response.Count;
        }
        catch (Exception ex)
        {
            // Non-fatal — status bar shows last known count
            errorCollector.Add("N/A", $"Unread count fetch failed: {ex.Message}");
        }
    }

    public async Task ShowNotificationsAsync(Func<Task> renderBackdrop)
    {
        try
        {
            var notifications = (await Client.NotificationsAsync(skip: 0, take: 50)).ToList();
            if (notifications.Count == 0)
            {
                AnsiConsole.MarkupLine("[grey]No notifications.[/]");
                Console.ReadKey(true);
                await renderBackdrop();
                return;
            }

            var choices = notifications
                .Select(n =>
                {
                    var prefix = n.IsRead ? "  " : "● ";
                    var time = n.CreatedAt.ToString("MMM dd HH:mm");
                    return $"{prefix}[bold]{Markup.Escape(n.Title)}[/] [grey]{time}[/]";
                })
                .ToList();

            var idx = await ListPrompt.ShowMarkup(
                "Notifications",
                choices,
                renderBackdrop: renderBackdrop
            );
            if (idx.HasValue && idx.Value >= 0 && idx.Value < notifications.Count)
            {
                var notif = notifications[idx.Value];
                if (!notif.IsRead)
                {
                    await Client.ReadAsync(notif.Id);
                    // Re-fetch authoritative count after mark-as-read instead of local decrement
                    await FetchUnreadCountAsync();
                }
            }
            await renderBackdrop();
        }
        catch (Exception ex)
        {
            errorCollector.Add("N/A", $"Notifications error: {ex.Message}");
            await renderBackdrop();
        }
    }
}
