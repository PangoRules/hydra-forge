<script setup lang="ts">
import SessionExpiryModal from '~/components/shared/SessionExpiryModal.vue'
import AppSidebar from '~/components/layout/AppSidebar.vue'
import AppTopbar from '~/components/layout/AppTopbar.vue'
import ChatDock from '~/components/chat/ChatDock.vue'

const { logout, isAuthenticated, checkAuth, listenForAuthChanges } = useAuth()
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
    <UDashboardGroup class="flex-1 overflow-hidden">
      <AppSidebar />

      <UDashboardPanel class="flex-1 flex flex-col overflow-hidden">
        <template #header>
          <AppTopbar />
        </template>

        <template #body>
          <slot />
        </template>
      </UDashboardPanel>
    </UDashboardGroup>

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

    <ChatDock />
  </UApp>
</template>
