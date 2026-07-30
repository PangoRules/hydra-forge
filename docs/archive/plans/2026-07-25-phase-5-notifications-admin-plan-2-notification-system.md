# Plan 2: Notification System (Domain + Application + Infrastructure)

**Branch:** `task/notification-system`
**Parent branch:** `feat/phase-5-notifications-admin`
**Parent spec:** `2026-07-25-phase-5-notifications-admin-design.md` — Task 2

## Task

Add `Notification.Create()` factory + `MarkRead()` instance method to the Domain entity. Create `INotificationRepository` port, `INotificationService`/`NotifyRequest` with actor-exclusion no-op, `EfNotificationRepository`, and DI registration. No UI or SignalR — just the persistence + service layer.

## Files to create

- `src/HydraForge.Application/Notifications/INotificationRepository.cs`
- `src/HydraForge.Application/Notifications/INotificationService.cs`
- `src/HydraForge.Application/Notifications/NotifyRequest.cs`
- `src/HydraForge.Application/Notifications/NotificationService.cs`
- `src/HydraForge.Infrastructure/Notifications/EfNotificationRepository.cs`
- `src/HydraForge.Infrastructure/Notifications/NotificationServiceCollectionExtensions.cs`
- `tests/HydraForge.Domain.Tests/Entities/NotificationTests.cs`
- `tests/HydraForge.Application.Tests/Notifications/NotificationServiceTests.cs`

## Files to modify

- `src/HydraForge.Domain/Entities/PersonalSpace/Notification.cs` — add `Create()` factory + `MarkRead()` instance method
- `src/HydraForge.Server/Program.cs` — register notification services

## Implementation steps

### Step 1: Add Domain entity methods

In `src/HydraForge.Domain/Entities/PersonalSpace/Notification.cs`, replace the property-bag class with:

```csharp
namespace HydraForge.Domain.Entities.PersonalSpace;

public class Notification
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? Body { get; private set; }
    public string Message { get; private set; } = string.Empty;
    public Guid? CardId { get; private set; }
    public Guid? ProjectId { get; private set; }
    public string? ActionUrl { get; private set; }
    public bool IsRead { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private Notification() { }

    public static Notification Create(
        Guid userId,
        string title,
        string? body,
        string message,
        Guid? cardId,
        Guid? projectId,
        string? actionUrl)
    {
        return new Notification
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = title,
            Body = body,
            Message = message,
            CardId = cardId,
            ProjectId = projectId,
            ActionUrl = actionUrl,
            IsRead = false,
            CreatedAt = DateTime.UtcNow,
        };
    }

    public void MarkRead()
    {
        IsRead = true;
    }
}
```

### Step 2: Write Domain unit tests

Create `tests/HydraForge.Domain.Tests/Entities/NotificationTests.cs`:

```csharp
using HydraForge.Domain.Entities.PersonalSpace;
using Xunit;

namespace HydraForge.Domain.Tests.Entities;

public class NotificationTests
{
    [Fact]
    public void Create_SetsAllProperties()
    {
        var userId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var projectId = Guid.NewGuid();

        var notif = Notification.Create(
            userId, "Test Title", "Test Body", "Test Message",
            cardId, projectId, "/projects/123");

        Assert.NotEqual(Guid.Empty, notif.Id);
        Assert.Equal(userId, notif.UserId);
        Assert.Equal("Test Title", notif.Title);
        Assert.Equal("Test Body", notif.Body);
        Assert.Equal("Test Message", notif.Message);
        Assert.Equal(cardId, notif.CardId);
        Assert.Equal(projectId, notif.ProjectId);
        Assert.Equal("/projects/123", notif.ActionUrl);
        Assert.False(notif.IsRead);
        Assert.True(notif.CreatedAt > DateTime.UtcNow.AddMinutes(-1));
    }

    [Fact]
    public void MarkRead_SetsIsReadToTrue()
    {
        var notif = Notification.Create(
            Guid.NewGuid(), "Title", null, "Message", null, null, null);

        notif.MarkRead();

        Assert.True(notif.IsRead);
    }

    [Fact]
    public void Create_WithNullOptionals_Works()
    {
        var notif = Notification.Create(
            Guid.NewGuid(), "Title", null, "Message", null, null, null);

        Assert.Null(notif.Body);
        Assert.Null(notif.CardId);
        Assert.Null(notif.ProjectId);
        Assert.Null(notif.ActionUrl);
    }
}
```

Run: `dotnet test tests/HydraForge.Domain.Tests/ --filter "FullyQualifiedName~NotificationTests"` — 3 tests pass.

### Step 3: Create Application ports

Create `src/HydraForge.Application/Notifications/INotificationRepository.cs`:

```csharp
using HydraForge.Domain.Entities.PersonalSpace;

namespace HydraForge.Application.Notifications;

public interface INotificationRepository
{
    Task AddAsync(Notification notification, CancellationToken ct = default);
    Task AddRangeAsync(IReadOnlyList<Notification> notifications, CancellationToken ct = default);
    Task<IReadOnlyList<Notification>> ListByUserAsync(Guid userId, int skip, int take, bool? unreadOnly = null, CancellationToken ct = default);
    Task<int> CountUnreadAsync(Guid userId, CancellationToken ct = default);
    Task MarkAsReadAsync(Guid notificationId, Guid userId, CancellationToken ct = default);
    Task MarkAllAsReadAsync(Guid userId, CancellationToken ct = default);
}
```

Create `src/HydraForge.Application/Notifications/NotifyRequest.cs`:

```csharp
namespace HydraForge.Application.Notifications;

public record NotifyRequest(
    Guid UserId,
    Guid ActorId,
    string Title,
    string? Body,
    string? Message,
    Guid? CardId,
    Guid? ProjectId,
    string? ActionUrl
);
```

Create `src/HydraForge.Application/Notifications/INotificationService.cs`:

```csharp
namespace HydraForge.Application.Notifications;

public interface INotificationService
{
    Task NotifyAsync(NotifyRequest request, CancellationToken ct = default);
}
```

### Step 4: Create NotificationService implementation

Create `src/HydraForge.Application/Notifications/NotificationService.cs`:

```csharp
using HydraForge.Domain.Entities.PersonalSpace;

namespace HydraForge.Application.Notifications;

public class NotificationService : INotificationService
{
    private readonly INotificationRepository _notifRepo;

    public NotificationService(INotificationRepository notifRepo)
    {
        _notifRepo = notifRepo;
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
    }
}
```

Note: `INtfyClient` and `INotificationHubBus` are injected in Task 3 and Task 6. For now, `NotificationService` only takes `INotificationRepository`. The constructor will be extended in later tasks.

### Step 5: Write Application unit tests

Create `tests/HydraForge.Application.Tests/Notifications/NotificationServiceTests.cs`:

```csharp
using HydraForge.Application.Notifications;
using HydraForge.Domain.Entities.PersonalSpace;
using Xunit;

namespace HydraForge.Application.Tests.Notifications;

public class NotificationServiceTests
{
    private class FakeNotificationRepository : INotificationRepository
    {
        public List<Notification> Added { get; } = [];

        public Task AddAsync(Notification notification, CancellationToken ct = default)
        {
            Added.Add(notification);
            return Task.CompletedTask;
        }

        public Task AddRangeAsync(IReadOnlyList<Notification> notifications, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task<IReadOnlyList<Notification>> ListByUserAsync(Guid userId, int skip, int take, bool? unreadOnly = null, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task<int> CountUnreadAsync(Guid userId, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task MarkAsReadAsync(Guid notificationId, Guid userId, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task MarkAllAsReadAsync(Guid userId, CancellationToken ct = default)
            => throw new NotImplementedException();
    }

    [Fact]
    public async Task NotifyAsync_WhenUserIdEqualsActorId_DoesNotAddNotification()
    {
        var repo = new FakeNotificationRepository();
        var service = new NotificationService(repo);
        var userId = Guid.NewGuid();

        await service.NotifyAsync(new NotifyRequest(userId, userId, "Title", null, null, null, null, null));

        Assert.Empty(repo.Added);
    }

    [Fact]
    public async Task NotifyAsync_WhenDifferentUser_AddsNotification()
    {
        var repo = new FakeNotificationRepository();
        var service = new NotificationService(repo);
        var userId = Guid.NewGuid();
        var actorId = Guid.NewGuid();

        await service.NotifyAsync(new NotifyRequest(userId, actorId, "Test Title", "Body", "Message", null, null, null));

        Assert.Single(repo.Added);
        var notif = repo.Added[0];
        Assert.Equal(userId, notif.UserId);
        Assert.Equal("Test Title", notif.Title);
        Assert.Equal("Body", notif.Body);
        Assert.Equal("Message", notif.Message);
        Assert.False(notif.IsRead);
    }

    [Fact]
    public async Task NotifyAsync_WhenMessageIsNull_UsesTitleAsMessage()
    {
        var repo = new FakeNotificationRepository();
        var service = new NotificationService(repo);

        await service.NotifyAsync(new NotifyRequest(Guid.NewGuid(), Guid.NewGuid(), "Title", null, null, null, null, null));

        Assert.Equal("Title", repo.Added[0].Message);
    }
}
```

Run: `dotnet test tests/HydraForge.Application.Tests/ --filter "FullyQualifiedName~NotificationServiceTests"` — 3 tests pass.

### Step 6: Create EfNotificationRepository

Create `src/HydraForge.Infrastructure/Notifications/EfNotificationRepository.cs`:

```csharp
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
```

### Step 7: Create DI extension method

Create `src/HydraForge.Infrastructure/Notifications/NotificationServiceCollectionExtensions.cs`:

```csharp
using HydraForge.Application.Notifications;
using Microsoft.Extensions.DependencyInjection;

namespace HydraForge.Infrastructure.Notifications;

public static class NotificationServiceCollectionExtensions
{
    public static IServiceCollection AddNotificationServices(this IServiceCollection services)
    {
        services.AddScoped<INotificationRepository, EfNotificationRepository>();
        services.AddScoped<INotificationService, NotificationService>();
        return services;
    }
}
```

### Step 8: Register in Program.cs

In `src/HydraForge.Server/Program.cs`, add `using HydraForge.Infrastructure.Notifications;` and add after the existing service registrations:

```csharp
builder.Services.AddNotificationServices();
```

### Step 9: Verify

```bash
dotnet build
dotnet test tests/HydraForge.Domain.Tests/ --filter "FullyQualifiedName~NotificationTests"
dotnet test tests/HydraForge.Application.Tests/ --filter "FullyQualifiedName~NotificationServiceTests"
dotnet test
```

## Verification

- `dotnet build` — no errors
- Domain tests: `Notification.Create()` sets all props, `MarkRead()` works, null optionals OK
- Application tests: actor-exclusion no-ops, different-user adds, null message falls back to title
- `dotnet test` — all existing tests still pass

## Dependencies

None — can start immediately. Task 1 (JWT fix) is independent.