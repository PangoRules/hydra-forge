<script setup lang="ts">
import type { ChatMessageDto } from '~/types/chat'
import { getInitials, getModelIcon } from '~/lib/chat-avatar'
import { renderMarkdown } from '~/lib/markdown'
import { highlightHtml } from '~/lib/highlight-html'

const props = withDefaults(
  defineProps<{
    message: ChatMessageDto
    isStreaming?: boolean
    rollbackDisabled?: boolean
    highlighted?: boolean
    findQuery?: string
  }>(),
  {
    isStreaming: false,
    rollbackDisabled: false,
    highlighted: false,
    findQuery: ''
  }
)

const emit = defineEmits<{
  rollback: [message: ChatMessageDto]
}>()

const authStore = useAuthStore()
const toast = useAppToast()
const userInitials = computed(() => getInitials(authStore.user?.username, 'U'))
const modelIcon = computed(() => getModelIcon(props.message.modelName))
const modelInitials = computed(() => getInitials(props.message.modelName, 'AI'))

interface ParsedImage {
  url: string
  base64?: string
}

const images = computed<ParsedImage[]>(() => {
  if (!props.message.imagesJson) return []
  try {
    return JSON.parse(props.message.imagesJson) as ParsedImage[]
  } catch {
    return []
  }
})

const renderedContent = computed(() => renderMarkdown(props.message.content))

const displayedContent = computed(() =>
  props.findQuery ? highlightHtml(renderedContent.value, props.findQuery) : renderedContent.value
)

const isUser = computed(() => props.message.role === 'User')

function getImageSrc(img: ParsedImage): string {
  if (img.base64) {
    if (img.base64.startsWith('data:')) return img.base64
    const ext = img.url.split('.').pop()?.toLowerCase()
    const mimeMap: Record<string, string> = {
      jpg: 'image/jpeg',
      jpeg: 'image/jpeg',
      png: 'image/png',
      webp: 'image/webp',
      gif: 'image/gif'
    }
    const mime = ext ? (mimeMap[ext] ?? 'image/png') : 'image/png'
    return `data:${mime};base64,${img.base64}`
  }
  return img.url
}

const copied = ref(false)

async function copyMessage() {
  try {
    await navigator.clipboard.writeText(props.message.content)
    copied.value = true
    setTimeout(() => {
      copied.value = false
    }, 1500)
  } catch {
    toast.error('Failed to copy message')
  }
}
</script>

<template>
  <div
    :id="`chat-message-${message.id}`"
    class="group flex gap-3 rounded-lg"
    :class="[isUser ? 'flex-row-reverse' : 'flex-row', highlighted ? 'ring-2 ring-primary' : '']"
  >
    <!-- Avatar -->
    <UAvatar
      :text="isUser ? userInitials : (modelIcon ? undefined : modelInitials)"
      :icon="isUser ? undefined : (modelIcon ?? undefined)"
      :class="isUser ? 'bg-primary text-white' : 'bg-gray-300 dark:bg-gray-600'"
      size="sm"
      class="shrink-0 mt-0.5"
    />

    <div
      class="flex flex-col gap-1 max-w-[80%]"
      :class="isUser ? 'items-end' : 'items-start'"
    >
      <!-- Image thumbnails for vision messages -->
      <div
        v-if="images.length > 0"
        class="flex flex-wrap gap-2 mb-1"
        :class="isUser ? 'justify-end' : 'justify-start'"
      >
        <a
          v-for="(img, idx) in images"
          :key="idx"
          :href="img.url"
          target="_blank"
          rel="noopener"
          class="block"
        >
          <img
            :src="getImageSrc(img)"
            class="h-20 w-20 object-cover rounded-md border border-gray-200 dark:border-gray-700 hover:opacity-90 transition-opacity"
            alt="Attached image"
          >
        </a>
      </div>

      <!-- Message bubble -->
      <!-- eslint-disable vue/no-v-html -- renderedContent is DOMPurify-sanitized above -->
      <div
        class="px-4 py-2 rounded-2xl text-sm leading-relaxed"
        :class="[
          isUser
            ? 'bg-primary text-white rounded-tr-sm'
            : 'bg-gray-100 dark:bg-gray-800 text-gray-900 dark:text-gray-100 rounded-tl-sm',
          isStreaming ? 'animate-pulse' : ''
        ]"
        v-html="displayedContent"
      />
      <!-- eslint-enable vue/no-v-html -->

      <!-- Timestamp (+ model name for assistant replies) + rollback action -->
      <div
        class="flex items-center gap-1 px-1"
        :class="isUser ? 'flex-row-reverse' : 'flex-row'"
      >
        <span class="text-xs text-muted">
          {{ new Date(message.createdAt).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }) }}
          <template v-if="!isUser && message.modelName">
            · {{ message.modelName }}
          </template>
        </span>
        <UButton
          :icon="copied ? 'i-lucide-check' : 'i-lucide-copy'"
          title="Copy message"
          variant="ghost"
          color="neutral"
          size="xs"
          class="opacity-0 group-hover:opacity-100 transition-opacity"
          @click="copyMessage"
        />
        <UButton
          :icon="isUser ? 'i-lucide-pencil' : 'i-lucide-rotate-ccw'"
          :title="isUser ? 'Edit and resend' : 'Regenerate'"
          variant="ghost"
          color="neutral"
          size="xs"
          :disabled="rollbackDisabled"
          class="opacity-0 group-hover:opacity-100 transition-opacity"
          @click="emit('rollback', message)"
        />
      </div>
    </div>
  </div>
</template>
