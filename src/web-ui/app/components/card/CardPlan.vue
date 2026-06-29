<script setup lang="ts">
import { ApiRoutes } from '~/lib/routes'
import MarkdownEditor from '~/components/shared/MarkdownEditor.vue'

interface PlanResponse {
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

interface PlanVersionResponse {
  id: string
  planId: string
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

const plan = ref<PlanResponse | null>(null)
const title = ref('')
const content = ref('')
const loading = ref(true)
const saving = ref(false)
const showHistory = ref(false)

const versions = ref<PlanVersionResponse[]>([])
const loadingVersions = ref(false)
const restoring = ref<string | null>(null)

async function fetchPlan() {
  loading.value = true
  try {
    const { data } = await api.GET(ApiRoutes.Plans.forCard(props.projectId, props.cardId))
    const list = data as { plans: PlanResponse[] } | undefined
    plan.value = list?.plans?.[0] ?? null
    if (plan.value) {
      title.value = plan.value.title
      content.value = plan.value.content
    }
  } catch {
    // No plan yet — fine
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
  saving.value = true
  try {
    const desc = deriveDescription(content.value)
    if (plan.value) {
      const { data } = await api.PUT<PlanResponse>(ApiRoutes.Plans.detail(props.projectId, plan.value.id), {
        body: { title: title.value, description: desc, content: content.value }
      })
      plan.value = data ?? plan.value
    } else {
      const { data } = await api.POST<PlanResponse>(ApiRoutes.Plans.forCard(props.projectId, props.cardId), {
        body: { title: title.value, description: desc, content: content.value }
      })
      plan.value = data ?? null
    }
    toast.success('Plan saved')
    if (showHistory.value) await fetchVersions()
  } catch {
    toast.error('Failed to save plan')
  } finally {
    saving.value = false
  }
}

async function fetchVersions() {
  if (!plan.value) return
  loadingVersions.value = true
  try {
    const { data } = await api.GET(ApiRoutes.Plans.versions(props.projectId, plan.value.id))
    const list = data as { versions: PlanVersionResponse[] } | undefined
    versions.value = list?.versions ?? []
  } catch {
    // silently fail
  } finally {
    loadingVersions.value = false
  }
}

async function restore(ver: PlanVersionResponse) {
  if (!plan.value) return
  restoring.value = ver.id
  try {
    await api.POST(ApiRoutes.Plans.restore(props.projectId, plan.value.id), {
      body: { version: ver.version }
    })
    toast.success('Version restored')
    await fetchPlan()
    if (showHistory.value) await fetchVersions()
  } catch {
    toast.error('Failed to restore version')
  } finally {
    restoring.value = null
  }
}

async function toggleHistory() {
  showHistory.value = !showHistory.value
  if (showHistory.value && versions.value.length === 0) {
    await fetchVersions()
  }
}

function formatDate(iso: string) {
  return new Date(iso).toLocaleString()
}

function shortUser(guid: string) {
  return guid.slice(0, 8) + '...'
}

onMounted(() => fetchPlan())
</script>

<template>
  <div class="space-y-3">
    <div class="flex items-center justify-between">
      <p class="text-xs font-medium text-muted uppercase tracking-wide">
        Plan
      </p>
      <div class="flex items-center gap-1">
        <UButton
          v-if="plan"
          size="xs"
          variant="ghost"
          :label="showHistory ? 'Hide history' : 'History'"
          @click="toggleHistory"
        />
        <UButton
          v-if="!props.readonly"
          size="xs"
          :loading="saving"
          @click="save"
        >
          {{ plan ? 'Save' : 'Create' }}
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
          placeholder="Plan title"
          :disabled="props.readonly"
          size="sm"
        />

        <MarkdownEditor
          v-model="content"
          :editable="!props.readonly"
          placeholder="1. First step&#10;2. Second step..."
        />
      </div>

      <div
        v-if="showHistory && plan"
        class="w-52 flex-shrink-0 border-l pl-4 space-y-2"
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
          v-for="(v, i) in versions"
          :key="v.id"
          class="flex items-center justify-between gap-1 text-xs py-1"
        >
          <div class="min-w-0">
            <p class="truncate">
              v{{ versions.length - i }} · {{ formatDate(v.createdAt) }}
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
