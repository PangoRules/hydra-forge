<script setup lang="ts">
import { ApiRoutes } from '~/lib/routes'

interface AttachmentResponse {
  id: string
  cardId: string
  fileName: string
  contentType: string
  size: number
  createdAt: string
}

const props = defineProps<{
  cardId: string
  projectId: string
}>()

const attachments = ref<AttachmentResponse[]>([])
const loading = ref(true)
const uploading = ref(false)

const api = useApi()
const config = useRuntimeConfig()
const toast = useToast()
const fileInput = ref<HTMLInputElement>()
const { getToken } = useAuthToken()

async function fetchAttachments() {
  loading.value = true
  try {
    const { data, error } = await api.GET(ApiRoutes.Attachments.list(props.projectId, props.cardId))
    if (error) throw error
    attachments.value = (data as { attachments: AttachmentResponse[] })?.attachments ?? []
  } catch {
    toast.add({ title: 'Failed to load attachments', color: 'error' })
  } finally {
    loading.value = false
  }
}

async function handleUpload(e: Event) {
  const input = e.target as HTMLInputElement
  const file = input.files?.[0]
  if (!file) return

  uploading.value = true
  try {
    const formData = new FormData()
    formData.append('file', file)

    const token = getToken()
    const res = await fetch(`${config.public.apiBaseUrl}${ApiRoutes.Attachments.upload(props.projectId, props.cardId)}`, {
      method: 'POST',
      headers: token ? { Authorization: `Bearer ${token}` } : {},
      body: formData
    })
    if (!res.ok) throw new Error('Upload failed')
    const data = await res.json() as AttachmentResponse
    attachments.value.push(data)
  } catch {
    toast.add({ title: 'Failed to upload attachment', color: 'error' })
  } finally {
    uploading.value = false
    input.value = ''
  }
}

async function deleteAttachment(attachmentId: string) {
  try {
    await api.DELETE(ApiRoutes.Attachments.delete(props.projectId, props.cardId, attachmentId))
    attachments.value = attachments.value.filter(a => a.id !== attachmentId)
  } catch {
    toast.add({ title: 'Failed to delete attachment', color: 'error' })
  }
}

function downloadUrl(attachment: AttachmentResponse) {
  return `${config.public.apiBaseUrl}${ApiRoutes.Attachments.download(props.projectId, props.cardId, attachment.id)}`
}

function formatSize(bytes: number) {
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`
}

onMounted(() => fetchAttachments())
</script>

<template>
  <div class="space-y-2">
    <div class="flex items-center justify-between">
      <p class="text-xs font-medium text-muted uppercase">
        Attachments
      </p>
      <UButton
        size="xs"
        variant="outline"
        :loading="uploading"
        @click="fileInput?.click()"
      >
        Upload
      </UButton>
      <input
        ref="fileInput"
        type="file"
        class="hidden"
        @change="handleUpload"
      >
    </div>

    <div class="space-y-1 max-h-48 overflow-y-auto">
      <div
        v-if="loading"
        class="text-xs text-muted"
      >
        Loading...
      </div>
      <div
        v-else-if="attachments.length === 0"
        class="text-xs text-muted"
      >
        No attachments
      </div>

      <div
        v-for="att in attachments"
        :key="att.id"
        class="flex items-center gap-2 group text-sm"
      >
        <UIcon
          name="i-lucide-paperclip"
          class="size-3 text-muted"
        />
        <a
          :href="downloadUrl(att)"
          class="flex-1 truncate hover:underline"
          download
        >
          {{ att.fileName }}
        </a>
        <span class="text-xs text-muted">{{ formatSize(att.size) }}</span>
        <UButton
          icon="i-lucide-trash-2"
          variant="ghost"
          size="xs"
          color="neutral"
          class="opacity-0 group-hover:opacity-100"
          @click="deleteAttachment(att.id)"
        />
      </div>
    </div>
  </div>
</template>
