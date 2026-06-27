<script setup lang="ts">
import type { components } from '~/types/api'
import { ApiRoutes } from '~/lib/routes'
import { formatDueDate, isOverdue } from '~/lib/date'
import { onClickOutside } from '@vueuse/core'
import ConfirmDialog from '~/components/shared/ConfirmDialog.vue'

type CardResponse = components['schemas']['CardResponse']

const props = defineProps<{
  card: CardResponse
  projectId: string
}>()

const emit = defineEmits<{
  click: [card: CardResponse]
}>()

const api = useApi()
const board = useBoardStore()
const toast = useToast()

const showMenu = ref(false)
const menuRef = ref<HTMLElement | null>(null)
const menuButtonRef = ref<HTMLElement | null>(null)
const showArchiveConfirm = ref(false)

const formattedDue = computed(() => formatDueDate(props.card.dueAt))
const cardIsOverdue = computed(() => isOverdue(props.card.dueAt))

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
    toast.add({ title: 'Card archived', color: 'success', duration: 4000 })
  } catch {
    toast.add({ title: 'Failed to archive card', color: 'error' })
  }
}

async function handleRestore() {
  closeMenu()
  try {
    await api.POST(ApiRoutes.Cards.restore(props.projectId, props.card.id), {
      body: { version: props.card.version }
    })
    board.fetchBoard(props.projectId)
    toast.add({ title: 'Card restored', color: 'success', duration: 4000 })
  } catch {
    toast.add({ title: 'Failed to restore card', color: 'error' })
  }
}
</script>

<template>
  <div
    class="bg-white dark:bg-gray-800 rounded border border-gray-200 dark:border-gray-700 p-3 cursor-pointer hover:shadow-md transition-shadow"
    @click="emit('click', card)"
  >
    <div class="flex items-start gap-2">
      <input
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
          <h4 class="text-sm font-medium text-gray-900 dark:text-gray-100 truncate">
            {{ card.title }}
          </h4>
          <span
            v-if="card.archivedAt"
            class="text-xs text-gray-400 shrink-0"
          >
            archived
          </span>
        </div>
        <p
          v-if="plainDescription"
          class="text-xs text-gray-500 mt-1 line-clamp-2"
        >
          {{ plainDescription }}
        </p>
      </div>

      <!-- Three-dot menu -->
      <div class="relative shrink-0">
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
          class="absolute right-0 top-full mt-1 bg-white dark:bg-gray-800 rounded-md border border-gray-200 dark:border-gray-700 shadow-lg py-1 z-50 min-w-35"
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
