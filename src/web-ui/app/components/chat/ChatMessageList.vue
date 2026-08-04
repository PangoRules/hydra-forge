<script setup lang="ts">
import type { ChatMessageDto } from '~/types/chat'
import type { StreamingMessage } from '~/composables/useChatStream'
import { getInitials, getModelIcon } from '~/lib/chat-avatar'

const MESSAGE_WINDOW_SIZE = 50

const props = withDefaults(
  defineProps<{
    messages: ChatMessageDto[]
    streamingMessage?: StreamingMessage | null
    streamError?: string | null
  }>(),
  {
    streamingMessage: null,
    streamError: null
  }
)

const streamingModelIcon = computed(() => getModelIcon(props.streamingMessage?.modelName))
const streamingModelInitials = computed(() => getInitials(props.streamingMessage?.modelName, 'AI'))

const listRef = ref<HTMLElement | null>(null)
const sentinelRef = ref<HTMLElement | null>(null)
const windowStart = ref(0)
const isAtBottom = ref(true)
const isAtTop = ref(false)

const visibleMessages = computed(() => {
  const start = Math.max(0, props.messages.length - MESSAGE_WINDOW_SIZE - windowStart.value)
  const end = props.messages.length
  return props.messages.slice(start, end)
})

const hasOlderMessages = computed(() => windowStart.value < props.messages.length - MESSAGE_WINDOW_SIZE)

watch(
  () => props.messages.length,
  () => {
    nextTick(() => {
      if (listRef.value) {
        if (isAtBottom.value) {
          listRef.value.scrollTop = listRef.value.scrollHeight
        }
      }
    })
  }
)

watch(
  () => props.streamingMessage?.content,
  () => {
    nextTick(() => {
      if (listRef.value) {
        if (isAtBottom.value) {
          listRef.value.scrollTop = listRef.value.scrollHeight
        }
      }
    })
  }
)

function onScroll() {
  if (!listRef.value) return
  const { scrollTop, scrollHeight, clientHeight } = listRef.value
  isAtBottom.value = scrollHeight - scrollTop - clientHeight < 50
  isAtTop.value = scrollTop < 50

  if (scrollTop < 100 && hasOlderMessages.value) {
    const prevScrollHeight = listRef.value.scrollHeight
    const prevScrollTop = listRef.value.scrollTop
    windowStart.value = Math.min(
      windowStart.value + MESSAGE_WINDOW_SIZE,
      props.messages.length - MESSAGE_WINDOW_SIZE
    )
    nextTick(() => {
      if (listRef.value) {
        listRef.value.scrollTop = listRef.value.scrollHeight - prevScrollHeight + prevScrollTop
      }
    })
  }
}

function scrollToBottom() {
  if (!listRef.value) return
  listRef.value.scrollTo({ top: listRef.value.scrollHeight, behavior: 'smooth' })
  isAtBottom.value = true
  isAtTop.value = false
}

function scrollToTop() {
  if (!listRef.value) return
  listRef.value.scrollTo({ top: 0, behavior: 'smooth' })
  isAtTop.value = true
  isAtBottom.value = false
}

onMounted(() => {
  // Land on the last message, not the top — a chat you're reopening is read
  // bottom-up in practice, same as every other chat app.
  nextTick(() => {
    if (listRef.value) {
      listRef.value.scrollTop = listRef.value.scrollHeight
      isAtBottom.value = true
      isAtTop.value = listRef.value.scrollHeight <= listRef.value.clientHeight
    }
  })

  if (sentinelRef.value) {
    const observer = new IntersectionObserver(
      (entries) => {
        const entry = entries[0]
        if (entry?.isIntersecting && hasOlderMessages.value && listRef.value) {
          const prevScrollHeight = listRef.value.scrollHeight
          const prevScrollTop = listRef.value.scrollTop
          windowStart.value = Math.min(
            windowStart.value + MESSAGE_WINDOW_SIZE,
            props.messages.length - MESSAGE_WINDOW_SIZE
          )
          nextTick(() => {
            if (listRef.value) {
              listRef.value.scrollTop = listRef.value.scrollHeight - prevScrollHeight + prevScrollTop
            }
          })
        }
      },
      { root: listRef.value }
    )
    observer.observe(sentinelRef.value)
    onUnmounted(() => observer.disconnect())
  }
})
</script>

<template>
  <div class="relative flex-1 min-h-0 flex flex-col">
    <div
      ref="listRef"
      class="flex-1 overflow-y-auto px-4 py-4 space-y-4"
      @scroll="onScroll"
    >
      <template v-if="messages.length === 0 && !streamingMessage">
        <div class="text-center text-muted text-sm py-12">
          No messages yet. Start the conversation!
        </div>
      </template>

      <template v-else>
        <!-- Sentinel/anchor for older messages trigger -->
        <div
          v-if="hasOlderMessages"
          ref="sentinelRef"
          class="h-1 w-full"
        />

        <ChatMessageBubble
          v-for="message in visibleMessages"
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
            :text="streamingModelIcon ? undefined : streamingModelInitials"
            :icon="streamingModelIcon ?? undefined"
            class="bg-gray-300 dark:bg-gray-600 shrink-0 mt-0.5"
            size="sm"
          />
          <div class="flex flex-col gap-1 max-w-[80%]">
            <span
              v-if="streamingMessage.modelName"
              class="text-xs text-muted px-1"
            >
              {{ streamingMessage.modelName }}
            </span>
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

        <!-- Inline error bubble for a turn that failed to stream -->
        <div
          v-else-if="streamError"
          class="flex gap-3"
        >
          <UAvatar
            icon="i-lucide-alert-triangle"
            class="bg-error/10 text-error shrink-0 mt-0.5"
            size="sm"
          />
          <div class="flex flex-col gap-1 max-w-[80%]">
            <div class="px-4 py-2 rounded-2xl rounded-tl-sm bg-error/10 text-error text-sm leading-relaxed">
              {{ streamError }}
            </div>
          </div>
        </div>
      </template>
    </div>

    <UButton
      v-if="!isAtTop"
      icon="i-lucide-arrow-up"
      color="neutral"
      variant="solid"
      size="sm"
      class="absolute top-4 right-4 rounded-full shadow-lg"
      @click="scrollToTop"
    />

    <UButton
      v-if="!isAtBottom"
      icon="i-lucide-arrow-down"
      color="neutral"
      variant="solid"
      size="sm"
      class="absolute bottom-4 right-4 rounded-full shadow-lg"
      @click="scrollToBottom"
    />
  </div>
</template>
