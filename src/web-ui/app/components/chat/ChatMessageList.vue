<script setup lang="ts">
import type { ChatMessageDto } from '~/types/chat'
import type { StreamingMessage } from '~/composables/useChatStream'

const props = withDefaults(
  defineProps<{
    messages: ChatMessageDto[]
    streamingMessage?: StreamingMessage | null
  }>(),
  {
    streamingMessage: null
  }
)

const listRef = ref<HTMLElement | null>(null)

watch(
  () => props.messages.length,
  () => {
    nextTick(() => {
      if (listRef.value) {
        listRef.value.scrollTop = listRef.value.scrollHeight
      }
    })
  }
)

watch(
  () => props.streamingMessage?.content,
  () => {
    nextTick(() => {
      if (listRef.value) {
        listRef.value.scrollTop = listRef.value.scrollHeight
      }
    })
  }
)
</script>

<template>
  <div
    ref="listRef"
    class="flex-1 overflow-y-auto px-4 py-4 space-y-4"
  >
    <template v-if="messages.length === 0 && !streamingMessage">
      <div class="text-center text-muted text-sm py-12">
        No messages yet. Start the conversation!
      </div>
    </template>

    <template v-else>
      <ChatMessageBubble
        v-for="message in messages"
        :key="message.id"
        :message="message"
        :is-streaming="streamingMessage?.messageId === message.id"
      />

      <!-- Live typing bubble for in-flight assistant response -->
      <div
        v-if="streamingMessage"
        class="flex gap-3"
      >
        <UAvatar
          name="AI"
          class="bg-gray-300 dark:bg-gray-600 shrink-0 mt-0.5"
          size="sm"
        />
        <div class="flex flex-col gap-1 max-w-[80%]">
          <div
            class="px-4 py-2 rounded-2xl rounded-tl-sm bg-gray-100 dark:bg-gray-800 text-gray-900 dark:text-gray-100 text-sm leading-relaxed"
          >
            <span v-if="streamingMessage.content">{{ streamingMessage.content }}</span>
            <span
              v-else
              class="inline-flex gap-1"
            >
              <span
                v-for="i in 3"
                :key="i"
                class="w-2 h-2 bg-gray-400 rounded-full animate-bounce"
                :style="{ animationDelay: `${(i - 1) * 150}ms` }"
              />
            </span>
          </div>
        </div>
      </div>
    </template>
  </div>
</template>
