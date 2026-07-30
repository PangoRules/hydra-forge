# Plan 4: Web UI Bell Icon + Notification Panel

**Branch:** `task/web-ui-bell`
**Parent branch:** `feat/phase-5-notifications-admin`
**Parent spec:** `2026-07-25-phase-5-notifications-admin-design.md` — Task 4

## Task

Add bell icon with unread badge to `default.vue` layout. Create notification dropdown panel. Create `useNotifications` composable. Add `ApiRoutes.Notifications.*` to `routes.ts`. Create `NotificationsController` API endpoint.

## Files to create

- `src/web-ui/app/composables/useNotifications.ts`
- `src/web-ui/app/components/notifications/NotificationBell.vue`
- `src/web-ui/app/components/notifications/NotificationPanel.vue`
- `src/HydraForge.Server/Controllers/NotificationsController.cs`

## Files to modify

- `src/web-ui/app/layouts/default.vue` — add bell icon in `#right` slot
- `src/web-ui/app/lib/routes.ts` — add `ApiRoutes.Notifications.*`

## Implementation steps

### Step 1: Add API routes

In `src/web-ui/app/lib/routes.ts`, add to `ApiRoutes`:

```typescript
Notifications: {
  list: (skip = 0, take = 20) => `/api/Notifications?skip=${skip}&take=${take}`,
  unreadCount: () => '/api/Notifications/unread-count',
  markRead: (id: string) => `/api/Notifications/${id}/read`,
  markAllRead: () => '/api/Notifications/read-all'
},
```

### Step 2: Create NotificationsController

Create `src/HydraForge.Server/Controllers/NotificationsController.cs`:

```csharp
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
```

### Step 3: Create useNotifications composable

Create `src/web-ui/app/composables/useNotifications.ts`:

```typescript
import { ref } from 'vue'
import { useApi } from '~/composables/useApi'
import { ApiRoutes } from '~/lib/routes'

export interface NotificationItem {
  id: string
  title: string
  body: string | null
  cardId: string | null
  projectId: string | null
  actionUrl: string | null
  isRead: boolean
  createdAt: string
}

export function useNotifications() {
  const api = useApi()
  const unreadCount = ref(0)
  const notifications = ref<NotificationItem[]>([])
  const showPanel = ref(false)

  async function fetchUnreadCount(): Promise<void> {
    try {
      const data = await api.GET<{ count: number }>(ApiRoutes.Notifications.unreadCount())
      unreadCount.value = data.count
    } catch {
      // Silently fail — bell just shows 0
    }
  }

  async function fetchNotifications(skip = 0, take = 20): Promise<void> {
    try {
      const data = await api.GET<NotificationItem[]>(ApiRoutes.Notifications.list(skip, take))
      notifications.value = data
    } catch {
      // Silently fail
    }
  }

  async function markAsRead(notificationId: string): Promise<void> {
    try {
      await api.POST(ApiRoutes.Notifications.markRead(notificationId))
      const notif = notifications.value.find(n => n.id === notificationId)
      if (notif && !notif.isRead) {
        notif.isRead = true
        unreadCount.value = Math.max(0, unreadCount.value - 1)
      }
    } catch {
      // Silently fail
    }
  }

  async function markAllAsRead(): Promise<void> {
    try {
      await api.POST(ApiRoutes.Notifications.markAllRead())
      notifications.value.forEach(n => { n.isRead = true })
      unreadCount.value = 0
    } catch {
      // Silently fail
    }
  }

  function onNotificationReceived(notification: NotificationItem): void {
    unreadCount.value++
    if (showPanel.value) {
      notifications.value.unshift(notification)
    }
  }

  return {
    unreadCount,
    notifications,
    showPanel,
    fetchUnreadCount,
    fetchNotifications,
    markAsRead,
    markAllAsRead,
    onNotificationReceived,
  }
}
```

### Step 4: Create NotificationBell component

Create `src/web-ui/app/components/notifications/NotificationBell.vue`:

```vue
<script setup lang="ts">
const { unreadCount, showPanel } = useNotifications()

function toggle() {
  showPanel.value = !showPanel.value
}
</script>

<template>
  <div class="relative">
    <UButton
      icon="i-heroicons-bell"
      color="neutral"
      variant="ghost"
      @click="toggle"
    />
    <span
      v-if="unreadCount > 0"
      class="absolute -top-1 -right-1 bg-red-500 text-white text-xs rounded-full w-5 h-5 flex items-center justify-center"
    >
      {{ unreadCount > 99 ? '99+' : unreadCount }}
    </span>
  </div>
</template>
```

### Step 5: Create NotificationPanel component

Create `src/web-ui/app/components/notifications/NotificationPanel.vue`:

```vue
<script setup lang="ts">
const {
  notifications,
  showPanel,
  fetchNotifications,
  markAsRead,
  markAllAsRead,
} = useNotifications()

const isClient = import.meta.client

watch(showPanel, async (open) => {
  if (open) {
    await fetchNotifications()
  }
})

function handleClick(notif: { id: string, actionUrl: string | null, isRead: boolean }) {
  if (!notif.isRead) {
    markAsRead(notif.id)
  }
  if (notif.actionUrl && isClient) {
    showPanel.value = false
    navigateTo(notif.actionUrl)
  }
}

function timeAgo(dateStr: string): string {
  const diff = Date.now() - new Date(dateStr).getTime()
  const mins = Math.floor(diff / 60000)
  if (mins < 1) return 'just now'
  if (mins < 60) return `${mins}m ago`
  const hours = Math.floor(mins / 60)
  if (hours < 24) return `${hours}h ago`
  const days = Math.floor(hours / 24)
  return `${days}d ago`
}
</script>

<template>
  <ClientOnly>
    <UPopover v-model:open="showPanel" :arrow="true">
      <template #default>
        <div />
      </template>

      <template #content>
        <div class="w-80 max-h-96 overflow-y-auto">
          <div class="flex items-center justify-between p-3 border-b">
            <span class="font-semibold">Notifications</span>
            <UButton
              label="Mark all read"
              color="neutral"
              variant="ghost"
              size="xs"
              @click="markAllAsRead"
            />
          </div>

          <div v-if="notifications.length === 0" class="p-4 text-center text-gray-500 text-sm">
            No notifications yet
          </div>

          <div
            v-for="notif in notifications"
            :key="notif.id"
            class="p-3 border-b last:border-b-0 cursor-pointer hover:bg-gray-50 dark:hover:bg-gray-800"
            :class="{ 'bg-blue-50 dark:bg-blue-900/20': !notif.isRead }"
            @click="handleClick(notif)"
          >
            <div class="flex items-start gap-2">
              <span
                v-if="!notif.isRead"
                class="w-2 h-2 mt-1.5 rounded-full bg-blue-500 flex-shrink-0"
              />
              <span v-else class="w-2 flex-shrink-0" />
              <div class="min-w-0">
                <p class="text-sm font-medium truncate">{{ notif.title }}</p>
                <p v-if="notif.body" class="text-xs text-gray-500 truncate">{{ notif.body }}</p>
                <p class="text-xs text-gray-400 mt-0.5">{{ timeAgo(notif.createdAt) }}</p>
              </div>
            </div>
          </div>
        </div>
      </template>
    </UPopover>
  </ClientOnly>
</template>
```

### Step 6: Update default.vue layout

In `src/web-ui/app/layouts/default.vue`, add the bell and panel components in the `#right` template slot, before the color mode button:

```vue
<script setup lang="ts">
import SessionExpiryModal from '~/components/shared/SessionExpiryModal.vue'
import NotificationBell from '~/components/notifications/NotificationBell.vue'
import NotificationPanel from '~/components/notifications/NotificationPanel.vue'

const { logout, isAuthenticated, checkAuth } = useAuth()
const { fetchUnreadCount } = useNotifications()
// ... rest of existing script
</script>

<template>
  <UApp ...>
    <UHeader>
      <template #left>
        <!-- existing -->
      </template>

      <template #right>
        <ClientOnly>
          <NotificationBell v-if="isAuthenticated" />
        </ClientOnly>
        <UColorModeButton />
        <ClientOnly>
          <UButton
            v-if="isAuthenticated"
            label="Logout"
            color="neutral"
            variant="ghost"
            @click="logout"
          />
        </ClientOnly>
      </template>
    </UHeader>

    <UMain class="flex-1 flex flex-col overflow-hidden">
      <slot />
    </UMain>

    <NotificationPanel />

    <!-- existing SessionExpiryModal -->
  </UApp>
</template>
```

Also add `onMounted` call to fetch unread count:

```typescript
onMounted(() => {
  startSessionManager()
  if (isAuthenticated.value) {
    fetchUnreadCount()
  }
})
```

### Step 7: Verify

```bash
cd src/web-ui && pnpm typecheck
cd src/web-ui && pnpm lint
cd src/web-ui && pnpm build
```

```bash
dotnet build
```

## Verification

- `pnpm typecheck` — no errors
- `pnpm lint` — no errors
- `pnpm build` — no errors
- `dotnet build` — no errors
- Manual: login, see bell icon in navbar, click to open panel, see "No notifications yet" empty state

## Dependencies

- Task 2 (NotificationService + repository must exist for API endpoints)
- Task 3 (NotificationHub for real-time updates — composable's `onNotificationReceived` wires to it in a follow-up)