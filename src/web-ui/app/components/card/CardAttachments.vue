<script setup lang="ts">
import { ApiRoutes } from '~/lib/routes'
import ConfirmDialog from '~/components/shared/ConfirmDialog.vue'

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
  readonly?: boolean
}>()

const attachments = ref<AttachmentResponse[]>([])
const loading = ref(true)
const uploading = ref(false)

const api = useApi()
const config = useRuntimeConfig()
const toast = useAppToast()
const fileInput = ref<HTMLInputElement>()
const { getToken } = useAuthToken()

const deleteTarget = ref<AttachmentResponse | null>(null)
const showDeleteConfirm = computed({
  get: () => deleteTarget.value !== null,
  set: (v: boolean) => { if (!v) deleteTarget.value = null }
})
const deleting = ref(false)

async function fetchAttachments() {
  loading.value = true
  try {
    const { data } = await api.GET<{ attachments: AttachmentResponse[] }>(ApiRoutes.Attachments.list(props.projectId, props.cardId))
    attachments.value = data?.attachments ?? []
  } catch {
    toast.error('Failed to load attachments')
  } finally {
    loading.value = false
  }
}

const MAX_FILE_SIZE = 10_485_760 // 10 MB (matches server RequestSizeLimit)

async function handleUpload(e: Event) {
  const input = e.target as HTMLInputElement
  const file = input.files?.[0]
  if (!file) return

  if (file.size > MAX_FILE_SIZE) {
    const mb = (MAX_FILE_SIZE / 1024 / 1024).toFixed(0)
    toast.error(`File too large. Maximum size is ${mb} MB.`)
    input.value = ''
    return
  }

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
    if (!res.ok) {
      const text = await res.text().catch(() => '')
      throw new Error(text || `Upload failed (${res.status})`)
    }
    const data = await res.json() as AttachmentResponse
    attachments.value.push(data)
  } catch (err: unknown) {
    const msg = err instanceof Error ? err.message : 'Failed to upload attachment'
    toast.error(msg)
  } finally {
    uploading.value = false
    input.value = ''
  }
}

function promptDelete(att: AttachmentResponse) {
  deleteTarget.value = att
}

async function confirmDeleteAttachment() {
  const target = deleteTarget.value
  if (!target) return
  deleting.value = true
  try {
    await api.DELETE(ApiRoutes.Attachments.delete(props.projectId, props.cardId, target.id))
    attachments.value = attachments.value.filter(a => a.id !== target.id)
    deleteTarget.value = null
  } catch {
    toast.error('Failed to delete attachment')
  } finally {
    deleting.value = false
  }
}

function downloadUrl(attachment: AttachmentResponse) {
  return `${config.public.apiBaseUrl}${ApiRoutes.Attachments.download(props.projectId, props.cardId, attachment.id)}`
}

async function downloadAttachment(att: AttachmentResponse) {
  try {
    const token = getToken()
    const res = await fetch(downloadUrl(att), {
      headers: token ? { Authorization: `Bearer ${token}` } : {}
    })
    if (!res.ok) throw new Error('Download failed')
    const blob = await res.blob()
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = att.fileName
    a.click()
    URL.revokeObjectURL(url)
  } catch {
    toast.error('Failed to download file')
  }
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
        v-if="!readonly"
        size="xs"
        variant="outline"
        :loading="uploading"
        @click="fileInput?.click()"
      >
        Upload
      </UButton>
      <input
        v-if="!readonly"
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
        <button
          class="flex-1 truncate text-left underline md:decoration-transparent md:hover:decoration-current transition"
          title="Download"
          @click="downloadAttachment(att)"
        >
          {{ att.fileName }}
        </button>
        <span class="text-xs text-muted">{{ formatSize(att.size) }}</span>
        <UButton
          v-if="!readonly"
          icon="i-lucide-trash-2"
          variant="ghost"
          size="xs"
          color="neutral"
          class="md:opacity-0 md:group-hover:opacity-100"
          @click="promptDelete(att)"
        />
      </div>
    </div>
  </div>

  <ConfirmDialog
    v-model:open="showDeleteConfirm"
    title="Delete attachment"
    :message="deleteTarget ? `Delete ${deleteTarget.fileName}? This cannot be undone.` : ''"
    confirm-text="Delete"
    :loading="deleting"
    @confirm="confirmDeleteAttachment"
  />
</template>
