<script setup lang="ts">
import { getNavGroups } from '~/lib/nav-config'
import { useSidebarCollapse } from '~/composables/useSidebarCollapse'
import { UiRoutes } from '~/lib/routes'

const authStore = useAuthStore()
const { collapsed } = useSidebarCollapse()
const route = useRoute()

const navGroups = computed(() => getNavGroups(authStore.user?.isAdmin ?? false, route.path))
</script>

<template>
  <UDashboardSidebar
    v-model:collapsed="collapsed"
    :collapsible="true"
    :resizable="false"
    mode="slideover"
  >
    <template #header>
      <UButton
        :label="collapsed ? undefined : 'New Chat'"
        icon="i-lucide-plus"
        block
        :to="UiRoutes.Chats"
      />
    </template>

    <UNavigationMenu
      :items="navGroups"
      orientation="vertical"
      :collapsed="collapsed"
    />

    <template #footer>
      <UDashboardSidebarCollapse />
    </template>
  </UDashboardSidebar>
</template>
