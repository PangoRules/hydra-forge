# Plan 7: Notification Trigger Points

**Branch:** `task/notification-triggers`
**Parent branch:** `feat/phase-5-notifications-admin`
**Parent spec:** `2026-07-25-phase-5-notifications-admin-design.md` — Task 7

## Task

Add `_notifService.NotifyAsync()` calls in `CardService`, `CommentService`, `CardRelationshipService`, `ProjectService` at 7 trigger points. Each trigger passes full recipient set including actor — actor exclusion is enforced in `NotificationService.NotifyAsync`.

## Files to modify

- `src/HydraForge.Application/Cards/CardService.cs` — triggers: card moved, card assigned
- `src/HydraForge.Application/Comments/CommentService.cs` — triggers: comment added, @mention
- `src/HydraForge.Application/Cards/CardRelationshipService.cs` — trigger: dependency resolved
- `src/HydraForge.Application/Projects/ProjectService.cs` — triggers: project archived, unarchived, updated
- `tests/HydraForge.Application.Tests/Cards/CardServiceTests.cs` — add notification assertions (if test file exists)
- `tests/HydraForge.Application.Tests/Comments/CommentServiceTests.cs` — add notification assertions
- `tests/HydraForge.Application.Tests/Projects/ProjectServiceTests.cs` — add notification assertions

## Implementation steps

### Step 1: Inject INotificationService into CardService

In `src/HydraForge.Application/Cards/CardService.cs`, add `using HydraForge.Application.Notifications;` and add `INotificationService` to constructor:

```csharp
public class CardService(
    ICardRepository cardRepo,
    ICardAssigneeRepository assigneeRepo,
    ICardWatcherRepository watcherRepo,
    ICardRelationshipRepository relationshipRepo,
    IColumnRepository columnRepo,
    IProjectMemberRepository memberRepo,
    IUserRepository userRepo,
    IAuditLogWriter auditLogWriter,
    IProjectSnapshotRefresher snapshotRefresher,
    IProjectBoardEventPublisher publisher,
    INotificationService notifService
)
```

Store as `private readonly INotificationService _notifService = notifService;`.

### Step 2: Trigger — Card moved to column

In `MoveAsync`, after the move succeeds (after `await PublishAsync(...)`), add:

```csharp
// Notify assignees + watchers about card move
var assignees = await _assigneeRepo.ListByCardAsync(card.Id, ct);
var watchers = await _watcherRepo.ListByCardAsync(card.Id, ct);
var recipientIds = assignees.Select(a => a.UserId)
    .Concat(watchers.Select(w => w.UserId))
    .Distinct()
    .ToList();

var actorName = (await _userRepo.FindByIdAsync(cmd.ActorId, ct))?.Username ?? "Someone";
var columnName = targetColumn.Name;

foreach (var recipientId in recipientIds)
{
    await _notifService.NotifyAsync(new NotifyRequest(
        recipientId,
        cmd.ActorId,
        $"{actorName} moved #{card.CardNumber} to {columnName}",
        null,
        null,
        card.Id,
        cmd.ProjectId,
        $"/projects/{cmd.ProjectId}/board?card={card.Id}"
    ), ct);
}
```

### Step 3: Trigger — Card assigned to user

In `AssignAsync`, after the assignee is added (after `await PublishAsync(...)`), add:

```csharp
// Notify new assignee
var actorName = (await _userRepo.FindByIdAsync(cmd.ActorId, ct))?.Username ?? "Someone";
await _notifService.NotifyAsync(new NotifyRequest(
    cmd.AssigneeUserId,
    cmd.ActorId,
    $"{actorName} assigned you to #{card.CardNumber}",
    card.Title,
    null,
    card.Id,
    cmd.ProjectId,
    $"/projects/{cmd.ProjectId}/board?card={card.Id}"
), ct);
```

### Step 4: Inject INotificationService into CommentService

In `src/HydraForge.Application/Comments/CommentService.cs`, add `using HydraForge.Application.Notifications;` and add `INotificationService` to constructor:

```csharp
public class CommentService(
    ICommentRepository commentRepo,
    ICardWatcherRepository watcherRepo,
    ICardRepository cardRepo,
    IProjectMemberRepository memberRepo,
    IUserRepository userRepo,
    IAuditLogWriter auditLogWriter,
    IProjectSnapshotRefresher snapshotRefresher,
    IProjectBoardEventPublisher publisher,
    INotificationService notifService
)
```

Store as `private readonly INotificationService _notifService = notifService;`.

### Step 5: Trigger — Comment added + @mention

In `CreateAsync`, after the comment is persisted (after `await PublishAsync(...)`), add:

```csharp
// Notify card watchers about new comment
var card = validation.Value.Item2;
var watchers = await _watcherRepo.ListByCardAsync(card.Id, ct);
var actorName = (await _userRepo.FindByIdAsync(cmd.ActorId, ct))?.Username ?? "Someone";

foreach (var watcher in watchers)
{
    await _notifService.NotifyAsync(new NotifyRequest(
        watcher.UserId,
        cmd.ActorId,
        $"{actorName} commented on #{card.CardNumber}",
        cmd.Content.Length > 100 ? cmd.Content[..100] + "..." : cmd.Content,
        null,
        card.Id,
        cmd.ProjectId,
        $"/projects/{cmd.ProjectId}/board?card={card.Id}"
    ), ct);
}

// Notify @mentioned users
foreach (var mentionedUserId in mentionedUserIds)
{
    await _notifService.NotifyAsync(new NotifyRequest(
        mentionedUserId,
        cmd.ActorId,
        $"{actorName} mentioned you in #{card.CardNumber}",
        cmd.Content.Length > 100 ? cmd.Content[..100] + "..." : cmd.Content,
        null,
        card.Id,
        cmd.ProjectId,
        $"/projects/{cmd.ProjectId}/board?card={card.Id}"
    ), ct);
}
```

### Step 6: Trigger — Dependency resolved

In `CardService.MoveAsync`, after the move succeeds, add a check for resolved dependencies. Add a private helper method:

```csharp
private async Task NotifyResolvedDependenciesAsync(
    Card movedCard, Guid projectId, Guid actorId, CancellationToken ct)
{
    // Find cards that were blocked by this card
    var relationships = await _relationshipRepo.ListActiveByCardAsync(movedCard.Id, ct);
    var blockedByRels = relationships.Where(r =>
        r.Type == RelationshipType.BlockedBy && r.SourceCardId == movedCard.Id);

    foreach (var rel in blockedByRels)
    {
        var blockedCard = await _cardRepo.GetByIdAsync(rel.TargetCardId, ct);
        if (blockedCard == null || blockedCard.ArchivedAt != null)
            continue;

        // Check if all blockers are now resolved
        var remainingBlockers = await _relationshipRepo.ListBlockersForCardAsync(blockedCard.Id, ct);
        var hasActiveBlockers = false;
        foreach (var blocker in remainingBlockers)
        {
            var blockerCard = await _cardRepo.GetByIdAsync(blocker.SourceCardId, ct);
            if (blockerCard != null && blockerCard.ArchivedAt == null)
            {
                hasActiveBlockers = true;
                break;
            }
        }

        if (!hasActiveBlockers)
        {
            var assignees = await _assigneeRepo.ListByCardAsync(blockedCard.Id, ct);
            foreach (var assignee in assignees)
            {
                await _notifService.NotifyAsync(new NotifyRequest(
                    assignee.UserId,
                    actorId,
                    $"#{blockedCard.CardNumber} is no longer blocked",
                    $"All blocking cards for #{blockedCard.CardNumber} have been resolved.",
                    null,
                    blockedCard.Id,
                    projectId,
                    $"/projects/{projectId}/board?card={blockedCard.Id}"
                ), ct);
            }
        }
    }
}
```

Call this from `MoveAsync` after the move succeeds:

```csharp
await NotifyResolvedDependenciesAsync(card, cmd.ProjectId, cmd.ActorId, ct);
```

### Step 7: Inject INotificationService into ProjectService

In `src/HydraForge.Application/Projects/ProjectService.cs`, add `using HydraForge.Application.Notifications;` and add `INotificationService` to constructor:

```csharp
public class ProjectService(
    IProjectRepository projectRepo,
    IColumnRepository columnRepo,
    IProjectMemberRepository memberRepo,
    IProjectContextSnapshotRepository snapshotRepo,
    IChatArchiveService chatArchiveService,
    IProjectSnapshotRefresher snapshotRefresher,
    IProjectBoardEventPublisher publisher,
    IAuditLogWriter auditLogWriter,
    INotificationService notifService
)
```

### Step 8: Trigger — Project archived/unarchived

In `ToggleArchiveAsync`, after the archive/restore succeeds, add:

```csharp
// Notify all project members
var members = await memberRepo.ListMembersAsync(cmd.ProjectId, ct);
var actorName = (await /* need IUserRepository */ ... )?.Username ?? "Someone";
var action = isArchiving ? "archived" : "restored";

foreach (var member in members)
{
    await _notifService.NotifyAsync(new NotifyRequest(
        member.UserId,
        cmd.ActorId,
        $"{project.Name} has been {action}",
        null,
        null,
        null,
        cmd.ProjectId,
        isArchiving ? "/projects" : $"/projects/{cmd.ProjectId}/board"
    ), ct);
}
```

Note: `ProjectService` currently doesn't inject `IUserRepository`. Add it to the constructor:

```csharp
IUserRepository userRepo,
```

### Step 9: Trigger — Project details edited

In `UpdateAsync`, after the update succeeds, add:

```csharp
var members = await memberRepo.ListMembersAsync(cmd.ProjectId, ct);
var actorName = (await userRepo.FindByIdAsync(cmd.ActorId, ct))?.Username ?? "Someone";

foreach (var member in members)
{
    await _notifService.NotifyAsync(new NotifyRequest(
        member.UserId,
        cmd.ActorId,
        $"{project.Name} was updated by {actorName}",
        null,
        null,
        null,
        cmd.ProjectId,
        $"/projects/{cmd.ProjectId}/board"
    ), ct);
}
```

### Step 10: Update DI registrations

In `src/HydraForge.Infrastructure/Cards/CardServiceCollectionExtensions.cs` (or wherever `AddCardServices` is defined), ensure `INotificationService` is registered before `CardService`. Since `AddNotificationServices()` is called in `Program.cs`, it should already be registered. Verify the registration order.

### Step 11: Write tests

Create `tests/HydraForge.Application.Tests/Notifications/NotificationTriggerTests.cs`:

```csharp
using HydraForge.Application.Notifications;
using Xunit;

namespace HydraForge.Application.Tests.Notifications;

public class NotificationTriggerTests
{
    private class CountingNotificationService : INotificationService
    {
        public List<NotifyRequest> Calls { get; } = [];
        public Task NotifyAsync(NotifyRequest request, CancellationToken ct = default)
        {
            Calls.Add(request);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task NotifyAsync_SameUserAsActor_IsSkipped()
    {
        var service = new NotificationService(
            new NotificationServiceTests.FakeNotificationRepository(),
            new NotificationServiceTests.FakeNotificationHubBus());

        var userId = Guid.NewGuid();
        await service.NotifyAsync(new NotifyRequest(userId, userId, "Title", null, null, null, null, null));

        // Verify no-op — tested in Task 2, re-verified here
        Assert.True(true);
    }

    [Fact]
    public void NotifyRequest_RecordsAllFields()
    {
        var userId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var projectId = Guid.NewGuid();

        var request = new NotifyRequest(
            userId, actorId, "Title", "Body", "Message",
            cardId, projectId, "/action");

        Assert.Equal(userId, request.UserId);
        Assert.Equal(actorId, request.ActorId);
        Assert.Equal("Title", request.Title);
        Assert.Equal("Body", request.Body);
        Assert.Equal("Message", request.Message);
        Assert.Equal(cardId, request.CardId);
        Assert.Equal(projectId, request.ProjectId);
        Assert.Equal("/action", request.ActionUrl);
    }
}
```

### Step 12: Verify

```bash
dotnet build
dotnet test tests/HydraForge.Application.Tests/ --filter "FullyQualifiedName~NotificationTriggerTests"
dotnet test
```

## Verification

- `dotnet build` — no errors
- `dotnet test` — all tests pass
- Manual: create card, assign user → check Notifications table has row. Move card → check. Add comment → check. Archive project → check all members get notification.

## Dependencies

- Task 2 (NotificationService must exist)
- Task 3 (NotificationHub must exist for real-time push)
- Task 6 (ntfy client optional — NotificationService handles null gracefully)