using HydraForge.Application.Notifications;
using HydraForge.Domain.Entities.PersonalSpace;
using HydraForge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HydraForge.Infrastructure.Notifications;

public class EfNotificationRepository(HydraForgeDbContext db) : INotificationRepository
{
    public async Task AddAsync(Notification notification, CancellationToken ct = default)
    {
        db.Notifications.Add(notification);
        await db.SaveChangesAsync(ct);
    }

    public async Task AddRangeAsync(IReadOnlyList<Notification> notifications, CancellationToken ct = default)
    {
        db.Notifications.AddRange(notifications);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<Notification>> ListByUserAsync(
        Guid userId, int skip, int take, bool? unreadOnly = null, CancellationToken ct = default)
    {
        var query = db.Notifications.Where(n => n.UserId == userId);

        if (unreadOnly == true)
            query = query.Where(n => !n.IsRead);

        return await query
            .OrderByDescending(n => n.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);
    }

    public async Task<int> CountUnreadAsync(Guid userId, CancellationToken ct = default)
    {
        return await db.Notifications
            .CountAsync(n => n.UserId == userId && !n.IsRead, ct);
    }

    public async Task MarkAsReadAsync(Guid notificationId, Guid userId, CancellationToken ct = default)
    {
        var notif = await db.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId, ct);

        if (notif != null)
        {
            notif.MarkRead();
            await db.SaveChangesAsync(ct);
        }
    }

    public async Task MarkAllAsReadAsync(Guid userId, CancellationToken ct = default)
    {
        var unread = await db.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ToListAsync(ct);

        foreach (var n in unread)
            n.MarkRead();

        if (unread.Count > 0)
            await db.SaveChangesAsync(ct);
    }
}
