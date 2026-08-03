<script setup lang="ts">
import { marked, Renderer } from 'marked'
import type { ChatMessageDto } from '~/types/chat'

const props = defineProps<{
  message: ChatMessageDto
  isStreaming?: boolean
}>()

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

const renderedContent = computed(() => {
  if (!props.message.content) return ''
  // Strip raw HTML by providing a no-op html renderer
  const renderer = new Renderer()
  renderer.html = () => ''
  let html = marked.parse(props.message.content, { async: false, breaks: true, renderer }) as string
  // Strip javascript: and data: link protocols to prevent XSS
  html = html.replace(/href=["'](?:javascript|data):/gi, 'href="#blocked-')
  return html
})

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
</script>

<template>
  <div
    class="flex gap-3"
    :class="isUser ? 'flex-row-reverse' : 'flex-row'"
  >
    <!-- Avatar -->
    <UAvatar
      :name="isUser ? 'You' : 'AI'"
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
      <div
        class="px-4 py-2 rounded-2xl text-sm leading-relaxed"
        :class="[
          isUser
            ? 'bg-primary text-white rounded-tr-sm'
            : 'bg-gray-100 dark:bg-gray-800 text-gray-900 dark:text-gray-100 rounded-tl-sm',
          isStreaming ? 'animate-pulse' : ''
        ]"
        v-html="renderedContent"
      />

      <!-- Timestamp -->
      <span class="text-xs text-muted px-1">
        {{ new Date(message.createdAt).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }) }}
      </span>
    </div>
  </div>
</template>
