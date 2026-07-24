<script setup lang="ts">
import AppModal from '~/components/shared/AppModal.vue'

const props = defineProps<{
  open: boolean
  expired: boolean
  timeRemaining: number
  remainingFormatted: string
  extending: boolean
}>()

const emit = defineEmits<{
  extend: []
  logout: []
}>()

// Auto-redirect after 10s of expired state
let autoTimer: ReturnType<typeof setTimeout> | null = null

watch(() => props.expired, (now) => {
  if (now) {
    autoTimer = setTimeout(() => emit('logout'), 10_000)
  } else {
    if (autoTimer !== null) {
      clearTimeout(autoTimer)
      autoTimer = null
    }
  }
})

onUnmounted(() => {
  if (autoTimer !== null) clearTimeout(autoTimer)
})
</script>

<template>
  <AppModal
    :open="open"
    :title="expired ? 'Session expired' : 'Session expiring soon'"
    width="sm:max-w-sm"
    :show-close="false"
    @update:open="$emit('logout')"
  >
    <template #body>
      <div class="space-y-4 p-1 text-center">
        <!-- Icon -->
        <div
          class="mx-auto size-12 rounded-full flex items-center justify-center"
          :class="expired ? 'bg-red-100 dark:bg-red-900/30' : 'bg-amber-100 dark:bg-amber-900/30'"
        >
          <UIcon
            v-if="expired"
            name="i-lucide-alarm-clock-off"
            class="size-6 text-red-500"
          />
          <UIcon
            v-else
            name="i-lucide-alarm-clock"
            class="size-6 text-amber-500"
          />
        </div>

        <p class="text-sm text-muted">
          <template v-if="expired">
            Your session has expired. You'll be redirected to the login page shortly.
          </template>
          <template v-else>
            Your session will expire in <strong class="text-amber-600 dark:text-amber-400">{{ remainingFormatted }}</strong>. Extend it to continue working without interruption.
          </template>
        </p>

        <!-- Expired: countdown bar -->
        <div
          v-if="expired"
          class="w-full bg-gray-200 dark:bg-gray-700 rounded-full h-1.5 overflow-hidden"
        >
          <div
            class="h-full bg-red-500 rounded-full transition-all duration-1000 ease-linear"
            :style="{ width: `${Math.min(100, (timeRemaining > 0 ? timeRemaining : 0) / 10 * 100)}%` }"
          />
        </div>
      </div>
    </template>

    <template #footer>
      <div class="flex justify-center gap-3 w-full">
        <UButton
          v-if="expired"
          color="primary"
          @click="$emit('logout')"
        >
          Go to Login
        </UButton>

        <template v-else>
          <UButton
            variant="ghost"
            color="neutral"
            @click="$emit('logout')"
          >
            Logout
          </UButton>
          <UButton
            color="primary"
            :loading="extending"
            :disabled="extending"
            @click="$emit('extend')"
          >
            Extend Session
          </UButton>
        </template>
      </div>
    </template>
  </AppModal>
</template>
