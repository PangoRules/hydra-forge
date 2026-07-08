<script setup lang="ts">
import type { components } from '~/types/api'
import { useColumnManage } from '~/composables/useColumnManage'
import { useColumnReorder } from '~/composables/useColumnReorder'
import ConfirmDialog from '~/components/shared/ConfirmDialog.vue'
import AppModal from '~/components/shared/AppModal.vue'

type ColumnResponse = components['schemas']['ColumnResponse']

const props = defineProps<{
  open: boolean
  projectId: string
  columns: ColumnResponse[]
}>()

const emit = defineEmits<{
  'update:open': [value: boolean]
}>()

const { createColumn, updateColumn, deleteColumn, saving } = useColumnManage(props.projectId)
const { moveColumnLeft, moveColumnRight } = useColumnReorder(props.projectId)

const editingId = ref<string | null>(null)
const editName = ref('')
const editColor = ref<string | null>(null)
const editWipLimitStr = ref('')
const deleteTargetId = ref<string | null>(null)
const newColumnName = ref('')

function startEdit(column: ColumnResponse) {
  editingId.value = column.id
  editName.value = column.name
  editColor.value = column.color
  editWipLimitStr.value = column.wipLimit != null ? String(column.wipLimit) : ''
}

function cancelEdit() {
  editingId.value = null
}

async function saveEdit() {
  if (!editingId.value || !editName.value.trim()) return
  const parsed = editWipLimitStr.value.trim() === '' ? null : Number(editWipLimitStr.value)
  const wipLimit = parsed !== null && !Number.isNaN(parsed) && parsed > 0 ? parsed : null
  const ok = await updateColumn(editingId.value, editName.value.trim(), editColor.value, wipLimit)
  if (ok) editingId.value = null
}

async function confirmDelete() {
  if (!deleteTargetId.value) return
  await deleteColumn(deleteTargetId.value)
  deleteTargetId.value = null
}

async function addColumn() {
  if (!newColumnName.value.trim()) return
  const ok = await createColumn(newColumnName.value.trim(), null, null)
  if (ok) newColumnName.value = ''
}
</script>

<template>
  <AppModal
    :open="open"
    title="Manage Columns"
    width="sm:max-w-md"
    @update:open="emit('update:open', $event)"
  >
    <template #body>
      <div class="space-y-2 p-4">
        <div
          v-for="(column, idx) in columns"
          :key="column.id"
          class="border border-gray-200 dark:border-gray-700 rounded-md p-2"
        >
          <div
            v-if="editingId !== column.id"
            class="flex items-center gap-2"
          >
            <div
              v-if="column.color"
              class="size-3 rounded-full shrink-0"
              :style="{ backgroundColor: column.color }"
            />
            <span class="flex-1 text-sm truncate">{{ column.name }}</span>
            <UButton
              icon="i-lucide-chevron-up"
              size="xs"
              variant="ghost"
              :disabled="idx === 0"
              @click="moveColumnLeft(column.id)"
            />
            <UButton
              icon="i-lucide-chevron-down"
              size="xs"
              variant="ghost"
              :disabled="idx === columns.length - 1"
              @click="moveColumnRight(column.id)"
            />
            <UButton
              icon="i-lucide-pencil"
              size="xs"
              variant="ghost"
              :data-testid="`edit-${column.id}`"
              @click="startEdit(column)"
            />
            <UButton
              icon="i-lucide-trash-2"
              size="xs"
              variant="ghost"
              color="error"
              :data-testid="`delete-${column.id}`"
              @click="deleteTargetId = column.id"
            />
          </div>
          <div
            v-else
            class="space-y-2"
          >
            <input
              v-model="editName"
              :data-testid="`edit-name-${column.id}`"
              class="w-full px-2 py-1 text-sm border border-gray-200 dark:border-gray-600 rounded bg-white dark:bg-gray-800"
            >
            <div class="flex items-center gap-2">
              <input
                v-model="editColor"
                type="color"
                class="h-7 w-10 border border-gray-200 dark:border-gray-600 rounded bg-white dark:bg-gray-800"
              >
              <input
                v-model="editWipLimitStr"
                type="number"
                min="0"
                placeholder="WIP limit"
                class="flex-1 px-2 py-1 text-sm border border-gray-200 dark:border-gray-600 rounded bg-white dark:bg-gray-800"
              >
            </div>
            <div class="flex gap-2">
              <UButton
                size="xs"
                :data-testid="`save-${editingId || 'unknown'}`"
                @click="saveEdit"
              >
                Save
              </UButton>
              <UButton
                size="xs"
                variant="ghost"
                @click="cancelEdit"
              >
                Cancel
              </UButton>
            </div>
          </div>
        </div>

        <!-- Add new column -->
        <div class="flex gap-2 pt-2">
          <input
            v-model="newColumnName"
            placeholder="New column name"
            data-testid="new-column-name"
            class="flex-1 px-2 py-1 text-sm border border-gray-200 dark:border-gray-600 rounded bg-white dark:bg-gray-800"
            @keyup.enter="addColumn"
          >
          <UButton
            size="xs"
            :loading="saving"
            data-testid="new-column-add"
            @click="addColumn"
          >
            Add
          </UButton>
        </div>
      </div>
    </template>
  </AppModal>

  <ConfirmDialog
    :open="!!deleteTargetId"
    title="Delete column"
    :message="`Delete this column? This only works if the column has no cards.`"
    confirm-text="Delete"
    confirm-color="error"
    @update:open="(v: boolean) => { if (!v) deleteTargetId = null }"
    @confirm="confirmDelete"
  />
</template>