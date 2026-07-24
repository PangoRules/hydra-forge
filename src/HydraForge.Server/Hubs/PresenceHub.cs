using System.Collections.Concurrent;
using System.Security.Claims;
using HydraForge.Application.Auth;
using HydraForge.Application.Projects;
using HydraForge.Infrastructure.Realtime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace HydraForge.Server.Hubs;

[Authorize]
public class PresenceHub : Hub
{
    public static string ProjectGroup(Guid projectId) => $"project-{projectId}";

    private static readonly ConcurrentDictionary<Guid, ConcurrentDictionary<string, PresenceEntry>> _projectPresence = new();

    public record PresenceEntry(Guid UserId, string Username, string ConnectionId, DateTime JoinedAt);

    private readonly IProjectMemberRepository _memberRepo;

    public PresenceHub(IProjectMemberRepository memberRepo)
    {
        _memberRepo = memberRepo;
    }

    public async Task JoinProject(Guid projectId)
    {
        var userId = Context.User!.GetRequiredUserId();

        var isAdmin = Context.User!.IsInRole("Admin");
        if (!isAdmin)
        {
            var membership = await _memberRepo.GetByProjectAndUserAsync(projectId, userId);
            if (membership == null)
            {
                throw new HubException("Access denied");
            }
        }

        var groupName = BoardHub.ProjectGroup(projectId);
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);

        var username = Context.User!.FindFirstValue("name") ?? "unknown";

        var projectEntries = _projectPresence.GetOrAdd(projectId, _ => new ConcurrentDictionary<string, PresenceEntry>());
        projectEntries[Context.ConnectionId] = new PresenceEntry(userId, username, Context.ConnectionId, DateTime.UtcNow);

        // Send current user list to the new joiner so they know who's already here
        var currentUsers = projectEntries.Values
            .Where(e => e.ConnectionId != Context.ConnectionId)
            .Select(e => new { e.UserId, e.Username, e.ConnectionId })
            .ToList();

        await Clients.Caller.SendAsync("CurrentUsers", currentUsers);

        await Clients.OthersInGroup(groupName).SendAsync("UserJoined", new
        {
            UserId = userId,
            Username = username,
            ConnectionId = Context.ConnectionId,
        });
    }

    public async Task FocusCard(Guid projectId, Guid cardId)
    {
        var userId = Context.User!.GetRequiredUserId();
        var groupName = BoardHub.ProjectGroup(projectId);
        await Clients.OthersInGroup(groupName).SendAsync("CardFocused", new
        {
            UserId = userId,
            CardId = cardId,
            ConnectionId = Context.ConnectionId,
        });
    }

    public async Task UnfocusCard(Guid projectId)
    {
        var userId = Context.User!.GetRequiredUserId();
        var groupName = BoardHub.ProjectGroup(projectId);
        await Clients.OthersInGroup(groupName).SendAsync("CardUnfocused", new
        {
            UserId = userId,
            ConnectionId = Context.ConnectionId,
        });
    }

    public async Task LeaveProject(Guid projectId)
    {
        var groupName = BoardHub.ProjectGroup(projectId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);

        if (_projectPresence.TryGetValue(projectId, out var projectEntries))
        {
            projectEntries.TryRemove(Context.ConnectionId, out _);
        }

        var userId = Context.User!.GetRequiredUserId();
        var username = Context.User!.FindFirstValue("name") ?? "unknown";

        await Clients.OthersInGroup(groupName).SendAsync("UserLeft", new
        {
            UserId = userId,
            Username = username,
            ConnectionId = Context.ConnectionId,
        });
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        foreach (var (projectId, projectEntries) in _projectPresence)
        {
            if (projectEntries.TryRemove(Context.ConnectionId, out var entry))
            {
                var groupName = BoardHub.ProjectGroup(projectId);
                await Clients.OthersInGroup(groupName).SendAsync("UserLeft", new
                {
                    UserId = entry.UserId,
                    Username = entry.Username,
                    ConnectionId = Context.ConnectionId,
                });
            }
        }

        await base.OnDisconnectedAsync(exception);
    }
}
