<script setup lang="ts">
import type { ChatSessionDto } from '~/types/chat'

const props = defineProps<{
  sessions: ChatSessionDto[]
  loading: boolean
  hasMore: boolean
  activeSessionId?: string | null
}>()

const emit = defineEmits<{
  select: [sessionId: string]
  loadMore: []
}>()

const sentinel = ref<HTMLElement | null>(null)
const observer = ref<IntersectionObserver | null>(null)

onMounted(() => {
  observer.value = new IntersectionObserver(
    ([entry]) => {
      if (entry?.isIntersecting && !props.loading && props.hasMore) {
        emit('loadMore')
      }
    },
    { rootMargin: '100px' }
  )
})

// watch the sentinel ref reactively — the sentinel may render after mount
// (v-if block), so onMounted's direct observe may miss it
watch(sentinel, (el) => {
  if (el && observer.value) {
    observer.value.observe(el)
  }
})

onUnmounted(() => {
  observer.value?.disconnect()
})
</script>

<template>
  <div class="flex-1 overflow-y-auto">
    <!-- Initial loading state (no items yet) -->
    <div
      v-if="loading && sessions.length === 0"
      class="flex items-center justify-center py-8"
    >
      <UIcon name="i-lucide-loader-2" class="animate-spin text-muted size-5" />
    </div>

    <!-- Empty state -->
    <div
      v-else-if="!loading && sessions.length === 0"
      class="px-4 py-8 text-sm text-muted text-center"
    >
      <slot name="empty">No conversations yet.</slot>
    </div>

    <!-- Session items + sentinel + footer -->
    <template v-else>
      <template
        v-for="session in sessions"
        :key="session.id"
      >
        <slot
          name="item"
          :session="session"
          :active="session.id === activeSessionId"
        />
      </template>

      <!-- Sentinel for infinite scroll -->
      <div
        v-if="hasMore"
        ref="sentinel"
        class="h-4 shrink-0"
      />

      <!-- Loading more spinner -->
      <div
        v-if="loading"
        class="flex items-center justify-center py-3"
      >
        <UIcon name="i-lucide-loader-2" class="animate-spin text-muted size-5" />
      </div>

      <!-- All caught up footer -->
      <div
        v-if="!hasMore && sessions.length > 0"
        class="px-4 py-3 text-sm text-muted text-center"
      >
        All caught up
      </div>
    </template>
  </div>
</template>
