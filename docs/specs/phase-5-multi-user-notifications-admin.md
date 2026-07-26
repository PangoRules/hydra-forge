# Phase 5: Multi-User Notifications & Admin Dashboard

**Branch:** `feat/phase-5-notifications-admin`

**Date:** 2026-07-25  
**Status:** Draft  
**Author:** @brainstorm

---

## Table of Contents

1. [Overview](#1-overview)
2. [BLOCKING BUG: JWT Role Claim](#2-blocking-bug-jwt-role-claim)
3. [Notification System Architecture](#3-notification-system-architecture)
4. [In-App Bell + Unread Count (Web UI)](#4-in-app-bell--unread-count-web-ui)
5. [Unread Count in TUI Status Bar](#5-unread-count-in-tui-status-bar)
6. [Notification Hub (SignalR)](#6-notification-hub-signalr)
7. [Admin Dashboard](#7-admin-dashboard)
8. [Admin User Management](#8-admin-user-management)
9. [Admin System Settings](#9-admin-system-settings)
10. [Admin All-Projects Bypass](#10-admin-all-projects-bypass)
11. [Audit Log Viewer](#11-audit-log-viewer)
12. [HousekeepingBackgroundService — Scope Decision](#12-housekeepingbackgroundservice--scope-decision)
13. [Dependency Notes](#13-dependency-notes)
14. [Implementation Order](#14-implementation-order)
15. [Tasks](#15-tasks)

---

## 1. Overview

Phase 5 adds two major capabilities:

- **Notifications**: push-based notification system with ntfy (self-hosted) fallback, per-user topic routing, in-app bell (Web UI) and status-bar unread count (TUI), and automatic notification rule triggers from existing service mutations.
- **Admin Dashboard**: full admin panel covering user management, system settings, project oversight (all-projects bypass), audit log browsing, and system health.

The phase also fixes one blocking bug (JWT role claim) that prevents all admin authorization work, and makes a scope decision on the deferred `HousekeepingBackgroundService`.

### Scope Boundaries

| In Scope | Out of Scope |
|---|---|
| Notification entity writes + reads | Email notifications |
| ntfy integration (push fallback) | SMS / Slack / Discord webhooks |
| In-app bell (Web UI) + unread count (TUI) | Push notification toasts |
| Admin dashboard (user mgmt, project oversight, system settings, audit log viewer, health) | Git/PR integration (PR event source flagged as dependency) |
| Admin all-projects bypass in membership checks | User roles beyond admin (no "moderator", "viewer" tiers) |
| 5-min settings cache TTL | Real-time settings refresh via SignalR |
| JWT role claim fix | JWT refresh overhaul |
| **SystemSettings**: NtfyServerUrl, SearXngUrl, BrandName, BrandLogoUrl fields | Per-project notification routing |
| **HousekeepingBackgroundService**: NOT in scope (see §12) | Notification preference per type (all deferred to post-MVP) |

---

## 2. BLOCKING BUG: JWT Role Claim

### Current State

`JwtTokenIssuer.cs` emits a custom claim `"is_admin": "true"|"false"` but never emits `ClaimTypes.Role`. Meanwhile `PresenceHub.cs:31` checks:

```csharp
var isAdmin = Context.User!.IsInRole("Admin");
```

`IsInRole("Admin")` looks for a `ClaimTypes.Role` claim with value `"Admin"`. Since no such claim exists, this check always returns `false` — even for the seeded admin user.

### Fix

Two options:

**Option A (Recommended): Add ClaimTypes.Role to JWT issuance.**  
Minimal change, preserves existing `IsInRole()` calls across the codebase.

```csharp
// In JwtTokenIssuer.IssueToken():
var claims = new[]
{
    new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
    new Claim(JwtRegisteredClaimNames.Name, user.Username),
    new Claim("is_admin", user.IsAdmin.ToString().ToLower()),
};

// Add role claim:
if (user.IsAdmin)
{
    claims = claims.Append(new Claim(ClaimTypes.Role, Roles.Admin)).ToArray();
}
```

`Roles.Admin` is the shared constant defined in §10.2 — used here, in `AuthPolicies.AdminRequired` (§7.2), and in `IsProjectMemberOrAdmin` (§10.2), so the role name exists in exactly one place.

This makes `Context.User!.IsInRole("Admin")` work everywhere — both in existing PresenceHub and in all new admin controllers added in Phase 5.

When `user.IsAdmin` is `false`, no role claim is emitted (null role, not "User" role). The only meaningful role at this stage is "Admin"; other roles would require a `MemberRole` → ASP.NET role mapping which doesn't exist yet and isn't needed.

**Option B: Change IsInRole to custom claim check.**  
Would require touching every admin authorization point. Option A is simpler and aligned with ASP.NET conventions.

**Decision: Option A — add `ClaimTypes.Role` emission in `JwtTokenIssuer`.**

### Token Refresh Consideration

Existing tokens issued before the fix won't have the role claim. The fix only applies to newly-issued tokens. Since JWT access tokens are short-lived (configurable, default 60 min), affected users simply re-login. No token-versioning mechanism or forced-reissue is needed.

### Effect on Auth Store (Web UI)

The auth store already decodes `is_admin` from the JWT payload:

```typescript
isAdmin: payload.is_admin === 'true'
```

No change needed on the Web UI side — the frontend determines admin status from the custom claim, not from `ClaimTypes.Role`. The fix is purely server-side for `IsInRole()` checks.

---

## 3. Notification System Architecture

### 3.1 Domain Entities

**`Notification`** already exists at `Domain/Entities/PersonalSpace/Notification.cs`:

| Field | Type | Notes |
|---|---|---|
| Id | Guid | PK |
| UserId | Guid | FK → User |
| Title | string | Short title |
| Body | string? | Optional body text |
| Message | string | Display message (kept for backward compat with existing schema — mapped in DbContext) |
| CardId | Guid? | Optional link to card |
| ProjectId | Guid? | Optional link to project |
| ActionUrl | string? | Deep-link URL |
| IsRead | bool | Read/unread |
| CreatedAt | DateTime | UTC |

No schema changes needed for the Notification entity.

**Domain entity pattern (non-negotiable, CLAUDE.md):** as read today, `Notification` is a plain property bag — public setters, no factory or instance methods. That's fine for a brand-new unused entity, but Phase 5 is what puts it into service, so bring it in line with the rest of the Domain layer before wiring it up:

- Add a static factory `Notification.Create(Guid userId, string title, string? body, string message, Guid? cardId, Guid? projectId, string? actionUrl)` — sets `Id`/`CreatedAt`/`IsRead = false` internally. `NotificationService.NotifyAsync` (§3.2) calls this instead of an object initializer.
- Add an instance method `MarkRead()` (`IsRead = true`) — `INotificationRepository.MarkAsReadAsync`/`MarkAllAsReadAsync` call this on the loaded entity rather than setting `IsRead` from the repository.

### 3.2 Application Layer

Create a new `Notifications` area in `HydraForge.Application`:

**`INotificationRepository`** (port):
- `Task AddAsync(Notification notif, CancellationToken ct = default)`
- `Task AddRangeAsync(IReadOnlyList<Notification> notifs, CancellationToken ct = default)`
- `Task<IReadOnlyList<Notification>> ListByUserAsync(Guid userId, int skip, int take, bool? unreadOnly = null, CancellationToken ct = default)`
- `Task<int> CountUnreadAsync(Guid userId, CancellationToken ct = default)`
- `Task MarkAsReadAsync(Guid notificationId, Guid userId, CancellationToken ct = default)`
- `Task MarkAllAsReadAsync(Guid userId, CancellationToken ct = default)`

**`INotificationService`** (port — higher-level orchestrator):
- `Task NotifyAsync(NotifyRequest request, CancellationToken ct = default)` 
- Internal: writes Notification row + pushes to ntfy (if configured) + pushes to NotificationHub (SignalR)

**`NotifyRequest`**:
```csharp
public record NotifyRequest(
    Guid UserId,
    Guid ActorId,      // acting user — NotifyAsync no-ops when UserId == ActorId (see §3.4)
    string Title,
    string? Body,
    string? Message,
    Guid? CardId,
    Guid? ProjectId,
    string? ActionUrl
);
```

**`NotificationService`** (implementation in Application — orchestrates DB write + ntfy push + SignalR push):
```csharp
public class NotificationService(
    INotificationRepository notifRepo,
    INtfyClient? ntfyClient,       // null if ntfy not configured
    INotificationHubBus hubBus     // in-process bus to push to SignalR
)
{
    public async Task NotifyAsync(NotifyRequest request, CancellationToken ct)
    {
        if (request.UserId == request.ActorId)
            return; // never notify a user about their own action — single enforcement point

        var notif = Notification.Create(
            request.UserId, request.Title, request.Body, request.Message,
            request.CardId, request.ProjectId, request.ActionUrl);
        await notifRepo.AddAsync(notif, ct);

        if (ntfyClient != null)
            await ntfyClient.PublishAsync(request.UserId, request.Title, request.Body, ct);

        await hubBus.SendNotificationAsync(request.UserId, notif, ct);
    }
}
```

Call sites pass every candidate recipient (including the actor, when they happen to be in the recipient set) — the actor filter lives in `NotifyAsync`, not at each of the 7 trigger call sites.

**`INotificationHubBus`** (port — in-process bus to keep Application free of SignalR dependency):
- `Task SendNotificationAsync(Guid userId, Notification notification, CancellationToken ct = default)`
- Implementation in Infrastructure maps to SignalR `Clients.User(userId).SendAsync("NotificationReceived", ...)`

### 3.3 Infrastructure

**`EfNotificationRepository`** — EF Core repository in `Infrastructure/Notifications/`.

**`INtfyClient`** (port in Application):
- `Task PublishAsync(Guid userId, string title, string? body, CancellationToken ct = default)`

**`NtfyClient`** (implementation in Infrastructure):
- Reads `SystemSettings.NtfyServerUrl` from a cached settings provider
- Constructs topic: `hydraforge-{userId}` (deterministic, derived from user ID)
- Posts JSON to `{ntfyServerUrl}/{topic}` with title, message, priority, tags
- If `NtfyServerUrl` is null/empty, `_ntfyClient` is null — graceful no-op

**`NtfyOptions`**:
```csharp
public class NtfyOptions
{
    public string? ServerUrl { get; set; }
    public string DefaultPriority { get; set; } = "default"; // default, high, urgent
}
```

No separate ntfy config section — the URL comes from `SystemSettings.NtfyServerUrl`, read via the cached settings provider. `NtfyOptions` holds defaults (priority, etc.).

### 3.4 Notification Trigger Points

Add notification calls to existing services. Each trigger is a single `await _notifService.NotifyAsync(...)` call at the point of mutation (after the domain operation succeeds):

| Event | Service | Recipients | Title/Body | ActionUrl |
|---|---|---|---|---|
| Card moved to column | `CardService.MoveCardAsync` | Card assignees + watchers | `"{actor} moved #{cardNumber} to {columnName}"` | `/projects/{projectId}/board?card={cardId}` |
| Card assigned to user | `CardService.SetAssigneesAsync` | New assignee(s) | `"{actor} assigned you to #{cardNumber}"` | Same |
| Comment added | `CommentService.AddCommentAsync` | Card watchers | `"{actor} commented on #{cardNumber}"` | Same |
| @mention in comment | `CommentService.AddCommentAsync` | Mentioned user(s) | `"{actor} mentioned you in #{cardNumber}"` | Same |
| Dependency resolved (blocking card completed → blocked card unblocked) | `CardRelationshipService` (when blocking card moved to Done or archived) | Blocked card assignees | `"{cardNumber} is no longer blocked"` | Same |
| Project archived | `ProjectService.ArchiveProjectAsync` | All project members | `"{projectName} has been archived"` | `/projects` |
| Project unarchived | `ProjectService.RestoreProjectAsync` | All project members | `"{projectName} has been restored"` | `/projects/{projectId}/board` |
| Project details edited | `ProjectService.UpdateProjectAsync` | All project members | `"{projectName} was updated by {actor}"` | Same |

**Self-notification exclusion (mandatory)**: Call sites pass the full recipient set as-is — including the actor, if they happen to be an assignee/watcher/member — one call to `NotifyAsync` per recipient, with `ActorId` set on every `NotifyRequest`. `NotifyAsync` is the single enforcement point: it no-ops when `request.UserId == request.ActorId` (see §3.2). Without this, a user moving/commenting/assigning on their own card gets notified of their own action. Don't duplicate the filter at each of the 7 call sites — one place, can't be forgotten per-trigger.

**@mention detection**: Parse `Comment.Body` for `@username` patterns at the Application layer in `CommentService`. Match against visible project members. Use regex: `@(\w[\w.-]+)`.

**Dependency resolved**: In `CardRelationshipService`, when a `BlockedBy` relationship's source card moves to a terminal column (e.g., "Done") or is completed, iterate the target card's assignees and notify. Needs a new method or hook in `CardService.MoveCardAsync`.

Note: **PR created** trigger is flagged as a dependency — no Git/PR integration exists yet. A placeholder comment in the trigger table documents this.

### 3.5 ntfy Docker Service

Add to `docker-compose.yml`:

```yaml
ntfy:
  image: binwiederhier/ntfy:v2.11.0
  environment:
    TZ: UTC
    NTFY_BASE_URL: http://ntfy:80
    NTFY_CACHE_FILE: /var/lib/ntfy/cache.db
    NTFY_AUTH_FILE: /var/lib/ntfy/auth.db
    NTFY_AUTH_DEFAULT_ACCESS: deny-all
  volumes:
    - ntfy-data:/var/lib/ntfy
  ports:
    - "8083:80"
  healthcheck:
    test: ["CMD", "wget", "-q", "http://localhost:80/v1/health"]
    interval: 30s
    timeout: 10s
    retries: 3
```

The `server` service should `depends_on: ntfy` (condition: `service_healthy`) when notification features are needed. Add a `profiles: ["notifications"]` marker to make it opt-in for now (like `searxng`), since self-hosted ntfy may not be desired by all deployments.

Add volume: `ntfy-data:`.

Update `.env.example` with `NTFY_BASE_URL=http://localhost:8083`.

### 3.6 Topic Naming

Topic format: `hydraforge-{userId}` — deterministic, no per-notification topic creation. Users subscribe to their own topic. The ntfy server URL is configured by the admin and stored in `SystemSettings.NtfyServerUrl`.

The TUI/Web UI do not expose ntfy to end users — ntfy is purely a push-fallback for when the user is not actively connected to the app (desktop notifications, mobile push via ntfy app). The in-app experience uses SignalR exclusively.

---

## 4. In-App Bell + Unread Count (Web UI)

### 4.1 Navbar Bell

Add a bell icon button in `layouts/default.vue` header `#right` slot, before the color mode button:

```vue
<template #right>
  <UButton
    v-if="isAuthenticated"
    icon="i-heroicons-bell"
    color="neutral"
    variant="ghost"
    :badge="unreadCount > 0 ? unreadCount : undefined"
    @click="showNotifications = !showNotifications"
  />
  <UColorModeButton />
  <UButton
    v-if="isAuthenticated"
    label="Logout"
    color="neutral"
    variant="ghost"
    @click="logout"
  />
</template>
```

### 4.2 Notification Panel (Bell Dropdown)

A `UPopover` or slide-over panel on the bell click, rendering:

- Header: "Notifications" + "Mark all read" button
- List of notification items (title, body, relative time, read/unread indicator)
- Click notification → navigate to `ActionUrl`
- Empty state: "No notifications yet"
- Footer: "View all" link → future `/notifications` page (deferred)

### 4.3 State & API

**`useNotifications` composable** (in `app/composables/`):

```typescript
// State
const unreadCount = ref(0)
const notifications = ref<Notification[]>([])
const showPanel = ref(false)

// API calls
async function fetchUnreadCount(): Promise<number>
async function fetchNotifications(skip: number, take: number): Promise<Notification[]>
async function markAsRead(notificationId: string): Promise<void>
async function markAllAsRead(): Promise<void>
```

**API routes** (add to `routes.ts`):

```typescript
Notifications: {
  list: (userId: string) => `/api/Notifications?skip=0&take=20`,
  unreadCount: (userId: string) => `/api/Notifications/unread-count`,
  markRead: (notificationId: string) => `/api/Notifications/${notificationId}/read`,
  markAllRead: () => `/api/Notifications/read-all`
}
```

Actually, since the user is identified from the JWT, the routes don't need userId:

```typescript
Notifications: {
  list: () => `/api/Notifications?skip=0&take=20`,
  unreadCount: () => `/api/Notifications/unread-count`,
  markRead: (id: string) => `/api/Notifications/${id}/read`,
  markAllRead: () => `/api/Notifications/read-all`
}
```

### 4.4 Push vs Polling Strategy

**Primary: SignalR push** via NotificationHub (see §6). When a notification arrives:
- Hub pushes `NotificationReceived` event → client updates `unreadCount` and appends to `notifications` array if panel is open
- No polling needed while SignalR is connected

**Fallback: Periodic poll** every 60 seconds as a fallback if SignalR disconnects:
- `setInterval(fetchUnreadCount, 60000)` within a `usePollUnreadCount` composable
- Cleared when SignalR reconnects or component unmounts

**On page load**: Always fetch fresh unread count via API (covers the case where user was offline).

### 4.5 Real-time Update via SignalR

See §6 for NotificationHub contract. The client-side composable:

```typescript
// In useSignalR.ts or a dedicated useNotificationHub.ts:
function onNotificationReceived(notification: Notification) {
  unreadCount.value++
  if (showPanel.value) {
    notifications.value.unshift(notification)
  }
}
```

---

## 5. Unread Count in TUI Status Bar

### 5.1 Current State

`AppState.cs` already has `UnreadNotifications` field (line 21). `BoardRenderer.cs` status bar (line 155) shows connection/online/error counts but not unread count.

### 5.2 Changes

**BoardRenderer.cs**: Update status bar line to include unread count:

```csharp
new Markup($"[{dotColor}]●[/] [grey]{statusText}    |    {onlineCount} online    |    {unreadCount} unread    |    {errorCount} errors[/]")
```

Add `int unreadCount = 0` parameter to `BuildLayout()` method signature and pass it through from `BoardScreen.cs`.

**BoardScreen.cs**: Fetch unread count via API on render and on SignalR NotificationReceived event. Add a short polling interval (30s) for when SignalR is disconnected.

**SignalR connection**: Add `NotificationHub` connection to the TUI's existing SignalR setup. On `NotificationReceived`, increment `_appState.UnreadNotifications` and re-render status bar.

**UI interaction**: Press `U` key to open unread notifications list (simple scrollable list panel, not a full screen). This uses the same Spectre.Console `ListPrompt` pattern as the existing keyboard help overlay.

### 5.3 API Endpoints (same as Web UI)

TUI calls the same `GET /api/Notifications/unread-count` and `GET /api/Notifications` endpoints through the existing `HydraForgeApiClient`.

---

## 6. Notification Hub (SignalR)

### 6.1 New Hub

Create `Hubs/NotificationHub.cs`:

```csharp
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

Users join a personal group `user-{userId}` automatically on connect. The server pushes notifications to this group via `Clients.Group($"user-{userId}").SendAsync("NotificationReceived", ...)`.

### 6.2 Registration

In `Program.cs`:

```csharp
builder.Services.AddSignalR()
    .AddHubOptions<NotificationHub>(options =>
    {
        // Add to existing SignalR config
    });

// In app.Map():
app.MapHub<BoardHub>("/hubs/board");
app.MapHub<PresenceHub>("/hubs/presence");
app.MapHub<NotificationHub>("/hubs/notifications");
```

### 6.3 INotificationHubBus Implementation

In `Infrastructure/Realtime/`:

```csharp
public class SignalRNotificationHubBus(IHubContext<NotificationHub> hubContext) : INotificationHubBus
{
    public async Task SendNotificationAsync(Guid userId, Notification notification, CancellationToken ct)
    {
        await hubContext.Clients.Group($"user-{userId}").SendAsync(
            "NotificationReceived",
            new { notification.Id, notification.Title, notification.Body, notification.CardId, notification.ProjectId, notification.ActionUrl, notification.CreatedAt },
            ct
        );
    }
}
```

### 6.4 TUI SignalR Connection

Extend the TUI's `SignalRConnectionManager` to also connect to `/hubs/notifications`:

```csharp
var notificationConnection = new HubConnectionBuilder()
    .WithUrl($"{baseUrl}/hubs/notifications", options => { options.AccessTokenProvider = () => Task.FromResult(token); })
    .Build();

notificationConnection.On<NotificationDto>("NotificationReceived", notif =>
{
    _appState.UnreadNotifications++;
    // Optionally store in a local list
});
```

Keep this as a separate `HubConnection` (not merged with BoardHub). BoardHub disconnects when leaving a project; NotificationHub stays connected for the app's lifetime.

---

## 7. Admin Dashboard

### 7.1 API Endpoints

Create **`AdminController`** at `Controllers/Admin/AdminController.cs` with `[Authorize]` + admin authorization:

```csharp
[Authorize(Policy = AuthPolicies.AdminRequired)]  // or [Authorize(Roles = "Admin")]
[Route("api/admin")]
[ApiController]
public class AdminController : ControllerBase
```

Endpoints:

| Method | Route | Description |
|---|---|---|
| GET | `/api/admin/health` | System health (wraps existing HealthController data + DB stats) |
| GET | `/api/admin/users` | List all users (paginated, searchable) |
| GET | `/api/admin/users/{userId}` | Single user detail |
| POST | `/api/admin/users` | Create user |
| PATCH | `/api/admin/users/{userId}/disable` | Disable user |
| PATCH | `/api/admin/users/{userId}/enable` | Enable user |
| POST | `/api/admin/users/{userId}/reset-password` | Reset user password |
| PATCH | `/api/admin/users/{userId}/role` | Toggle admin role |
| GET | `/api/admin/projects` | List all projects (including those user is not member of) |
| GET | `/api/admin/projects/{projectId}` | Single project detail with members |
| GET | `/api/admin/settings` | Get system settings |
| PUT | `/api/admin/settings` | Update system settings |
| GET | `/api/admin/audit-log` | Query audit log (see §11) |

### 7.2 Authorization Policy

**`AuthPolicies.AdminRequired`**: Reusable policy that checks `ClaimTypes.Role == Roles.Admin` (now works after the JWT fix in §2).

Register in `Program.cs`:

```csharp
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(AuthPolicies.AdminRequired, policy =>
        policy.RequireRole(Roles.Admin));
});
```

### 7.3 Web UI Pages

Create `/pages/admin/` directory:

| Route | Page | Description |
|---|---|---|
| `/admin` | `admin/index.vue` | Dashboard home — links to all admin sections |
| `/admin/users` | `admin/users.vue` | User list (table with search, pagination, create/edit/disable/reset-password actions) |
| `/admin/users/new` | `admin/users-new.vue` (or modal) | Create user form |
| `/admin/projects` | `admin/projects.vue` | All-projects table |
| `/admin/settings` | `admin/settings.vue` | System settings editor |
| `/admin/audit-log` | `admin/audit-log.vue` | Audit log browser |

Add `UiRoutes.Admin.*` to `routes.ts`. Per CLAUDE.md ("Never write inline API path strings... if a route is not in routes.ts, add it there first"), every endpoint from §7.1's table needs a matching `ApiRoutes.Admin.*` entry — not left implicit:

```typescript
Admin: {
  health: () => `/api/admin/health`,
  usersList: (skip: number, take: number, search?: string) => `/api/admin/users?skip=${skip}&take=${take}${search ? `&search=${search}` : ''}`,
  userGet: (userId: string) => `/api/admin/users/${userId}`,
  userCreate: () => `/api/admin/users`,
  userDisable: (userId: string) => `/api/admin/users/${userId}/disable`,
  userEnable: (userId: string) => `/api/admin/users/${userId}/enable`,
  userResetPassword: (userId: string) => `/api/admin/users/${userId}/reset-password`,
  userRole: (userId: string) => `/api/admin/users/${userId}/role`,
  projectsList: () => `/api/admin/projects`,
  projectGet: (projectId: string) => `/api/admin/projects/${projectId}`,
  settingsGet: () => `/api/admin/settings`,
  settingsUpdate: () => `/api/admin/settings`,
  auditLog: () => `/api/admin/audit-log`,
}
```

Add a sidebar or top nav link for admin in `default.vue` layout, visible only when `user.isAdmin` is true:

```vue
<template #left>
  <NuxtLink to="/projects" class="flex items-center gap-2">
    <span class="text-lg font-bold">HydraForge</span>
  </NuxtLink>
  <UNavigationMenu v-if="user?.isAdmin" :items="adminNavItems" />
</template>
```

### 7.4 Admin Dashboard Home

The `/admin` index page shows a grid of summary cards:

- Total users (with active vs disabled breakdown)
- Total projects (with active vs archived breakdown)
- Total cards
- System health status (green/red dot with component breakdown)
- Recent audit log entries (last 10)
- Quick-action buttons: Create User, View Settings, Browse Audit Log

---

## 8. Admin User Management

### 8.1 Domain Entity Pattern (non-negotiable, CLAUDE.md)

`User.cs` as read today is a plain property bag — `IsAdmin`/`IsDisabled`/`PasswordHash` all have public setters, no instance methods. CLAUDE.md: "services orchestrate but NEVER set entity properties directly." `AdminService` below must not do `user.IsDisabled = true`. Add these instance methods to `User` first:

| Method | Effect |
|---|---|
| `Disable()` | `IsDisabled = true` |
| `Enable()` | `IsDisabled = false` |
| `SetAdminRole(bool isAdmin)` | `IsAdmin = isAdmin` |
| `SetPasswordHash(string hash)` | `PasswordHash = hash` |

`AdminService` calls these on the loaded entity; it never assigns `User` properties itself.

### 8.2 Backend Logic

**`AdminService`** in `Application/Admin/`:

| Method | Description |
|---|---|
| `ListUsersAsync(skip, take, search)` | Paginated + searchable user list |
| `GetUserAsync(userId)` | Single user detail |
| `CreateUserAsync(username, password, name, email, isAdmin)` | Creates user, validates uniqueness |
| `DisableUserAsync(userId)` | Loads user, calls `user.Disable()` — user cannot log in |
| `EnableUserAsync(userId)` | Loads user, calls `user.Enable()` |
| `ResetPasswordAsync(userId, newPassword)` | Hashes password, calls `user.SetPasswordHash(hash)` |
| `ToggleAdminRoleAsync(actorId, targetUserId)` | Loads user, calls `user.SetAdminRole(!user.IsAdmin)` — safety check below prevents self-demotion |

All methods validate: actor must be admin (already enforced by controller auth), target user must exist.

**Self-demotion prevention**:

```csharp
public async Task<Result> ToggleAdminRoleAsync(Guid actorId, Guid targetUserId)
{
    if (actorId == targetUserId)
        return Result.Failure(new Error("ADMIN_SELF_DEMOTION", "Cannot remove your own admin role."));
    // ...
}
```

### 8.3 Admin User List (Web UI)

Table with columns: Username, Name, Email, Admin (badge), Disabled (badge), Last Login, Actions.

Actions: Edit (inline or modal), Disable/Enable toggle, Reset Password, Toggle Admin Role.

Create user via `UModal` with form fields: Username, Password, Name, Email, IsAdmin toggle.

### 8.4 TUI Admin Screens (Deferred)

Admin screens in the TUI are deferred to a future phase. The TUI gets notification unread count in this phase but not admin management screens. Rationale: admin management is primarily a Web UI concern; the TUI is optimized for board interaction, not system configuration.

---

## 9. Admin System Settings

### 9.1 Schema Additions

Add to `SystemSettings` entity:

```csharp
public string? NtfyServerUrl { get; set; }       // ntfy push server URL
public string? SearXngUrl { get; set; }           // SearXNG search URL
public string? BrandName { get; set; }            // Platform brand name (default "HydraForge")
public string? BrandLogoUrl { get; set; }         // Optional logo URL
```

Default seeded values remain: `null` for all new fields (opt-in configuration).

**Domain entity pattern (non-negotiable, CLAUDE.md)**: `SystemSettings.cs` as read today is a plain property bag, no instance methods. Add `UpdateSettings(int? archivedItemRetentionDays, int? auditLogRetentionDays, int? notificationRetentionDays, string? ntfyServerUrl, string? searXngUrl, string? brandName, string? brandLogoUrl)` — applies only the non-null args, sets `UpdatedAt`. The settings controller (§9.2) calls this on the loaded singleton; it never assigns `SystemSettings` properties itself.

### 9.2 Settings Controller

`PUT /api/admin/settings` accepts a partial update model:

```csharp
public record UpdateSystemSettingsRequest(
    int? ArchivedItemRetentionDays,
    int? AuditLogRetentionDays,
    int? NotificationRetentionDays,
    string? NtfyServerUrl,
    string? SearXngUrl,
    string? BrandName,
    string? BrandLogoUrl
);
```

Only non-null fields are updated — the controller loads the singleton and calls `settings.UpdateSettings(request.ArchivedItemRetentionDays, ...)` (see domain entity pattern note above), not a property-by-property set. This allows the admin to change one setting at a time without reading the current state.

`GET /api/admin/settings` returns the full `SystemSettings` object (minus `Id` and timestamps).

### 9.3 5-Min Cache TTL

**`CachedSettingsProvider`** in Infrastructure:

```csharp
public class CachedSettingsProvider(ISettingsRepository repo, IMemoryCache cache) : ISettingsProvider
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    public async Task<SystemSettings> GetAsync(CancellationToken ct = default)
    {
        return await cache.GetOrCreateAsync("system_settings", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheTtl;
            return await repo.GetSingletonAsync(ct);
        }) ?? new SystemSettings();  // fallback default
    }
}
```

**`ISettingsRepository`** (port in Application):
- `Task<SystemSettings> GetSingletonAsync(CancellationToken ct = default)`
- `Task UpdateAsync(SystemSettings settings, CancellationToken ct = default)`

**EfSettingsRepository** reads the singleton row. On `PUT /api/admin/settings`, the cache entry is invalidated (`cache.Remove("system_settings")`) so the next read fetches fresh data.

**`ISettingsProvider`** (port in Application) — used by other services that need settings:
- `Task<SystemSettings> GetAsync(CancellationToken ct = default)`

The `CachedSettingsProvider` implements `ISettingsProvider` and is registered as singleton-scoped (the underlying `IMemoryCache` is thread-safe).

### 9.4 Settings Page (Web UI)

The `/admin/settings` page has sections:

1. **Retention** — ArchivedItemRetentionDays, AuditLogRetentionDays, NotificationRetentionDays (number inputs with descriptions)
2. **Notifications** — NtfyServerUrl (text input)
3. **Search** — SearXngUrl (text input)  
4. **Branding** — BrandName, BrandLogoUrl (text inputs)

Each section has a "Save" button that calls `PUT /api/admin/settings` with only that section's fields (partial update model supports this). Success toast: "Settings saved. Changes will apply within 5 minutes (cache TTL) or on next housekeeping run."

---

## 10. Admin All-Projects Bypass

### 10.1 Current State

Two membership check patterns exist:

**Pattern 1 — Explicit membership check in controllers** (e.g., `CardsController`, `ColumnsController`):
```csharp
var membership = await _memberRepo.GetByProjectAndUserAsync(projectId, userId);
if (membership == null)
    return Forbid();
```

**Pattern 2 — Hub `IsInRole` check** — confirmed in both `PresenceHub.cs:31` and `BoardHub.JoinProject` (`src/HydraForge.Infrastructure/Realtime/BoardHub.cs:30`):
```csharp
var isAdmin = Context.User!.IsInRole("Admin");
if (!isAdmin) { /* check membership */ }
```
Both hubs already anticipate the admin bypass — they're broken today only because the JWT never emits the role claim (§2). Once §2 ships, both hubs work with no further code change. `BoardHub` needs no update beyond the JWT fix — remove it from any "controllers to touch" list.

### 10.2 Changes

Define the role name once — avoid the "Admin" string literal drifting across `ClaimTypes.Role` emission (§2), `RequireRole`, and every `IsInRole` call:

```csharp
// HydraForge.Domain (or Application) — shared by JwtTokenIssuer, AuthPolicies, and this helper
public static class Roles
{
    public const string Admin = "Admin";
}
```

Then a reusable extension method in `Server/`:

```csharp
public static async Task<bool> IsProjectMemberOrAdmin(this ClaimsPrincipal user, IProjectMemberRepository memberRepo, Guid projectId)
{
    if (user.IsInRole(Roles.Admin))
        return true;
    var membership = await memberRepo.GetByProjectAndUserAsync(projectId, user.GetRequiredUserId());
    return membership != null;
}
```

Replace all `Forbid()` patterns in controllers with:

```csharp
if (!await User.IsProjectMemberOrAdmin(_memberRepo, projectId))
    return Forbid();
```

`§2`'s `JwtTokenIssuer` and `§7.2`'s `AuthPolicies.AdminRequired`/`RequireRole` both reference `Roles.Admin`, not a raw string.

### 10.3 Controllers to Update

- `ProjectsController` — all endpoints (list, get, update, archive, delete)
- `CardsController` — all card-scoped endpoints
- `ColumnsController` — all column-scoped endpoints
- `CommentsController` — all comment endpoints
- `AttachmentsController` — all attachment endpoints
- `RelationshipsController` — all relationship endpoints
- `SpecsController`, `PlansController`
- `ProjectSnapshotController`
- `CardChecklistController`
- `PresenceHub.JoinProject` (already gated on `IsInRole` — just needs §2's JWT fix, no code change here)

`BoardHub.JoinProject` also already gated on `IsInRole` (confirmed by reading the file) — same as `PresenceHub`, it needs no controller-style rewrite, only the JWT fix in §2.

Existing `ProjectsController` list endpoint: admin should see **all** projects, not just their own memberships. The `ListForUserAsync` method in the repository needs an admin variant: `ListAllAsync` or an `includeAllProjectsIfAdmin` parameter.

---

## 11. Audit Log Viewer

### 11.1 Current State

**Write path**: Fully built — `IAuditLogWriter`, `EfAuditLogWriter`, `AuditService`, and 8+ service trigger points exist.

**Read path**: Completely missing — no `IAuditLogReader`, no controller, no query service.

### 11.2 Application Layer

**`IAuditLogReader`** (port in Application):

```csharp
public interface IAuditLogReader
{
    Task<AuditLogQueryResult> QueryAsync(AuditLogQuery query, CancellationToken ct = default);
}

public record AuditLogQuery(
    Guid? ProjectId = null,
    Guid? ActorId = null,
    string? EntityType = null,
    string? Action = null,
    DateTime? From = null,
    DateTime? To = null,
    int Skip = 0,
    int Take = 50
);

public record AuditLogQueryResult(
    IReadOnlyList<AuditLogEntryDto> Items,
    int TotalCount
);

public record AuditLogEntryDto(
    Guid Id,
    Guid? ProjectId,
    Guid ActorId,
    string ActorName,      // joined from User table
    string EntityType,
    Guid EntityId,
    string Action,
    string? OldValue,
    string? NewValue,
    DateTime Timestamp,
    string Scope
);
```

**`AuditLogQueryService`** (implementation in Application — or just put it in Infrastructure as `EfAuditLogReader`):

The query is straightforward: filter on `AuditLogEntry` table with optional joins on `User` for actor name. No complex aggregation needed.

### 11.3 Controller

```csharp
[Authorize(Policy = AuthPolicies.AdminRequired)]
[Route("api/admin/audit-log")]
public class AuditLogController(IAuditLogReader reader) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Query(
        [FromQuery] Guid? projectId,
        [FromQuery] Guid? actorId,
        [FromQuery] string? entityType,
        [FromQuery] string? action,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50
    )
    {
        var query = new AuditLogQuery(projectId, actorId, entityType, action, from, to, skip, take);
        var result = await _reader.QueryAsync(query, ct);
        return Ok(result);
    }
}
```

### 11.4 Web UI Page

`/admin/audit-log` page has:

- **Filter bar**: Project dropdown (all projects), User dropdown (search), Entity type dropdown (Card/Column/Project/Comment/Attachment/Spec/Plan/Checklist/Relationship), Action type, Date range (from/to date pickers)
- **Results table**: Timestamp, Actor, Entity Type, Entity ID (truncated), Action, Project, Scope
- **Row expansion** or modal for detail: shows OldValue/NewValue JSON formatted
- **Pagination** at bottom
- **Export to CSV** button (future, nice-to-have)

All filter params are URL query params for shareable/bookmarkable links.

Non-admin users should not see the audit log page or its API (enforced by `[Authorize(Policy = AuthPolicies.AdminRequired)]`).

### 11.5 TUI Audit Log (Deferred)

Admin audit log browsing in the TUI is deferred. Same rationale as admin screens (TUI is board-centric).

---

## 12. HousekeepingBackgroundService — Scope Decision

### 12.1 Current State

The retention fields (`ArchivedItemRetentionDays`, `AuditLogRetentionDays`, `NotificationRetentionDays`) exist in `SystemSettings` and are seeded with defaults (730, 90, 30). However, no `HousekeepingBackgroundService` exists to act on them. The `CLAUDE.md` explicitly flags this as deferred.

### 12.2 Recommendation: DEFERRED — Not in Phase 5 Scope

**Rationale:**

1. **No hard requirement** — The retention knobs work fine without the service; records simply aren't cleaned up. No immediate user-facing bug or feature gap.
2. **Scope complexity** — A proper housekeeping service needs:
   - `IHostedService` registration
   - Scheduled execution (e.g., daily at 2 AM, configurable interval)
   - Soft-delete awareness (archived items vs permanently deleted)
   - Notification/audit-log cleanup that respects `NotificationRetentionDays`/`AuditLogRetentionDays`
   - Archived-project/item cleanup that respects `ArchivedItemRetentionDays`
   - Logging, error handling, rate limiting
   - Admin toggle (pause/resume housekeeping)
3. **Independent work item** — Housekeeping is orthogonal to all other Phase 5 tasks. It can be implemented as a standalone Phase 5.1 or Phase 6 task without blocking notifications or admin.
4. **Retention knobs are still useful** — Even without housekeeping, the settings page lets admins configure the TTL values. They'll be read by HousekeepingBackgroundService when it's later implemented.

**If scope creep is acceptable** (smallest viable implementation): single `BackgroundService` that runs every 6 hours, deletes `AuditLogEntry` older than `AuditLogRetentionDays`, deletes `Notification` older than `NotificationRetentionDays`, and soft-deletes archived projects/items past `ArchivedItemRetentionDays`. ~2 days of work. But the recommendation is to defer.

**Decision: HousekeepingBackgroundService stays deferred.**

---

## 13. Dependency Notes

### 13.1 PR Created Event (Flagged)

The functional spec lists "PR created → all members" as a notification trigger (FR-47). However, no Git/PR integration exists in HydraForge:

- No repository entity (Git remote URL exists on Project, but no PR/webhook model)
- No webhook receiver
- No PR entity or event model

**Action**: Add a comment in the code and in `docs/backlog.md` noting that the PR notification trigger is a placeholder until the Git integration is implemented. Do not create stub endpoints or dead code for it.

### 13.2 Notification on Dependency Resolved

This trigger depends on `CardRelationshipService` having a hook point after a related card moves to a terminal state. Currently `MoveCardAsync` does not check relationships on the target side. Two approaches:

- **Approach A**: Add a `CheckAndNotifyResolvedDependenciesAsync` call inside `CardService.MoveCardAsync` after the move succeeds. This is clean — it keeps the trigger close to the mutation.
- **Approach B**: Event-based — fire a `CardMovedEvent` domain event. Over-engineered for now.

**Decision**: Approach A — inline method call in `CardService.MoveCardAsync` after successful move. Extract into a private method that queries relationships and fires notifications.

### 13.3 ntfy Deployment Topology

ntfy is **optional** (opt-in via Docker profile). Users who don't run ntfy still get in-app notifications via SignalR (when connected) and nothing when offline. This is acceptable — HydraForge is server-authoritative with no offline mode (D-13).

The admin may leave `NtfyServerUrl` empty in settings. The `NtfyClient` is null-injected and gracefully skips ntfy push when null.

### 13.4 Web UI Admin Nav Visibility

Admin nav items (sidebar links to admin sections) should be visible only when `user.isAdmin` is true. The frontend determines admin status from the JWT payload (`payload.is_admin`), which is already read by `auth.ts`'s `restoreToken`. No additional API call needed for nav visibility.

However, API endpoints are protected by server-side `[Authorize(Roles = "Admin")]` as the authoritative gate. The frontend nav visibility is a UX convenience, not a security boundary.

---

## 14. Implementation Order

Recommended sequence: **cheapest wins first** (high-value, low-complexity items early).

| Order | Task | Effort | Rationale |
|---|---|---|---|
| 1 | JWT role claim fix | Small (1 file, ~3 lines, + `Roles.Admin` constant) | Unblocks all admin authz, including `BoardHub`/`PresenceHub` which already gate on `IsInRole` and need no further change; test confirms role claim present/absent |
| 2 | Notification API + NotificationService | Medium (4-5 files, + `Notification.Create()`/`MarkRead()` instance methods) | Foundation for bell + TUI unread + ntfy; no UI dependency |
| 3 | NotificationHub + SignalR push | Small (2 files + Program.cs registration) | Provides real-time push; independent of client consumers |
| 4 | Web UI bell + unread count | Medium (composable + layout change + panel component) | Visible user-facing win; needs NotificationService + Hub from 2/3 |
| 5 | TUI unread count in status bar | Small (BoardRenderer + BoardScreen changes) | Already has AppState field; wiring + API call |
| 6 | ntfy integration + docker-compose | Medium (client + settings field + compose) | No UI dependency; independent of bell |
| 7 | Notification trigger points (7 services) | Large (touch 7+ application services) | Each trigger is ~5-10 lines; time is in finding right spot + tests, including the self-notification exclusion test |
| 8 | Admin all-projects bypass | Medium (`IsProjectMemberOrAdmin` extension method + update controllers in §10.3) | Mechanical: find/replace pattern across controllers only — `BoardHub`/`PresenceHub` already handled by Task 1 |
| 9 | Admin controller + user management | Medium (`User` instance methods + `AdminController` + `AdminService` + Web UI page) | Needs JWT fix (1) and bypass (8) done first |
| 10 | System settings API + Web UI page | Medium (`SystemSettings.UpdateSettings()` instance method + controller + page + cache layer) | New entity fields need migration; cache layer is new pattern |
| 11 | Audit log reader + controller | Small (IAuditLogReader + EfAuditLogReader + controller) | Write path already done; read is just a query |
| 12 | Audit log Web UI page | Medium (page with filter bar + results table) | Biggest single UI page; needs reader (11) |
| 13 | Admin dashboard home page | Small (dashboard cards + links) | Integrates all admin pages; needs 9/10/12 done |

**Quick-win batch (1-6)**: Can be worked in parallel by 2 agents (backend tasks + frontend tasks).

**Trigger points (7)**: Largest backend task. Each of 7 services gets a notification call.

**Admin endpoints (8-10)**: Sequential — bypass must be done before admin controller.

**Audit log (11-12)**: Independent of other admin work; can start after reader is built.

**Dashboard (13)**: Last — it's just a landing page wrapper.

---

## 15. Tasks

Per CLAUDE.md, Application/Domain layers need >90% test coverage — each task below includes its own test subtask, not deferred to a separate pass.

- [x] Task 1: Fix JWT role claim — add shared `Roles.Admin` constant, `ClaimTypes.Role` emission in `JwtTokenIssuer.cs`; tests: token issuance includes role claim iff `user.IsAdmin`
- [x] Task 2: Notification system — `Notification.Create()` factory + `MarkRead()` instance method on the entity (replacing the current property-bag shape), `INotificationRepository`, `INotificationService`/`NotifyRequest` with actor-exclusion no-op (§3.2/§3.4), `EfNotificationRepository`, DI registration; tests: `NotifyAsync` skips write+push when `UserId == ActorId`, repository CRUD, `MarkRead()`/`Create()` unit tests
- [x] Task 3: NotificationHub — create `Hubs/NotificationHub.cs`, register in `Program.cs`, implement `INotificationHubBus` via `SignalRNotificationHubBus`; tests: group join on connect, push payload shape
- [x] Task 4: Web UI bell icon + notification panel — `useNotifications` composable, layout change, panel component, `ApiRoutes.Notifications.*` in `routes.ts`; tests: composable unread-count/mark-read behavior
- [x] Task 5: TUI unread count — wire `AppState.UnreadNotifications` into `BoardRenderer` status bar, add polling/SignalR
- [ ] Task 6: ntfy integration — `INtfyClient`, `NtfyClient`, `NtfyOptions`, `SystemSettings.NtfyServerUrl` field + migration, docker-compose ntfy service; tests: null-client no-op, topic naming
- [ ] Task 7: Notification trigger points — add `_notifService.NotifyAsync()` calls in `CardService`, `CommentService`, `CardRelationshipService`, `ProjectService` (7 service methods); tests: one per trigger, including a same-actor case asserting no self-notification
- [ ] Task 8: Admin all-projects bypass — `IsProjectMemberOrAdmin` extension method (using `Roles.Admin` from Task 1), update all controllers in §10.3; confirm `BoardHub`/`PresenceHub` need no code change beyond Task 1 (already gated on `IsInRole`); tests: bypass true for admin, false for non-member
- [ ] Task 9: Admin controller + user management — `User.Disable()`/`Enable()`/`SetAdminRole()`/`SetPasswordHash()` instance methods (replacing direct property sets), `AdminController` (users CRUD), `AdminService`, Web UI admin user page; tests: self-demotion rejection, each instance method, service methods call instance methods not property setters
- [ ] Task 10: System settings API + cache — `SystemSettings.UpdateSettings(...)` instance method (partial-update, replacing direct property sets), schema migration (NtfyServerUrl, SearXngUrl, BrandName, BrandLogoUrl), `CachedSettingsProvider`, settings controller, `ApiRoutes.Admin.*` in `routes.ts`, Web UI settings page; tests: `UpdateSettings` only touches non-null args, cache invalidation on write
- [ ] Task 11: Audit log reader — `IAuditLogReader`, `EfAuditLogReader`, `AuditLogQuery`, `AuditLogController`; tests: each filter dimension (project/actor/entityType/date range)
- [ ] Task 12: Audit log Web UI page — filter bar, results table, pagination
- [ ] Task 13: Admin dashboard home page — summary cards, health status, recent audit entries, quick-action buttons