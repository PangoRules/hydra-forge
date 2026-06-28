<script setup lang="ts">
import type { components } from '~/types/api'
import { ApiRoutes } from '~/lib/routes'
import { formatDueDate, isOverdue } from '~/lib/date'
import { cardTypeOption, cardTypeColorClass } from '~/lib/card-type'
import { onClickOutside } from '@vueuse/core'
import ConfirmDialog from '~/components/shared/ConfirmDialog.vue'

type CardResponse = components['schemas']['CardResponse']

const props = defineProps<{
  card: CardResponse
  projectId: string
  readonly?: boolean
}>()

const emit = defineEmits<{
  'click': [card: CardResponse]
  'move-up': [cardId: string]
  'move-down': [cardId: string]
  'card-drop': [draggedCardId: string, targetPosition: number]
}>()

const api = useApi()
const board = useBoardStore()
const toast = useAppToast()

const showMenu = ref(false)
const menuRef = ref<HTMLElement | null>(null)
const menuButtonRef = ref<HTMLElement | null>(null)
const showArchiveConfirm = ref(false)
const isDragging = ref(false)
const isCardDragOver = ref(false)

const formattedDue = computed(() => formatDueDate(props.card.dueAt))
const cardIsOverdue = computed(() => isOverdue(props.card.dueAt))
const typeOption = computed(() => cardTypeOption(props.card.type))

const plainDescription = computed(() =>
  (props.card.description ?? '').replace(/<[^>]*>/g, '')
)

function toggleMenu() {
  showMenu.value = !showMenu.value
}

function closeMenu() {
  showMenu.value = false
}

onClickOutside(menuRef, closeMenu, { ignore: [menuButtonRef] })

function handleArchive() {
  closeMenu()
  showArchiveConfirm.value = true
}

async function confirmArchive() {
  try {
    await api.POST(ApiRoutes.Cards.archive(props.projectId, props.card.id), {
      body: { version: props.card.version }
    })
    board.fetchBoard(props.projectId)
    toast.success('Card archived')
  } catch {
    toast.error('Failed to archive card')
  }
}

async function handleRestore() {
  closeMenu()
  try {
    await api.POST(ApiRoutes.Cards.restore(props.projectId, props.card.id), {
      body: { version: props.card.version }
    })
    board.fetchBoard(props.projectId)
    toast.success('Card restored')
  } catch {
    toast.error('Failed to restore card')
  }
}

function handleDragStart(event: DragEvent) {
  if (props.readonly) return
  event.dataTransfer!.setData('text/plain', props.card.id)
  event.dataTransfer!.setData('application/position', String(props.card.position))
  event.dataTransfer!.effectAllowed = 'move'
  isDragging.value = true
}

function handleDragEnd() {
  isDragging.value = false
}

function handleCardDragOver(event: DragEvent) {
  if (event.dataTransfer) event.dataTransfer.dropEffect = 'move'
  isCardDragOver.value = true
}

function handleCardDragLeave() {
  isCardDragOver.value = false
}

function handleCardDrop(event: DragEvent) {
  isCardDragOver.value = false
  if (!event.dataTransfer) return
  const draggedCardId = event.dataTransfer.getData('text/plain')
  if (!draggedCardId || draggedCardId === props.card.id) return
  // For same-column downward moves, adjust so dragged card lands BEFORE the target
  const draggedPos = Number(event.dataTransfer.getData('application/position'))
  let targetPos = Number(props.card.position)
  if (!isNaN(draggedPos) && draggedPos < targetPos) {
    targetPos = Math.max(0, targetPos - 1)
  }
  emit('card-drop', draggedCardId, targetPos)
}
</script>

<template>
  <div
    class="bg-white dark:bg-gray-800 rounded border border-gray-200 dark:border-gray-700 p-3 cursor-pointer hover:shadow-md transition-shadow group"
    :class="{ 'opacity-50': isDragging, 'ring-2 ring-primary/50': isCardDragOver }"
    draggable="true"
    @click="emit('click', card)"
    @dragstart="handleDragStart"
    @dragend="handleDragEnd"
    @dragover.stop.prevent="handleCardDragOver"
    @dragleave="handleCardDragLeave"
    @drop.stop.prevent="handleCardDrop"
  >
    <div class="flex items-start gap-2">
      <input
        v-if="!readonly"
        type="checkbox"
        class="mr-2 shrink-0 hidden md:block"
        :checked="!!board.selectedCardIds[card.id]"
        aria-label="Select card"
        @click.stop="board.toggleSelectCard(card.id)"
        @keydown.stop.prevent="board.toggleSelectCard(card.id)"
      >
      <div class="flex-1 min-w-0">
        <div class="flex items-center gap-2">
          <span class="text-xs font-medium text-gray-500 shrink-0">#{{ card.cardNumber }}</span>
          <UIcon
            :name="typeOption.icon"
            class="size-3.5 shrink-0"
            :class="cardTypeColorClass(typeOption)"
          />
          <span
            class="text-xs font-medium shrink-0"
            :class="cardTypeColorClass(typeOption)"
          >
            {{ typeOption.label }}
          </span>
        </div>
        <h4 class="text-sm font-medium text-gray-900 dark:text-gray-100 truncate mt-1">
          {{ card.title }}
          <span
            v-if="card.archivedAt"
            class="text-xs text-gray-400 font-normal ml-1"
          >
            (archived)
          </span>
        </h4>
        <p
          v-if="plainDescription"
          class="text-xs text-gray-500 mt-1 line-clamp-2"
        >
          {{ plainDescription }}
        </p>
      </div>

      <!-- Move up/down arrows -->
      <div
        v-if="!readonly"
        class="shrink-0"
      >
        <div class="flex flex-col">
          <UButton
            icon="i-lucide-chevron-up"
            size="xs"
            variant="ghost"
            color="neutral"
            class="opacity-0 group-hover:opacity-100 transition-opacity -my-0.5"
            @click.stop="emit('move-up', card.id)"
          />
          <UButton
            icon="i-lucide-chevron-down"
            size="xs"
            variant="ghost"
            color="neutral"
            class="opacity-0 group-hover:opacity-100 transition-opacity -my-0.5"
            @click.stop="emit('move-down', card.id)"
          />
        </div>
      </div>

      <!-- Drag handle + three-dot menu -->
      <div
        v-if="!readonly"
        class="flex items-center shrink-0 relative"
      >
        <span
          class="touch-none select-none cursor-grab text-gray-300 hover:text-gray-500"
          @mousedown.stop
          @touchstart.stop
        >
          <UIcon
            name="i-lucide-grip-vertical"
            class="size-4"
          />
        </span>
        <span ref="menuButtonRef">
          <UButton
            icon="i-lucide-ellipsis-vertical"
            variant="ghost"
            size="xs"
            @click.stop="toggleMenu"
          />
        </span>
        <div
          v-if="showMenu"
          ref="menuRef"
          class="absolute right-0 top-full mt-1 bg-white dark:bg-gray-800 rounded-md border border-gray-200 dark:border-gray-700 shadow-lg py-1 z-50 min-w-44 whitespace-nowrap"
          @click.stop
        >
          <button
            v-if="card.archivedAt"
            class="w-full text-left px-3 py-1.5 text-sm hover:bg-gray-100 dark:hover:bg-gray-700 flex items-center gap-2 text-primary"
            @click="handleRestore"
          >
            <UIcon
              name="i-lucide-archive-restore"
              class="size-4"
            />
            Restore
          </button>
          <button
            v-else
            class="w-full text-left px-3 py-1.5 text-sm hover:bg-gray-100 dark:hover:bg-gray-700 flex items-center gap-2 text-red-600 dark:text-red-400"
            @click="handleArchive"
          >
            <UIcon
              name="i-lucide-archive"
              class="size-4"
            />
            Archive
          </button>
        </div>
      </div>
    </div>

    <div class="flex items-center justify-between mt-2">
      <div class="flex items-center gap-2">
        <span
          v-if="card.parentCardId"
          class="text-xs text-primary flex items-center gap-1"
          title="Has a parent card"
        >
          <UIcon
            name="i-lucide-layers"
            class="size-3"
          />
          Parent
        </span>
        <div
          v-if="card.assignees.length > 0"
          class="flex -space-x-1"
        >
          <div
            v-for="assignee in card.assignees.slice(0, 3)"
            :key="assignee.userId"
            class="size-5 rounded-full bg-primary text-white flex items-center justify-center text-xs"
            :title="assignee.username"
          >
            {{ (assignee.username[0] ?? '?').toUpperCase() }}
          </div>
        </div>
      </div>

      <div class="flex items-center gap-2">
        <span
          v-if="formattedDue"
          class="text-xs"
          :class="cardIsOverdue ? 'text-red-500 font-medium' : 'text-gray-400'"
        >
          <UIcon
            name="i-lucide-calendar"
            class="size-3 inline"
          />
          {{ formattedDue }}
        </span>
      </div>
    </div>
  </div>

  <ConfirmDialog
    v-model:open="showArchiveConfirm"
    title="Archive card"
    :message="`Archive #${card.cardNumber} ${card.title}?`"
    confirm-text="Archive"
    @confirm="confirmArchive"
  />
</template>
