<script setup lang="ts">
import SessionExpiryModal from '~/components/shared/SessionExpiryModal.vue'

const { logout, isAuthenticated, checkAuth } = useAuth()
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

onMounted(() => startSessionManager())
onUnmounted(() => stopSessionManager())

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
