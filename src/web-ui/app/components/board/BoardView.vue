<script setup lang="ts">
import type { components } from '~/types/api'
import BoardColumn from '~/components/board/BoardColumn.vue'
import { useColumnReorder } from '~/composables/useColumnReorder'
import { useColumnManage } from '~/composables/useColumnManage'

type ColumnResponse = components['schemas']['ColumnResponse']
type CardResponse = components['schemas']['CardResponse']

const props = defineProps<{
  columns: ColumnResponse[]
  cardsByColumn: Map<string, CardResponse[]>
  projectId: string
  includeArchived: boolean
  readonly?: boolean
  selectedCardId?: string | null
  selectedColumnIndex?: number
}>()

const emit = defineEmits<{
  'card-move': [cardId: string, targetColumnId: string, targetPosition: number]
  'card-click': [card: CardResponse]
  'column-click': [columnId: string]
  'add-card': [columnId: string]
}>()

const { reorderColumns, moveColumnLeft, moveColumnRight } = useColumnReorder(props.projectId)
const { createColumn, updateColumn, deleteColumn, saving } = useColumnManage(props.projectId)

function handleCardMove(cardId: string, targetColumnId: string, targetPosition: number) {
  emit('card-move', cardId, targetColumnId, targetPosition)
}

function handleCardClick(card: CardResponse) {
  emit('card-click', card)
}

const showAddColumn = ref(false)
const newColumnName = ref('')

async function handleAddColumn() {
  if (!newColumnName.value.trim()) return
  const ok = await createColumn(newColumnName.value.trim(), null, null)
  if (ok) {
    newColumnName.value = ''
    showAddColumn.value = false
  }
}
</script>

<template>
  <div class="flex gap-4 pb-4 flex-1 min-h-0">
    <BoardColumn
      v-for="(col, idx) in columns"
      :key="col.id"
      :column="col"
      :cards="cardsByColumn.get(col.id) ?? []"
      :project-id="projectId"
      :include-archived="includeArchived"
      :readonly="readonly"
      :can-move-left="idx > 0"
      :can-move-right="idx < columns.length - 1"
      :selected="idx === selectedColumnIndex"
      :selected-card-id="selectedCardId"
      class="w-64 md:w-72 lg:w-80"
      @card-move="handleCardMove"
      @card-click="handleCardClick"
      @column-click="(colId: string) => emit('column-click', colId)"
      @add-card="(colId: string) => emit('add-card', colId)"
      @reorder="reorderColumns"
      @move-left="() => moveColumnLeft(col.id)"
      @move-right="() => moveColumnRight(col.id)"
      @update-column="(columnId: string, name: string, color: string | null, wipLimit: number | null) => updateColumn(columnId, name, color, wipLimit)"
      @delete-column="(columnId: string) => deleteColumn(columnId)"
    />
    <div
      v-if="!readonly"
      class="shrink-0 w-[220px]"
    >
      <UButton
        v-if="!showAddColumn"
        variant="ghost"
        icon="i-lucide-plus"
        @click="showAddColumn = true"
      >
        Add Column
      </UButton>
      <div
        v-else
        class="flex flex-col gap-2 p-2 bg-gray-50 dark:bg-gray-900 rounded-lg"
      >
        <input
          v-model="newColumnName"
          placeholder="Column name"
          class="px-2 py-1 text-sm border border-gray-200 dark:border-gray-600 rounded bg-white dark:bg-gray-800"
          @keyup.enter="handleAddColumn"
        >
        <div class="flex gap-2">
          <UButton
            size="xs"
            :loading="saving"
            @click="handleAddColumn"
          >
            Add
          </UButton>
          <UButton
            size="xs"
            variant="ghost"
            @click="showAddColumn = false; newColumnName = ''"
          >
            Cancel
          </UButton>
        </div>
      </div>
    </div>
  </div>
</template>
