using HydraForge.Application.Auth;
using HydraForge.Application.Notifications;
using HydraForge.Server.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HydraForge.Server.Controllers;

[Authorize(Policy = AuthPolicies.UserIdRequired)]
[ApiController]
[Route("api/[controller]")]
public class NotificationsController(
    INotificationRepository notifRepo
) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] int skip = 0,
        [FromQuery] int take = 20,
        CancellationToken ct = default)
    {
        var userId = User.GetRequiredUserId();
        var notifications = await notifRepo.ListByUserAsync(userId, skip, take, ct: ct);
        var response = notifications.Select(n => new NotificationResponse(
            n.Id,
            n.Title,
            n.Body,
            n.CardId,
            n.ProjectId,
            n.ActionUrl,
            n.IsRead,
            n.CreatedAt
        )).ToList();
        return Ok(response);
    }

    [HttpGet("unread-count")]
    public async Task<IActionResult> UnreadCount(CancellationToken ct)
    {
        var userId = User.GetRequiredUserId();
        var count = await notifRepo.CountUnreadAsync(userId, ct);
        return Ok(new { count });
    }

    [HttpPost("{notificationId:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid notificationId, CancellationToken ct)
    {
        var userId = User.GetRequiredUserId();
        await notifRepo.MarkAsReadAsync(notificationId, userId, ct);
        return NoContent();
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct)
    {
        var userId = User.GetRequiredUserId();
        await notifRepo.MarkAllAsReadAsync(userId, ct);
        return NoContent();
    }
}

public record NotificationResponse(
    Guid Id,
    string Title,
    string? Body,
    Guid? CardId,
    Guid? ProjectId,
    string? ActionUrl,
    bool IsRead,
    DateTime CreatedAt
);
