<script setup lang="ts">
import { ApiRoutes } from '~/lib/routes'
import MarkdownEditor from '~/components/shared/MarkdownEditor.vue'

const STATUS_LABELS: Record<string, string> = { Pending: 'Pending', Active: 'Active', Done: 'Done' }
const STATUS_COLORS: Record<string, 'neutral' | 'primary' | 'success'> = {
  Pending: 'neutral',
  Active: 'primary',
  Done: 'success'
}

interface PlanResponse {
  id: string
  projectId: string
  cardId: string
  specId: string | null
  title: string
  description: string | null
  content: string
  status: string
  position: number
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
  specId?: string | null
  readonly?: boolean
}>()

const toast = useAppToast()
const api = useApi()
const board = useBoardStore()

const plans = ref<PlanResponse[]>([])
const loading = ref(true)

// Per-plan edit state: title + content mirroring server state
const editState = ref<Record<string, { title: string, content: string }>>({})
const savingId = ref<string | null>(null)
const actionId = ref<string | null>(null)

// New plan inline form
const showNewPlan = ref(false)
const newTitle = ref('')
const newContent = ref('')
const creating = ref(false)

// Version history: one panel open at a time
const showHistoryId = ref<string | null>(null)
const versionsCache = ref<Record<string, PlanVersionResponse[]>>({})
const loadingVersionsId = ref<string | null>(null)
const restoringId = ref<string | null>(null)

function isPlanDirty(plan: PlanResponse): boolean {
  const s = editState.value[plan.id]
  if (!s) return false
  return s.title !== plan.title || s.content !== plan.content
}

function initEditState(plan: PlanResponse) {
  editState.value[plan.id] = { title: plan.title, content: plan.content }
}

async function fetchPlans() {
  loading.value = true
  try {
    const { data } = await api.GET(ApiRoutes.Plans.forCard(props.projectId, props.cardId))
    const list = data as { plans: PlanResponse[] } | undefined
    plans.value = (list?.plans ?? []).sort((a, b) => a.position - b.position)
    for (const p of plans.value) initEditState(p)
  } catch {
    // silent
  } finally {
    loading.value = false
  }
}

function deriveDescription(md: string): string {
  const para = md.trim().split('\n\n')[0]
  return para ? para.slice(0, 200) : ''
}

async function savePlan(plan: PlanResponse) {
  const s = editState.value[plan.id]
  if (!s || !isPlanDirty(plan)) return
  savingId.value = plan.id
  try {
    const desc = deriveDescription(s.content)
    const { data } = await api.PUT<PlanResponse>(ApiRoutes.Plans.detail(props.projectId, plan.id), {
      body: { title: s.title, description: desc, content: s.content }
    })
    if (data) {
      const idx = plans.value.findIndex(p => p.id === plan.id)
      if (idx >= 0) plans.value[idx] = data
      editState.value[data.id] = { title: data.title, content: data.content }
      versionsCache.value[plan.id] = []
      if (showHistoryId.value === plan.id) await fetchVersions(plan.id)
    }
    toast.success('Plan saved')
  } catch {
    toast.error('Failed to save plan')
  } finally {
    savingId.value = null
  }
}

async function createPlan() {
  if (!newTitle.value.trim()) return
  creating.value = true
  try {
    const desc = deriveDescription(newContent.value)
    const { data } = await api.POST<PlanResponse>(ApiRoutes.Plans.forCard(props.projectId, props.cardId), {
      body: {
        title: newTitle.value,
        description: desc,
        content: newContent.value,
        specId: props.specId ?? null,
        position: plans.value.length
      }
    })
    if (data) {
      plans.value.push(data)
      initEditState(data)
    }
    newTitle.value = ''
    newContent.value = ''
    showNewPlan.value = false
    toast.success('Plan created')
  } catch {
    toast.error('Failed to create plan')
  } finally {
    creating.value = false
  }
}

async function activate(plan: PlanResponse) {
  actionId.value = plan.id
  try {
    const { data } = await api.POST<PlanResponse>(ApiRoutes.Plans.activate(props.projectId, plan.id))
    if (data) {
      const idx = plans.value.findIndex(p => p.id === plan.id)
      if (idx >= 0) plans.value[idx] = data
    }
  } catch {
    toast.error('Failed to activate plan')
  } finally {
    actionId.value = null
  }
}

async function complete(plan: PlanResponse) {
  actionId.value = plan.id
  try {
    const { data } = await api.POST<PlanResponse>(ApiRoutes.Plans.complete(props.projectId, plan.id))
    if (data) {
      const idx = plans.value.findIndex(p => p.id === plan.id)
      if (idx >= 0) plans.value[idx] = data
    }
  } catch {
    toast.error('Failed to complete plan')
  } finally {
    actionId.value = null
  }
}

async function reactivate(plan: PlanResponse) {
  actionId.value = plan.id
  try {
    const { data } = await api.POST<PlanResponse>(ApiRoutes.Plans.reactivate(props.projectId, plan.id))
    if (data) {
      const idx = plans.value.findIndex(p => p.id === plan.id)
      if (idx >= 0) plans.value[idx] = data
    }
  } catch {
    toast.error('Failed to reactivate plan')
  } finally {
    actionId.value = null
  }
}

async function fetchVersions(planId: string) {
  loadingVersionsId.value = planId
  try {
    const { data } = await api.GET(ApiRoutes.Plans.versions(props.projectId, planId))
    const list = data as { versions: PlanVersionResponse[] } | undefined
    versionsCache.value[planId] = (list?.versions ?? []).sort((a, b) => b.version - a.version)
  } catch {
    // silent
  } finally {
    loadingVersionsId.value = null
  }
}

async function toggleHistory(planId: string) {
  if (showHistoryId.value === planId) {
    showHistoryId.value = null
  } else {
    showHistoryId.value = planId
    if (!versionsCache.value[planId]?.length) await fetchVersions(planId)
  }
}

async function restore(plan: PlanResponse, ver: PlanVersionResponse) {
  restoringId.value = ver.id
  try {
    await api.POST(ApiRoutes.Plans.restore(props.projectId, plan.id), {
      body: { version: ver.version }
    })
    toast.success('Version restored')
    await fetchPlans()
    if (showHistoryId.value === plan.id) await fetchVersions(plan.id)
  } catch {
    toast.error('Failed to restore version')
  } finally {
    restoringId.value = null
  }
}

function formatDate(iso: string) {
  return new Date(iso).toLocaleString()
}

function shortUser(userId: string) {
  return board.members.find(m => m.userId === userId)?.username ?? userId.slice(0, 8) + '...'
}

onMounted(() => fetchPlans())
</script>

<template>
  <div class="space-y-3">
    <div class="flex items-center justify-between">
      <p class="text-xs font-medium text-muted uppercase tracking-wide">
        Plans ({{ plans.length }})
      </p>
      <UButton
        v-if="!props.readonly"
        size="xs"
        variant="ghost"
        icon="i-lucide-plus"
        @click="showNewPlan = !showNewPlan"
      >
        Add Plan
      </UButton>
    </div>

    <div
      v-if="loading"
      class="text-xs text-muted"
    >
      Loading...
    </div>

    <div
      v-else
      class="space-y-4"
    >
      <!-- Plan list -->
      <div
        v-for="plan in plans"
        :key="plan.id"
        class="border rounded-md p-3 space-y-3"
        :class="plan.status === 'Done' ? 'opacity-75' : ''"
      >
        <!-- Plan header row -->
        <div class="flex items-center gap-2 flex-wrap">
          <UInput
            v-if="editState[plan.id]"
            v-model="editState[plan.id]!.title"
            class="flex-1"
            style="min-width: 8rem"
            :disabled="props.readonly || plan.status === 'Done'"
            size="sm"
          />
          <div class="flex items-center gap-1 shrink-0 flex-wrap">
            <UBadge
              :color="STATUS_COLORS[plan.status] ?? 'neutral'"
              variant="subtle"
              size="xs"
            >
              {{ STATUS_LABELS[plan.status] ?? 'Unknown' }}
            </UBadge>
            <UButton
              v-if="!props.readonly && plan.status === 'Pending'"
              size="xs"
              variant="ghost"
              :loading="actionId === plan.id"
              @click="activate(plan)"
            >
              Activate
            </UButton>
            <UButton
              v-if="!props.readonly && plan.status === 'Active'"
              size="xs"
              variant="ghost"
              :loading="actionId === plan.id"
              @click="complete(plan)"
            >
              Complete
            </UButton>
            <UButton
              v-if="!props.readonly && plan.status === 'Done'"
              size="xs"
              variant="ghost"
              :loading="actionId === plan.id"
              @click="reactivate(plan)"
            >
              Reactivate
            </UButton>
            <UButton
              size="xs"
              variant="ghost"
              :label="showHistoryId === plan.id ? 'Hide history' : 'History'"
              @click="toggleHistory(plan.id)"
            />
            <UButton
              v-if="!props.readonly && plan.status !== 'Done'"
              size="xs"
              :loading="savingId === plan.id"
              :disabled="!isPlanDirty(plan)"
              @click="savePlan(plan)"
            >
              Save
            </UButton>
          </div>
        </div>

        <!-- Editor + optional history panel -->
        <div class="flex gap-4">
          <div class="flex-1 min-w-0">
            <MarkdownEditor
              v-if="editState[plan.id]"
              v-model="editState[plan.id]!.content"
              :editable="!props.readonly && plan.status !== 'Done'"
              placeholder="Describe this plan step by step..."
            />
          </div>
          <div
            v-if="showHistoryId === plan.id"
            class="w-44 flex-shrink-0 border-l pl-4 space-y-2 max-h-80 overflow-y-auto"
          >
            <p class="text-xs font-medium text-muted uppercase">
              History
            </p>
            <div
              v-if="loadingVersionsId === plan.id"
              class="text-xs text-muted"
            >
              Loading...
            </div>
            <div
              v-else-if="!versionsCache[plan.id]?.length"
              class="text-xs text-muted"
            >
              No versions yet
            </div>
            <div
              v-for="v in versionsCache[plan.id]"
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
                :loading="restoringId === v.id"
                :disabled="!!restoringId || plan.status === 'Done'"
                @click="restore(plan, v)"
              >
                Restore
              </UButton>
            </div>
          </div>
        </div>
      </div>

      <!-- Empty state -->
      <p
        v-if="plans.length === 0 && !showNewPlan"
        class="text-xs text-muted"
      >
        No plans yet.
      </p>

      <!-- New plan inline form -->
      <div
        v-if="showNewPlan"
        class="border border-dashed rounded-md p-3 space-y-3"
      >
        <p class="text-xs font-medium text-muted uppercase">
          New Plan
        </p>
        <UInput
          v-model="newTitle"
          placeholder="Plan title"
          size="sm"
        />
        <MarkdownEditor
          v-model="newContent"
          :editable="true"
          placeholder="Describe this plan step by step..."
        />
        <div class="flex items-center gap-1 justify-end">
          <UButton
            size="xs"
            variant="ghost"
            @click="showNewPlan = false; newTitle = ''; newContent = ''"
          >
            Cancel
          </UButton>
          <UButton
            size="xs"
            :loading="creating"
            :disabled="!newTitle.trim()"
            @click="createPlan"
          >
            Create
          </UButton>
        </div>
      </div>
    </div>
  </div>
</template>
