<script setup lang="ts">
import { useChatSessionList } from '~/composables/useChatSessionList'

const emit = defineEmits<{
  select: [sessionId: string]
  back: []
}>()

const { sessions, loading, hasMore, loadMore } = useChatSessionList()

const sentinel = ref<HTMLElement | null>(null)
const observer = ref<IntersectionObserver | null>(null)

onMounted(() => {
  observer.value = new IntersectionObserver(
    ([entry]) => {
      if (entry?.isIntersecting && !loading.value && hasMore.value) {
        loadMore()
      }
    },
    { rootMargin: '100px' }
  )
  if (sentinel.value) observer.value.observe(sentinel.value)
})

onUnmounted(() => {
  observer.value?.disconnect()
})
</script>

<template>
  <div class="flex-1 flex flex-col min-h-0">
    <div class="flex items-center justify-between px-4 py-2 border-b border-gray-200 dark:border-gray-700">
      <UButton
        icon="i-lucide-chevron-left"
        variant="ghost"
        size="xs"
        @click="emit('back')"
      >
        Back
      </UButton>
      <span class="text-sm font-medium">History</span>
      <div class="w-8" />
    </div>
    <div class="flex-1 overflow-y-auto">
      <div
        v-for="session in sessions"
        :key="session.id"
        class="px-4 py-3 border-b border-gray-100 dark:border-gray-800 hover:bg-gray-50 dark:hover:bg-gray-800 cursor-pointer"
        @click="emit('select', session.id)"
      >
        <div class="text-sm font-medium truncate">
          {{ session.title || 'New Chat' }}
        </div>
        <div class="text-xs text-muted truncate mt-0.5">
          {{ session.summary || 'No messages' }}
        </div>
      </div>
      <div
        v-if="loading"
        class="px-4 py-3 text-sm text-muted text-center"
      >
        Loading...
      </div>
      <div
        v-if="hasMore"
        ref="sentinel"
        class="h-4"
      />
      <div
        v-if="!hasMore && sessions.length > 0"
        class="px-4 py-3 text-sm text-muted text-center"
      >
        All caught up
      </div>
      <div
        v-if="!loading && sessions.length === 0"
        class="px-4 py-8 text-sm text-muted text-center"
      >
        No conversations yet.
      </div>
    </div>
  </div>
</template>
