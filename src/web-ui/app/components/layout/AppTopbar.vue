<script setup lang="ts">
import NotificationPanel from '~/components/notifications/NotificationPanel.vue'
import { UiRoutes } from '~/lib/routes'

const { logout, isAuthenticated } = useAuth()
const authStore = useAuthStore()

const userMenuItems = computed(() => [
  [{ label: authStore.user?.username ?? '', type: 'label' as const }],
  [
    { label: 'Usage', icon: 'i-lucide-bar-chart-3', to: '/account/usage' },
    { label: 'Logout', icon: 'i-lucide-log-out', onSelect: () => logout() }
  ]
])
</script>

<template>
  <UDashboardNavbar>
    <template #leading>
      <NuxtLink
        :to="UiRoutes.Chats"
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
        <UDropdownMenu
          v-if="isAuthenticated"
          :items="userMenuItems"
        >
          <UButton
            :label="authStore.user?.username"
            trailing-icon="i-lucide-chevron-down"
            color="neutral"
            variant="ghost"
          />
        </UDropdownMenu>
      </ClientOnly>
    </template>
  </UDashboardNavbar>
</template>
