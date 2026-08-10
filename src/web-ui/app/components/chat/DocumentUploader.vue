<script setup lang="ts">
import { ApiRoutes } from '~/lib/routes'
import type { DocumentDto } from '~/types/chat'

const emit = defineEmits<{
  uploaded: [document: DocumentDto]
}>()

const config = useRuntimeConfig()
const toast = useAppToast()
const { getToken } = useAuthToken()

type UploadState = 'idle' | 'uploading' | 'processing' | 'done'

const uploadState = ref<UploadState>('idle')
const isDragging = ref(false)
const fileInput = ref<HTMLInputElement>()

const ACCEPTED_TYPES = [
  'text/plain',
  'text/markdown',
  'text/csv',
  'text/html',
  'application/pdf'
]
const ACCEPTED_EXTENSIONS = '.txt,.md,.markdown,.csv,.ts,.js,.py,.rs,.go,.java,.c,.cpp,.h,.sh,.bash,.html,.htm'

const ACCEPTED_LABEL = 'Text, Markdown, Code, CSV, HTML'

function validateFile(file: File): string | null {
  const typeAccepted = ACCEPTED_TYPES.includes(file.type)
    || file.type.startsWith('text/')
    || file.name.match(/\.(txt|md|markdown|csv|ts|js|py|rs|go|java|c|cpp|h|sh|bash|html|htm)$/i)

  if (!typeAccepted) {
    return 'Unsupported file type. Please upload a text, markdown, code, CSV, or HTML file.'
  }

  // 50 MB max (server-side limit; reasonable client-side guard)
  const MAX_SIZE = 50 * 1024 * 1024
  if (file.size > MAX_SIZE) {
    const mb = (MAX_SIZE / 1024 / 1024).toFixed(0)
    return `File too large. Maximum size is ${mb} MB.`
  }

  return null
}

async function uploadFile(file: File) {
  const validationError = validateFile(file)
  if (validationError) {
    toast.error(validationError)
    return
  }

  uploadState.value = 'uploading'
  try {
    const formData = new FormData()
    formData.append('file', file)
    formData.append('title', file.name)
    formData.append('contentType', file.type || 'text/plain')

    const token = getToken()
    const res = await fetch(`${config.public.apiBaseUrl}${ApiRoutes.Chat.documents.create()}`, {
      method: 'POST',
      headers: token ? { Authorization: `Bearer ${token}` } : {},
      body: formData
    })

    if (!res.ok) {
      let msg = `Upload failed (${res.status})`
      try {
        const body = await res.json()
        msg = (body as Record<string, unknown>).detail as string || msg
      } catch { /* ignore parse failure */ }
      throw new Error(msg)
    }

    uploadState.value = 'processing'

    const doc = await res.json() as DocumentDto

    // Give the user a moment to see "Chunking & embedding…"
    await new Promise(resolve => setTimeout(resolve, 800))

    uploadState.value = 'done'
    toast.success(`"${doc.title}" uploaded`)
    emit('uploaded', doc)

    await new Promise(resolve => setTimeout(resolve, 1500))
    uploadState.value = 'idle'
  } catch (err: unknown) {
    uploadState.value = 'idle'
    const msg = err instanceof Error ? err.message : 'Failed to upload document'
    toast.error(msg)
  }
}

function handleFileSelect(e: Event) {
  const input = e.target as HTMLInputElement
  const file = input.files?.[0]
  if (file) {
    void uploadFile(file)
  }
  input.value = ''
}

function openPicker() {
  fileInput.value?.click()
}

function onDragOver(e: DragEvent) {
  e.preventDefault()
  isDragging.value = true
}

function onDragLeave() {
  isDragging.value = false
}

function onDrop(e: DragEvent) {
  e.preventDefault()
  isDragging.value = false
  const file = e.dataTransfer?.files?.[0]
  if (file) {
    void uploadFile(file)
  }
}

function onKeyDown(e: KeyboardEvent) {
  if (e.key === 'Enter' || e.key === ' ') {
    e.preventDefault()
    openPicker()
  }
}
</script>

<template>
  <div class="space-y-2">
    <!-- Hidden file input -->
    <input
      ref="fileInput"
      type="file"
      class="sr-only"
      :accept="ACCEPTED_EXTENSIONS + ',.pdf'"
      tabindex="-1"
      @change="handleFileSelect"
    >

    <!-- Drop zone -->
    <div
      class="relative flex flex-col items-center justify-center gap-3 rounded-lg border-2 border-dashed border-muted transition-colors cursor-pointer focus-within:border-primary focus-within:ring-2 focus-within:ring-primary/20"
      :class="{
        'border-primary bg-primary/5': isDragging,
        'hover:border-muted-foreground/40': !isDragging && uploadState === 'idle',
        'opacity-60 pointer-events-none': uploadState !== 'idle'
      }"
      role="button"
      tabindex="0"
      :aria-label="`Upload document — accepts ${ACCEPTED_LABEL}`"
      @click="openPicker"
      @keydown="onKeyDown"
      @dragover="onDragOver"
      @dragleave="onDragLeave"
      @drop="onDrop"
    >
      <!-- Idle state -->
      <template v-if="uploadState === 'idle'">
        <UIcon
          name="i-lucide-upload-cloud"
          class="size-8 text-muted"
        />
        <div class="text-center">
          <p class="text-sm font-medium text-foreground">
            Drop a file here, or <span class="text-primary">browse</span>
          </p>
          <p class="text-xs text-muted mt-0.5">
            {{ ACCEPTED_LABEL }} — max 50 MB
          </p>
        </div>
      </template>

      <!-- Uploading state -->
      <template v-else-if="uploadState === 'uploading'">
        <UIcon
          name="i-lucide-loader-circle"
          class="size-8 animate-spin text-primary"
        />
        <p class="text-sm font-medium text-foreground">
          Uploading…
        </p>
      </template>

      <!-- Processing state -->
      <template v-else-if="uploadState === 'processing'">
        <UIcon
          name="i-lucide-loader-circle"
          class="size-8 animate-spin text-primary"
        />
        <p class="text-sm font-medium text-foreground">
          Chunking & embedding…
        </p>
      </template>

      <!-- Done state -->
      <template v-else-if="uploadState === 'done'">
        <UIcon
          name="i-lucide-check-circle"
          class="size-8 text-success"
        />
        <p class="text-sm font-medium text-foreground">
          Done
        </p>
      </template>
    </div>
  </div>
</template>
