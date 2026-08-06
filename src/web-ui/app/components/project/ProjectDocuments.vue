<script setup lang="ts">
import { ApiRoutes } from '~/lib/routes'
import { formatDateTime } from '~/lib/date'
import { htmlToMarkdown } from '~/lib/document-markdown'
import MarkdownEditor from '~/components/shared/MarkdownEditor.vue'
import AppModal from '~/components/shared/AppModal.vue'
import { useMemberDisplay } from '~/composables/useMemberDisplay'

const props = defineProps<{
  projectId: string
}>()

interface ProjectDocumentResponse {
  id: string
  projectId: string
  docType: number
  title: string
  description: string | null
  content: string
  version: number
  createdByUserId: string
  createdAt: string
  updatedAt: string
  archivedAt: string | null
}

interface ProjectDocumentVersionResponse {
  id: string
  projectDocumentId: string
  version: number
  title: string
  description: string | null
  content: string
  createdAt: string
  createdByUserId: string
}

const DOC_TYPE_LABELS: Record<number, string> = {
  1: 'Scope',
  2: 'Glossary',
  3: 'Data Model',
  4: 'Architecture',
  5: 'Functional Spec',
  6: 'Decisions',
  7: 'Reference'
}

const toast = useAppToast()
const api = useApi()

const documents = ref<ProjectDocumentResponse[]>([])
const loading = ref(true)
const selectedDoc = ref<ProjectDocumentResponse | null>(null)
const editorContent = ref('')
const editorTitle = ref('')
const saving = ref(false)

const showHistory = ref(false)
const versions = ref<ProjectDocumentVersionResponse[]>([])
const loadingVersions = ref(false)
const restoring = ref<string | null>(null)

const showCreateModal = ref(false)
const createDocType = ref<number>(1)
const createTitle = ref('')
const creating = ref(false)

const groupedDocs = computed(() => {
  const groups: Record<number, ProjectDocumentResponse[]> = {}
  for (const doc of documents.value) {
    if (!groups[doc.docType]) groups[doc.docType] = []
    groups[doc.docType]!.push(doc)
  }
  return groups
})

async function fetchDocuments() {
  loading.value = true
  try {
    const { data } = await api.GET<{ documents: ProjectDocumentResponse[] }>(
      ApiRoutes.Documents.list(props.projectId)
    )
    documents.value = data?.documents ?? []
  } catch {
    toast.error('Failed to load documents')
  } finally {
    loading.value = false
  }
}

async function selectDoc(doc: ProjectDocumentResponse) {
  showHistory.value = false
  selectedDoc.value = doc
  editorTitle.value = doc.title
  editorContent.value = doc.content
}

function closeDoc() {
  selectedDoc.value = null
  showHistory.value = false
  versions.value = []
}

async function save() {
  if (!selectedDoc.value) return
  saving.value = true
  try {
    const { data } = await api.PUT<ProjectDocumentResponse>(
      ApiRoutes.Documents.update(props.projectId, selectedDoc.value.id),
      { body: { title: editorTitle.value, description: null, content: editorContent.value } }
    )
    if (data) {
      selectedDoc.value = data
      const idx = documents.value.findIndex(d => d.id === data.id)
      if (idx !== -1) documents.value[idx] = data
      toast.success('Document saved')
      if (showHistory.value) await fetchVersions()
    }
  } catch {
    toast.error('Failed to save document')
  } finally {
    saving.value = false
  }
}

async function fetchVersions() {
  if (!selectedDoc.value) return
  loadingVersions.value = true
  try {
    const { data } = await api.GET<{ versions: ProjectDocumentVersionResponse[] }>(
      ApiRoutes.Documents.versions(props.projectId, selectedDoc.value.id)
    )
    versions.value = (data?.versions ?? []).sort((a, b) => b.version - a.version)
  } catch {
    toast.error('Failed to load version history')
  } finally {
    loadingVersions.value = false
  }
}

async function restore(ver: ProjectDocumentVersionResponse) {
  if (!selectedDoc.value) return
  restoring.value = ver.id
  try {
    const { data } = await api.POST<ProjectDocumentResponse>(
      ApiRoutes.Documents.restore(props.projectId, selectedDoc.value.id),
      { body: { version: ver.version } }
    )
    if (data) {
      selectedDoc.value = data
      editorTitle.value = data.title
      editorContent.value = data.content
      const idx = documents.value.findIndex(d => d.id === data.id)
      if (idx !== -1) documents.value[idx] = data
      toast.success('Version restored')
      await fetchVersions()
    }
  } catch {
    toast.error('Failed to restore version')
  } finally {
    restoring.value = null
  }
}

async function toggleHistory() {
  showHistory.value = !showHistory.value
  if (showHistory.value) await fetchVersions()
}

async function createDocument() {
  if (!createTitle.value.trim()) return
  creating.value = true
  try {
    const { data } = await api.POST<ProjectDocumentResponse>(
      ApiRoutes.Documents.create(props.projectId),
      { body: { docType: createDocType.value, title: createTitle.value.trim(), description: null, content: '' } }
    )
    if (data) {
      documents.value.push(data)
      selectDoc(data)
      toast.success('Document created')
    }
  } catch {
    toast.error('Failed to create document')
  } finally {
    creating.value = false
    showCreateModal.value = false
    createTitle.value = ''
    createDocType.value = 1
  }
}

const { shortUser } = useMemberDisplay()

const { exportDocument } = useDocumentExport()

function exportAsMarkdown() {
  if (!selectedDoc.value) return
  exportDocument(editorTitle.value || 'untitled', htmlToMarkdown(editorContent.value), 'md')
}

onMounted(() => fetchDocuments())
</script>

<template>
  <div class="flex-1 flex min-h-0">
    <!-- Document list sidebar -->
    <div class="w-64 flex-shrink-0 border-r border-gray-200 dark:border-gray-700 flex flex-col">
      <div class="p-3 border-b border-gray-200 dark:border-gray-700">
        <UButton
          size="sm"
          block
          @click="showCreateModal = true"
        >
          <UIcon
            name="i-lucide-plus"
            class="size-4 mr-1"
          />
          New Document
        </UButton>
      </div>

      <div class="flex-1 overflow-y-auto">
        <div
          v-if="loading"
          class="p-3 text-xs text-muted"
        >
          Loading...
        </div>
        <div
          v-else-if="documents.length === 0"
          class="p-3 text-xs text-muted"
        >
          No documents yet
        </div>
        <template v-else>
          <div
            v-for="(docs, docType) in groupedDocs"
            :key="docType"
            class="border-b border-gray-100 dark:border-gray-800"
          >
            <div class="px-3 py-1.5 text-xs font-semibold text-muted uppercase tracking-wide bg-gray-50 dark:bg-gray-800">
              {{ DOC_TYPE_LABELS[Number(docType)] ?? `Type ${docType}` }}
            </div>
            <div
              v-for="doc in docs"
              :key="doc.id"
              class="px-3 py-2 cursor-pointer hover:bg-primary/5 transition-colors"
              :class="{ 'bg-primary/10': selectedDoc?.id === doc.id }"
              @click="selectDoc(doc)"
            >
              <p class="text-sm font-medium truncate">
                {{ doc.title }}
              </p>
              <p class="text-xs text-muted truncate">
                {{ doc.description ?? 'No description' }}
              </p>
            </div>
          </div>
        </template>
      </div>
    </div>

    <!-- Editor area -->
    <div
      v-if="selectedDoc"
      class="flex-1 flex flex-col min-h-0"
    >
      <!-- Editor toolbar -->
      <div class="flex items-center justify-between px-4 py-2 border-b border-gray-200 dark:border-gray-700">
        <UInput
          v-model="editorTitle"
          placeholder="Document title"
          size="sm"
          class="w-64"
        />
        <div class="flex items-center gap-1">
          <UButton
            size="xs"
            variant="ghost"
            icon="i-lucide-arrow-left"
            title="Back to list"
            @click="closeDoc"
          />
          <UButton
            size="xs"
            variant="ghost"
            :label="showHistory ? 'Hide history' : 'History'"
            @click="toggleHistory"
          />
          <UButton
            size="xs"
            variant="ghost"
            icon="i-lucide-download"
            title="Export as Markdown"
            @click="exportAsMarkdown"
          />
          <UButton
            size="xs"
            :loading="saving"
            @click="save"
          >
            Save
          </UButton>
        </div>
      </div>

      <!-- Main content area -->
      <div class="flex-1 flex min-h-0">
        <!-- Editor -->
        <div class="flex-1 flex flex-col min-h-0">
          <MarkdownEditor
            v-model="editorContent"
            placeholder="Write your document..."
            class="flex-1 min-h-0"
          />
        </div>

        <!-- Version history sidebar -->
        <div
          v-if="showHistory"
          class="w-48 flex-shrink-0 border-l border-gray-200 dark:border-gray-700 overflow-y-auto p-3 space-y-2"
        >
          <p class="text-xs font-medium text-muted uppercase mb-2">
            History
          </p>
          <div
            v-if="loadingVersions"
            class="text-xs text-muted"
          >
            Loading...
          </div>
          <div
            v-else-if="versions.length === 0"
            class="text-xs text-muted"
          >
            No versions yet
          </div>
          <div
            v-for="v in versions"
            :key="v.id"
            class="flex items-center justify-between gap-1 text-xs py-1"
          >
            <div class="min-w-0">
              <p class="truncate">
                v{{ v.version }} · {{ formatDateTime(v.createdAt) }}
              </p>
              <p class="text-muted truncate">
                {{ shortUser(v.createdByUserId) }}
              </p>
            </div>
            <UButton
              size="xs"
              variant="ghost"
              :loading="restoring === v.id"
              :disabled="!!restoring"
              @click="restore(v)"
            >
              Restore
            </UButton>
          </div>
        </div>
      </div>
    </div>

    <!-- Empty state -->
    <div
      v-else
      class="flex-1 flex items-center justify-center"
    >
      <div class="text-center text-muted">
        <UIcon
          name="i-lucide-file-text"
          class="size-12 mb-2 mx-auto opacity-30"
        />
        <p class="text-sm">
          Select a document or create a new one
        </p>
      </div>
    </div>

    <!-- Create document modal -->
    <AppModal
      v-model:open="showCreateModal"
      title="New Document"
      width="sm:max-w-lg"
    >
      <template #body>
        <div class="space-y-3">
          <div>
            <label class="text-xs font-medium text-muted uppercase tracking-wide mb-1 block">Type</label>
            <USelect
              v-model="createDocType"
              :items="Object.entries(DOC_TYPE_LABELS).map(([value, label]) => ({ value: Number(value), label }))"
              size="sm"
              class="w-full"
            />
            <p class="text-xs text-muted mt-1">
              Not one of the fixed categories? Pick <strong>Reference</strong> — it allows any
              number of docs, so it doubles as a catch-all for anything project-specific
              (meeting notes, policies, runbooks, etc). The title is yours to name.
            </p>
          </div>
          <div>
            <label class="text-xs font-medium text-muted uppercase tracking-wide mb-1 block">Title</label>
            <UInput
              v-model="createTitle"
              placeholder="Document title"
              size="sm"
              class="w-full"
              @keydown.enter="createDocument"
            />
          </div>
        </div>
      </template>
      <template #footer>
        <UButton
          variant="ghost"
          size="sm"
          @click="showCreateModal = false"
        >
          Cancel
        </UButton>
        <UButton
          size="sm"
          :loading="creating"
          :disabled="!createTitle.trim()"
          @click="createDocument"
        >
          Create
        </UButton>
      </template>
    </AppModal>
  </div>
</template>
