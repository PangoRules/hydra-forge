<script setup lang="ts">
const { logout, isAuthenticated } = useAuth()
</script>

<template>
  <UApp>
    <Toaster />
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
        <UButton
          v-if="isAuthenticated"
          label="Logout"
          color="neutral"
          variant="ghost"
          @click="logout"
        />
      </template>
    </UHeader>

    <UMain>
      <slot />
    </UMain>
  </UApp>
</template>
