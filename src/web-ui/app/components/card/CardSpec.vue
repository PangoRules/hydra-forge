<script setup lang="ts">
import { ApiRoutes } from '~/lib/routes'
import MarkdownEditor from '~/components/shared/MarkdownEditor.vue'

interface SpecResponse {
  id: string
  projectId: string
  cardId: string
  title: string
  description: string | null
  content: string
  version: number
  createdByUserId: string
  createdAt: string
  updatedAt: string
}

interface SpecVersionResponse {
  id: string
  specId: string
  version: number
  title: string
  description: string | null
  content: string
  createdAt: string
  createdByUserId: string
}

const props = defineProps<{
  cardId: string
  projectId: string
  readonly?: boolean
}>()

const toast = useAppToast()
const api = useApi()

const spec = ref<SpecResponse | null>(null)
const title = ref('')
const content = ref('')
const loading = ref(true)
const saving = ref(false)
const showHistory = ref(false)

const isDirty = computed(() => {
  if (!spec.value) return true // create mode — always allow
  return title.value !== spec.value.title || content.value !== spec.value.content
})

const versions = ref<SpecVersionResponse[]>([])
const loadingVersions = ref(false)
const restoring = ref<string | null>(null)

async function fetchSpec() {
  loading.value = true
  try {
    const { data } = await api.GET(ApiRoutes.Specs.forCard(props.projectId, props.cardId))
    const list = data as { specs: SpecResponse[] } | undefined
    spec.value = list?.specs?.[0] ?? null
    if (spec.value) {
      title.value = spec.value.title
      content.value = spec.value.content
    }
  } catch {
    // No spec yet — that's fine, user can create one
  } finally {
    loading.value = false
  }
}

/** Extract a short description from content's first paragraph */
function deriveDescription(md: string): string {
  const para = md.trim().split('\n\n')[0]
  return para ? para.slice(0, 200) : ''
}

async function save() {
  if (!isDirty.value) return
  saving.value = true
  try {
    const desc = deriveDescription(content.value)
    if (spec.value) {
      const { data } = await api.PUT<SpecResponse>(ApiRoutes.Specs.detail(props.projectId, spec.value.id), {
        body: { title: title.value, description: desc, content: content.value }
      })
      spec.value = data ?? spec.value
    } else {
      const { data } = await api.POST<SpecResponse>(ApiRoutes.Specs.forCard(props.projectId, props.cardId), {
        body: { title: title.value, description: desc, content: content.value }
      })
      spec.value = data ?? null
    }
    versions.value = [] // invalidate cache so next history toggle re-fetches
    toast.success('Spec saved')
    if (showHistory.value) await fetchVersions()
  } catch {
    toast.error('Failed to save spec')
  } finally {
    saving.value = false
  }
}

async function fetchVersions() {
  if (!spec.value) return
  loadingVersions.value = true
  try {
    const { data } = await api.GET(ApiRoutes.Specs.versions(props.projectId, spec.value.id))
    const list = data as { versions: SpecVersionResponse[] } | undefined
    versions.value = (list?.versions ?? []).sort((a, b) => b.version - a.version)
  } catch {
    // silently fail
  } finally {
    loadingVersions.value = false
  }
}

async function restore(ver: SpecVersionResponse) {
  if (!spec.value) return
  restoring.value = ver.id
  try {
    await api.POST(ApiRoutes.Specs.restore(props.projectId, spec.value.id), {
      body: { version: ver.version }
    })
    toast.success('Version restored')
    await fetchSpec()
    if (showHistory.value) await fetchVersions()
  } catch {
    toast.error('Failed to restore version')
  } finally {
    restoring.value = null
  }
}

async function toggleHistory() {
  showHistory.value = !showHistory.value
  if (showHistory.value) {
    await fetchVersions()
  }
}

function formatDate(iso: string) {
  return new Date(iso).toLocaleString()
}

const board = useBoardStore()

function shortUser(userId: string) {
  return board.members.find(m => m.userId === userId)?.username ?? userId.slice(0, 8) + '...'
}

onMounted(() => fetchSpec())
</script>

<template>
  <div class="space-y-3">
    <div class="flex items-center justify-between">
      <p class="text-xs font-medium text-muted uppercase tracking-wide">
        Spec
      </p>
      <div class="flex items-center gap-1">
        <UButton
          v-if="spec"
          size="xs"
          variant="ghost"
          :label="showHistory ? 'Hide history' : 'History'"
          @click="toggleHistory"
        />
        <UButton
          v-if="!props.readonly"
          size="xs"
          :loading="saving"
          :disabled="spec !== null && !isDirty"
          @click="save"
        >
          {{ spec ? 'Save' : 'Create' }}
        </UButton>
      </div>
    </div>

    <div
      v-if="loading"
      class="text-xs text-muted"
    >
      Loading...
    </div>

    <div
      v-else
      class="flex gap-4"
    >
      <div class="flex-1 space-y-3 min-w-0">
        <UInput
          v-model="title"
          placeholder="Spec title"
          :disabled="props.readonly"
          size="sm"
        />
        <MarkdownEditor
          v-model="content"
          :editable="!props.readonly"
          placeholder="Write your spec..."
        />
      </div>

      <div
        v-if="showHistory && spec"
        class="w-44 flex-shrink-0 border-l pl-4 space-y-2 max-h-80 overflow-y-auto"
      >
        <p class="text-xs font-medium text-muted uppercase">
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
              v{{ v.version }} · {{ formatDate(v.createdAt) }}
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
</template>
