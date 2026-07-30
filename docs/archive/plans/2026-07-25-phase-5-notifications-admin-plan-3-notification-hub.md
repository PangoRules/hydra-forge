# Plan 3: NotificationHub + SignalR Push

**Branch:** `task/notification-hub`
**Parent branch:** `feat/phase-5-notifications-admin`
**Parent spec:** `2026-07-25-phase-5-notifications-admin-design.md` — Task 3

## Task

Create `NotificationHub` (SignalR), register in `Program.cs`, implement `INotificationHubBus` port + `SignalRNotificationHubBus` in Infrastructure. Wire into `NotificationService`.

## Files to create

- `src/HydraForge.Server/Hubs/NotificationHub.cs`
- `src/HydraForge.Application/Notifications/INotificationHubBus.cs`
- `src/HydraForge.Infrastructure/Realtime/SignalRNotificationHubBus.cs`

## Files to modify

- `src/HydraForge.Server/Program.cs` — map hub, add `/hubs/notifications` to JWT query-string token extraction
- `src/HydraForge.Infrastructure/Realtime/RealtimeServiceCollectionExtensions.cs` — register `INotificationHubBus`
- `src/HydraForge.Application/Notifications/NotificationService.cs` — inject `INotificationHubBus`, call `SendNotificationAsync` after DB write

## Implementation steps

### Step 1: Create `INotificationHubBus` port

Create `src/HydraForge.Application/Notifications/INotificationHubBus.cs`:

```csharp
using HydraForge.Domain.Entities.PersonalSpace;

namespace HydraForge.Application.Notifications;

public interface INotificationHubBus
{
    Task SendNotificationAsync(Guid userId, Notification notification, CancellationToken ct = default);
}
```

### Step 2: Create `NotificationHub`

Create `src/HydraForge.Server/Hubs/NotificationHub.cs`:

```csharp
using HydraForge.Application.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace HydraForge.Server.Hubs;

[Authorize]
public class NotificationHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var userId = Context.User!.GetRequiredUserId();
        await Groups.AddToGroupAsync(Context.ConnectionId, $"user-{userId}");
        await base.OnConnectedAsync();
    }
}
```

### Step 3: Create `SignalRNotificationHubBus`

Create `src/HydraForge.Infrastructure/Realtime/SignalRNotificationHubBus.cs`:

```csharp
using HydraForge.Application.Notifications;
using HydraForge.Domain.Entities.PersonalSpace;
using HydraForge.Server.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace HydraForge.Infrastructure.Realtime;

public class SignalRNotificationHubBus(IHubContext<NotificationHub> hubContext) : INotificationHubBus
{
    public async Task SendNotificationAsync(Guid userId, Notification notification, CancellationToken ct)
    {
        await hubContext.Clients.Group($"user-{userId}").SendAsync(
            "NotificationReceived",
            new
            {
                notification.Id,
                notification.Title,
                notification.Body,
                notification.CardId,
                notification.ProjectId,
                notification.ActionUrl,
                notification.CreatedAt,
                notification.IsRead,
            },
            ct
        );
    }
}
```

### Step 4: Register in DI

In `src/HydraForge.Infrastructure/Realtime/RealtimeServiceCollectionExtensions.cs`, add:

```csharp
using HydraForge.Application.Notifications;

// Inside AddRealtimeServices:
services.AddScoped<INotificationHubBus, SignalRNotificationHubBus>();
```

### Step 5: Map hub in Program.cs

In `src/HydraForge.Server/Program.cs`:

Add hub mapping after existing hubs:
```csharp
app.MapHub<NotificationHub>("/hubs/notifications");
```

Add `/hubs/notifications` to the JWT query-string token extraction in the `OnMessageReceived` event handler (line 102):
```csharp
if (!string.IsNullOrEmpty(accessToken) &&
    (path.StartsWithSegments("/hubs/board") || path.StartsWithSegments("/hubs/presence") || path.StartsWithSegments("/hubs/notifications")))
{
    context.Token = accessToken;
}
```

### Step 6: Wire into NotificationService

Update `src/HydraForge.Application/Notifications/NotificationService.cs` constructor to accept `INotificationHubBus`:

```csharp
public class NotificationService : INotificationService
{
    private readonly INotificationRepository _notifRepo;
    private readonly INotificationHubBus _hubBus;

    public NotificationService(INotificationRepository notifRepo, INotificationHubBus hubBus)
    {
        _notifRepo = notifRepo;
        _hubBus = hubBus;
    }

    public async Task NotifyAsync(NotifyRequest request, CancellationToken ct = default)
    {
        if (request.UserId == request.ActorId)
            return;

        var notif = Notification.Create(
            request.UserId,
            request.Title,
            request.Body,
            request.Message ?? request.Title,
            request.CardId,
            request.ProjectId,
            request.ActionUrl);

        await _notifRepo.AddAsync(notif, ct);
        await _hubBus.SendNotificationAsync(request.UserId, notif, ct);
    }
}
```

### Step 7: Update Application tests

Update `tests/HydraForge.Application.Tests/Notifications/NotificationServiceTests.cs` — add a fake `INotificationHubBus`:

```csharp
private class FakeNotificationHubBus : INotificationHubBus
{
    public List<(Guid UserId, Notification Notification)> Sent { get; } = [];

    public Task SendNotificationAsync(Guid userId, Notification notification, CancellationToken ct = default)
    {
        Sent.Add((userId, notification));
        return Task.CompletedTask;
    }
}
```

Update the `FakeNotificationRepository` to implement all methods (already done in Task 2).

Update existing tests to pass the fake hub bus, and add a new test:

```csharp
[Fact]
public async Task NotifyAsync_WhenDifferentUser_SendsToHubBus()
{
    var repo = new FakeNotificationRepository();
    var hubBus = new FakeNotificationHubBus();
    var service = new NotificationService(repo, hubBus);
    var userId = Guid.NewGuid();
    var actorId = Guid.NewGuid();

    await service.NotifyAsync(new NotifyRequest(userId, actorId, "Title", null, null, null, null, null));

    Assert.Single(hubBus.Sent);
    Assert.Equal(userId, hubBus.Sent[0].UserId);
    Assert.Equal("Title", hubBus.Sent[0].Notification.Title);
}

[Fact]
public async Task NotifyAsync_WhenUserIdEqualsActorId_DoesNotSendToHubBus()
{
    var repo = new FakeNotificationRepository();
    var hubBus = new FakeNotificationHubBus();
    var service = new NotificationService(repo, hubBus);
    var userId = Guid.NewGuid();

    await service.NotifyAsync(new NotifyRequest(userId, userId, "Title", null, null, null, null, null));

    Assert.Empty(hubBus.Sent);
}
```

### Step 8: Verify

```bash
dotnet build
dotnet test tests/HydraForge.Application.Tests/ --filter "FullyQualifiedName~NotificationServiceTests"
dotnet test
```

## Verification

- `dotnet build` — no errors
- Application tests: hub bus receives notification on different-user, no-op on same-user
- `dotnet test` — all existing tests still pass
- Manual: start server, connect SignalR to `/hubs/notifications`, trigger a notification (via Task 7), verify `NotificationReceived` event arrives

## Dependencies

- Task 2 (NotificationService must exist to wire hub bus into it)