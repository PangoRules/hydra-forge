namespace HydraForge.Tui.Models;

public static class DateFormatting
{
    // Minute-granular relative time for recent, high-frequency events (notifications).
    // Mirrors the Web UI's NotificationPanel.vue timeAgo() exactly, so both clients
    // read the same way.
    public static string FormatRecentRelative(DateTimeOffset dt)
    {
        var diff = DateTimeOffset.UtcNow - dt;
        if (diff.TotalMinutes < 1)
            return "just now";
        if (diff.TotalMinutes < 60)
            return $"{(int)diff.TotalMinutes}m ago";
        if (diff.TotalHours < 24)
            return $"{(int)diff.TotalHours}h ago";
        return $"{(int)diff.TotalDays}d ago";
    }

    // Years/months-down-to-hours relative time for long-lived data (project creation
    // dates) — a notification-scale minute counter would be noise here.
    public static string FormatLongRelative(DateTimeOffset dt)
    {
        var diff = DateTimeOffset.UtcNow - dt;
        if (diff.TotalDays > 365)
            return $"{(int)(diff.TotalDays / 365)}y ago";
        if (diff.TotalDays > 30)
            return $"{(int)(diff.TotalDays / 30)}mo ago";
        if (diff.TotalDays >= 1)
            return $"{(int)diff.TotalDays}d ago";
        if (diff.TotalHours >= 1)
            return $"{(int)diff.TotalHours}h ago";
        return "just now";
    }

    // Compact absolute timestamp for activity/version lists (comments, spec/plan
    // version history) — one format instead of each screen picking its own.
    public static string FormatTimestamp(DateTimeOffset dt) => dt.ToString("MMM dd HH:mm");
}
