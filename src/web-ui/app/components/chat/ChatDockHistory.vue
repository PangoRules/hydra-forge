<script setup lang="ts">
import { useChatSessionList } from '~/composables/useChatSessionList'
import ChatSessionList from '~/components/shared/ChatSessionList.vue'

const emit = defineEmits<{
  select: [sessionId: string]
  back: []
}>()

const { sessions, loading, hasMore, loadMore } = useChatSessionList()
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
    <ChatSessionList
      :sessions="sessions"
      :loading="loading"
      :has-more="hasMore"
      @select="emit('select', $event)"
      @load-more="loadMore"
    >
      <template #item="{ session }">
        <div
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
      </template>
    </ChatSessionList>
  </div>
</template>
