<script setup lang="ts">
const {
  notifications,
  showPanel,
  fetchNotifications,
  markAsRead,
  markAllAsRead,
  unreadCount
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
    <UPopover
      v-model:open="showPanel"
      :arrow="true"
    >
      <template #default>
        <div class="relative">
          <UButton
            icon="i-lucide-bell"
            color="neutral"
            variant="ghost"
          />
          <span
            v-if="unreadCount > 0"
            class="absolute -top-1 -right-1 bg-red-500 text-white text-xs rounded-full w-5 h-5 flex items-center justify-center"
          >
            {{ unreadCount > 99 ? '99+' : unreadCount }}
          </span>
        </div>
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

          <div
            v-if="notifications.length === 0"
            class="p-4 text-center text-gray-500 text-sm"
          >
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
              <span
                v-else
                class="w-2 flex-shrink-0"
              />
              <div class="min-w-0">
                <p class="text-sm font-medium truncate">
                  {{ notif.title }}
                </p>
                <p
                  v-if="notif.body"
                  class="text-xs text-gray-500 truncate"
                >
                  {{ notif.body }}
                </p>
                <p class="text-xs text-gray-400 mt-0.5">
                  {{ timeAgo(notif.createdAt) }}
                </p>
              </div>
            </div>
          </div>
        </div>
      </template>
    </UPopover>
  </ClientOnly>
</template>
