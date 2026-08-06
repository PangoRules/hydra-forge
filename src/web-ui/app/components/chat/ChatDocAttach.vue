<script setup lang="ts">
import { ApiRoutes } from '~/lib/routes'

interface AttachedDoc {
  documentId: string
  title: string
}

const props = defineProps<{
  sessionId: string
}>()

const emit = defineEmits<{
  attached: []
}>()

const api = useApi()
const toast = useAppToast()

const attachedDocs = ref<AttachedDoc[]>([])
const loading = ref(true)
const fetchError = ref<string | null>(null)
const showPicker = ref(false)
const expanded = ref(false)

async function fetchAttached() {
  loading.value = true
  fetchError.value = null
  try {
    const { data } = await api.GET<AttachedDoc[]>(
      ApiRoutes.Chat.sessions.listDocuments(props.sessionId)
    )
    attachedDocs.value = data ?? []
  } catch (err) {
    fetchError.value = err instanceof Error ? err.message : 'Failed to load documents'
    toast.error(fetchError.value)
  } finally {
    loading.value = false
  }
}

function dismissError() {
  fetchError.value = null
}

async function removeDoc(documentId: string) {
  const doc = attachedDocs.value.find(d => d.documentId === documentId)
  const label = doc?.title ?? 'document'
  try {
    await api.DELETE(ApiRoutes.Chat.sessions.detachDocument(props.sessionId, documentId))
    attachedDocs.value = attachedDocs.value.filter(d => d.documentId !== documentId)
    toast.success(`"${label}" removed`)
    emit('attached')
  } catch (err) {
    toast.error(err instanceof Error ? err.message : 'Failed to remove document')
  }
}

function handlePicked() {
  void fetchAttached()
  emit('attached')
}

onMounted(fetchAttached)
</script>

<template>
  <div class="space-y-2">
    <!-- Header row — summary toggles expand/collapse, + always reachable -->
    <div class="flex items-center justify-between gap-2 w-full">
      <button
        type="button"
        data-testid="doc-attach-toggle"
        class="flex items-center gap-1 text-xs font-medium text-muted uppercase text-left min-w-0 flex-1"
        @click="expanded = !expanded"
      >
        <UIcon
          :name="expanded ? 'i-lucide-chevron-down' : 'i-lucide-chevron-right'"
          class="size-3.5 shrink-0"
        />
        <span class="truncate">Attached docs ({{ attachedDocs.length }})</span>
      </button>
      <UButton
        icon="i-lucide-plus"
        variant="ghost"
        size="xs"
        title="Attach a document"
        @click="showPicker = true"
      />
    </div>

    <template v-if="expanded">
      <!-- Loading -->
      <div
        v-if="loading"
        class="flex items-center justify-center py-4"
      >
        <UIcon
          name="i-lucide-loader-circle"
          class="animate-spin size-4 text-muted"
        />
      </div>

      <!-- Error banner (dismissible, shown above list) -->
      <div
        v-if="fetchError"
        class="flex items-center gap-2 text-xs text-error bg-error/10 border border-error/20 rounded px-3 py-2"
      >
        <UIcon
          name="i-lucide-alert-circle"
          class="size-3.5 shrink-0"
        />
        <span class="flex-1 min-w-0 truncate">{{ fetchError }}</span>
        <button
          class="shrink-0 hover:text-error/70 transition-colors"
          title="Dismiss"
          @click="dismissError"
        >
          <UIcon
            name="i-lucide-x"
            class="size-3.5"
          />
        </button>
      </div>

      <!-- Empty state -->
      <p
        v-if="!loading && !fetchError && attachedDocs.length === 0"
        class="text-xs text-muted text-center py-2"
      >
        No documents attached
      </p>

      <!-- Doc list -->
      <ul
        v-if="!loading && attachedDocs.length > 0"
        class="space-y-1"
      >
        <li
          v-for="doc in attachedDocs"
          :key="doc.documentId"
          class="flex items-center gap-2 text-xs group focus-within:relative"
        >
          <UIcon
            name="i-lucide-file-text"
            class="size-3.5 shrink-0 text-muted"
          />
          <span class="truncate flex-1 min-w-0">{{ doc.title }}</span>
          <UButton
            icon="i-lucide-x"
            variant="ghost"
            size="xs"
            color="error"
            class="shrink-0 opacity-0 group-hover:opacity-100 group-focus-within:opacity-100 sm:opacity-0 sm:group-hover:opacity-100 focus:opacity-100"
            title="Remove attachment"
            @click="removeDoc(doc.documentId)"
          />
        </li>
      </ul>
    </template>

    <!-- Picker modal -->
    <ChatDocAttachPicker
      v-model:open="showPicker"
      :session-id="sessionId"
      @picked="handlePicked"
    />
  </div>
</template>
