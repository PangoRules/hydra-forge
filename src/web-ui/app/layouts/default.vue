<script setup lang="ts">
import SessionExpiryModal from '~/components/shared/SessionExpiryModal.vue'
import NotificationPanel from '~/components/notifications/NotificationPanel.vue'

const { logout, isAuthenticated, checkAuth, listenForAuthChanges } = useAuth()
const authStore = useAuthStore()
const { fetchUnreadCount } = useNotifications()
const notificationHub = useNotificationHub()
const {
  isExpired,
  isExpiringSoon,
  isExtending,
  timeRemaining,
  remainingFormatted,
  extendSession,
  start: startSessionManager,
  stop: stopSessionManager
} = useSessionManager()

// Restore session from cookie immediately during setup — before any page
// mounts or API calls fire. onMounted is too late: the page's onMounted
// (which calls fetchBoard) fires right after the layout's onMounted.
checkAuth()
listenForAuthChanges()

onMounted(() => {
  startSessionManager()
  if (isAuthenticated) {
    fetchUnreadCount()
    notificationHub.connect()
  }
})
onUnmounted(() => {
  stopSessionManager()
  notificationHub.disconnect()
})

const showSessionModal = computed(() => isExpiringSoon.value || isExpired.value)

function handleExtend() {
  extendSession()
}

function handleSessionLogout() {
  logout()
}
</script>

<template>
  <UApp
    :toaster="{ position: 'bottom-right', duration: 5000 }"
    class="h-full flex flex-col overflow-hidden"
  >
    <UHeader>
      <template #left>
        <NuxtLink
          to="/projects"
          class="flex items-center gap-2"
        >
          <span class="text-lg font-bold">HydraForge</span>
        </NuxtLink>
      </template>

      <template #right>
        <ClientOnly>
          <NotificationPanel v-if="isAuthenticated" />
        </ClientOnly>
        <UColorModeButton />
        <ClientOnly>
          <span
            v-if="isAuthenticated && authStore.user"
            class="text-sm text-muted mr-2"
          >{{ authStore.user.username }}</span>
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

    <ClientOnly>
      <SessionExpiryModal
        :open="showSessionModal"
        :expired="isExpired"
        :time-remaining="timeRemaining"
        :remaining-formatted="remainingFormatted"
        :extending="isExtending"
        @extend="handleExtend"
        @logout="handleSessionLogout"
      />
    </ClientOnly>
  </UApp>
</template>
