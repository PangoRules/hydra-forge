<script setup lang="ts">
import { ApiRoutes } from '~/lib/routes'

interface AttachedDoc {
  id: string
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
const showPicker = ref(false)

async function fetchAttached() {
  loading.value = true
  try {
    const { data } = await api.GET<AttachedDoc[]>(
      ApiRoutes.Chat.sessions.listDocuments(props.sessionId)
    )
    attachedDocs.value = data ?? []
  } catch {
    // non-critical — panel degrades gracefully
  } finally {
    loading.value = false
  }
}

async function removeDoc(docId: string) {
  try {
    await api.DELETE(ApiRoutes.Chat.sessions.detachDocument(props.sessionId, docId))
    attachedDocs.value = attachedDocs.value.filter(d => d.id !== docId)
    emit('attached')
  } catch (err) {
    toast.error(err instanceof Error ? err.message : 'Failed to remove document')
  }
}

function handlePicked() {
  showPicker.value = false
  void fetchAttached()
  emit('attached')
}

onMounted(fetchAttached)
</script>

<template>
  <div class="space-y-2">
    <!-- Header row -->
    <div class="flex items-center justify-between">
      <span class="text-xs font-medium text-muted uppercase">Attached Docs</span>
      <UButton
        icon="i-lucide-plus"
        variant="ghost"
        size="xs"
        title="Attach a document"
        @click="showPicker = true"
      />
    </div>

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

    <!-- Empty state -->
    <p
      v-else-if="attachedDocs.length === 0"
      class="text-xs text-muted text-center py-2"
    >
      No documents attached
    </p>

    <!-- Doc list -->
    <ul
      v-else
      class="space-y-1"
    >
      <li
        v-for="doc in attachedDocs"
        :key="doc.id"
        class="flex items-center gap-2 text-xs group"
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
          class="opacity-0 group-hover:opacity-100 shrink-0"
          title="Remove attachment"
          @click="removeDoc(doc.id)"
        />
      </li>
    </ul>

    <!-- Picker modal -->
    <ChatDocAttachPicker
      v-model:open="showPicker"
      :session-id="sessionId"
      @picked="handlePicked"
    />
  </div>
</template>
