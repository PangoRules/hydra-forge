<script setup lang="ts">
import type { components } from '~/types/api'
import { ApiRoutes } from '~/lib/routes'
import AppModal from '~/components/shared/AppModal.vue'
import MarkdownEditor from '~/components/shared/MarkdownEditor.vue'
import { CARD_TYPE_OPTIONS, toTypeString } from '~/lib/card-type'

type ColumnResponse = components['schemas']['ColumnResponse']
type MemberResponse = components['schemas']['MemberResponse']

const props = defineProps<{
  projectId: string
  columns: ColumnResponse[]
  members?: MemberResponse[]
  preselectedColumnId?: string
}>()

const emit = defineEmits<{
  close: []
  created: []
}>()

const isOpen = ref(true)
const api = useApi()
const toast = useToast()

const CARD_TYPE_DEFAULT = 0

const title = ref('')
const description = ref('')
const cardType = ref(CARD_TYPE_DEFAULT)
const columnId = ref(props.preselectedColumnId ?? '')
const dueAt = ref('')
const selectedAssignees = ref<string[]>([])
const saving = ref(false)

const canSave = computed(() => title.value.trim().length > 0 && columnId.value.length > 0)

async function handleCreate() {
  if (!canSave.value) return
  saving.value = true
  const body: Record<string, unknown> = {
    columnId: columnId.value,
    title: title.value.trim(),
    description: description.value,
    type: toTypeString(cardType.value)
  }
  if (dueAt.value) {
    body.dueAt = `${dueAt.value}T00:00:00Z`
  }
  if (selectedAssignees.value.length > 0) {
    body.assigneeUserIds = selectedAssignees.value.map(id => id)
  }
  try {
    await api.POST(ApiRoutes.Cards.create(props.projectId), { body })
    toast.add({ title: 'Card created', color: 'success' })
    emit('created')
    closeWithAnimation()
  } catch (e: unknown) {
    toast.add({ title: e instanceof ApiError ? e.message : 'Failed to create card', color: 'error' })
  } finally {
    saving.value = false
  }
}

function closeWithAnimation() {
  isOpen.value = false
  setTimeout(() => emit('close'), 200)
}
</script>

<template>
  <AppModal
    :open="isOpen"
    title="Create card"
    width="sm:max-w-lg"
    @update:open="closeWithAnimation"
  >
    <template #body>
      <div class="space-y-4 p-1">
        <div>
          <label class="block text-sm font-medium mb-1">Title *</label>
          <input
            v-model="title"
            class="w-full px-3 py-2 text-sm border border-gray-300 dark:border-gray-600 rounded-md bg-white dark:bg-gray-800 focus:outline-none focus:ring-2 focus:ring-primary"
            placeholder="Card title"
            @keydown.enter="handleCreate"
          >
        </div>
        <div>
          <label class="block text-sm font-medium mb-1">Description</label>
          <MarkdownEditor
            :model-value="description"
            placeholder="Optional description"
            class="min-h-25"
            @update:model-value="description = $event"
          />
        </div>
        <!-- Assignees -->
        <div>
          <label class="block text-sm font-medium mb-1.5">Assignees</label>
          <div class="flex flex-wrap gap-1.5 mb-2">
            <span
              v-for="userId in selectedAssignees"
              :key="userId"
              class="inline-flex items-center gap-1 px-2 py-0.5 text-xs rounded-full bg-primary/10 text-primary dark:bg-primary/20"
            >
              {{ props.members?.find(m => m.userId === userId)?.username ?? userId }}
              <button
                class="hover:text-red-500 leading-none"
                @click="selectedAssignees = selectedAssignees.filter(id => id !== userId)"
              >×</button>
            </span>
            <span
              v-if="selectedAssignees.length === 0"
              class="text-xs text-gray-400"
            >None</span>
          </div>
          <select
            v-if="props.members && props.members.length > 0"
            class="w-full px-2 py-1.5 text-sm border border-gray-300 dark:border-gray-600 rounded-md bg-white dark:bg-gray-800"
            @change="(e: Event) => { const s = e.target as HTMLSelectElement; if (s.value && !selectedAssignees.includes(s.value)) selectedAssignees.push(s.value); s.value = '' }"
          >
            <option value="">
              + Add assignee
            </option>
            <option
              v-for="m in props.members.filter(m => !selectedAssignees.includes(m.userId))"
              :key="m.userId"
              :value="m.userId"
            >
              {{ m.username }}
            </option>
          </select>
        </div>
        <div class="flex gap-4 items-end">
          <div class="flex-1">
            <label class="block text-sm font-medium mb-1">Type</label>
            <select
              v-model="cardType"
              class="w-full px-3 py-2 text-sm border border-gray-300 dark:border-gray-600 rounded-md bg-white dark:bg-gray-800"
            >
              <option
                v-for="opt in CARD_TYPE_OPTIONS"
                :key="opt.value"
                :value="opt.value"
              >
                {{ opt.label }}
              </option>
            </select>
          </div>
          <div class="flex-1">
            <label class="block text-sm font-medium mb-1">Due date</label>
            <input
              v-model="dueAt"
              type="date"
              class="w-full px-3 py-2 text-sm border border-gray-300 dark:border-gray-600 rounded-md bg-white dark:bg-gray-800 focus:outline-none focus:ring-2 focus:ring-primary"
            >
          </div>
          <div class="flex-1">
            <label class="block text-sm font-medium mb-1">{{ preselectedColumnId ? 'Column' : 'Column *' }}</label>
            <p
              v-if="preselectedColumnId"
              class="mt-1 text-xs text-gray-500"
            >
              Locked to {{ columns.find(c => c.id === preselectedColumnId)?.name }}
            </p>
            <select
              v-model="columnId"
              class="w-full px-3 py-2 text-sm border rounded-md bg-white dark:bg-gray-800 focus:outline-none focus:ring-2 focus:ring-primary disabled:cursor-not-allowed disabled:opacity-60"
              :class="preselectedColumnId ? 'border-gray-300 dark:border-gray-600' : 'border-gray-300 dark:border-gray-600'"
              :disabled="!!preselectedColumnId"
            >
              <option
                v-for="col in columns"
                :key="col.id"
                :value="col.id"
              >
                {{ col.name }}
              </option>
            </select>
          </div>
        </div>
      </div>
    </template>
    <template #footer>
      <div class="flex justify-end gap-3 w-full">
        <UButton
          variant="ghost"
          @click="closeWithAnimation"
        >
          Cancel
        </UButton>
        <UButton
          :disabled="!canSave"
          :loading="saving"
          color="primary"
          @click="handleCreate"
        >
          Create
        </UButton>
      </div>
    </template>
  </AppModal>
</template>
