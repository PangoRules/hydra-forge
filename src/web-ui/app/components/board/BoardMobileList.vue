<script setup lang="ts">
import type { components } from '~/types/api'
import { ApiRoutes } from '~/lib/routes'
import ConfirmDialog from '~/components/shared/ConfirmDialog.vue'

type ColumnResponse = components['schemas']['ColumnResponse']
type CardResponse = components['schemas']['CardResponse']

const props = defineProps<{
  columns: ColumnResponse[]
  cardsByColumn: Map<string, CardResponse[]>
  projectId: string
}>()

const emit = defineEmits<{
  'card-click': [card: CardResponse]
}>()

const api = useApi()
const board = useBoardStore()
const toast = useToast()

const selectedMenuCardId = ref<string | null>(null)
const showArchiveConfirm = ref(false)
const archiveTargetCard = ref<CardResponse | null>(null)

function toggleMenu(cardId: string) {
  selectedMenuCardId.value = selectedMenuCardId.value === cardId ? null : cardId
}

function closeMenu() {
  selectedMenuCardId.value = null
}

// Close menu on outside click
function onWindowClick(e: MouseEvent) {
  if (selectedMenuCardId.value) {
    const target = (e.target as HTMLElement).closest('[data-menu-id], [data-menu-btn]')
    if (!target) {
      closeMenu()
    }
  }
}

onMounted(() => window.addEventListener('click', onWindowClick))
onUnmounted(() => window.removeEventListener('click', onWindowClick))

async function handleArchive(card: CardResponse) {
  closeMenu()
  archiveTargetCard.value = card
  showArchiveConfirm.value = true
}

async function confirmArchive() {
  const card = archiveTargetCard.value
  if (!card) return
  const { error } = await api.POST(ApiRoutes.Cards.archive(props.projectId, card.id), {
    body: { version: card.version }
  })
  if (error) {
    toast.add({ title: 'Failed to archive card', color: 'error' })
  } else {
    board.removeCard(card.id)
    toast.add({ title: 'Card archived', color: 'success' })
  }
  archiveTargetCard.value = null
}

const typeIcons: Record<number, string> = {
  0: 'i-lucide-square',
  1: 'i-lucide-bug',
  2: 'i-lucide-layers',
  3: 'i-lucide-file-text',
  4: 'i-lucide-lightbulb'
}

function stripHtml(text: string): string {
  return text.replace(/<[^>]*>/g, '')
}
</script>

<template>
  <div>
    <div class="p-4 space-y-6">
      <div
        v-for="column in columns"
        :key="column.id"
      >
        <div class="flex items-center gap-2 mb-3 pb-2 border-b border-gray-200 dark:border-gray-700">
          <h3 class="font-semibold text-sm">
            {{ column.name }}
          </h3>
          <span class="text-xs text-gray-400 bg-gray-100 dark:bg-gray-700 rounded px-1.5 py-0.5">
            {{ cardsByColumn.get(column.id)?.length ?? 0 }}
          </span>
          <span
            v-if="column.wipLimit"
            class="text-xs"
            :class="Number(column.wipLimit) < (cardsByColumn.get(column.id)?.length ?? 0) ? 'text-red-500 font-medium' : 'text-gray-400'"
          >
            WIP {{ column.wipLimit }}
          </span>
        </div>

        <div class="space-y-2">
          <div
            v-for="card in cardsByColumn.get(column.id) ?? []"
            :key="card.id"
            class="bg-white dark:bg-gray-800 rounded border border-gray-200 dark:border-gray-700 p-3 cursor-pointer hover:shadow-sm transition-shadow"
            @click="emit('card-click', card)"
          >
            <div class="flex items-start gap-2">
              <UIcon
                :name="typeIcons[card.type] ?? 'i-lucide-square'"
                class="size-4 mt-0.5 shrink-0 text-gray-400"
              />
              <div class="flex-1 min-w-0">
                <div class="flex items-center gap-2">
                  <span class="text-xs font-medium text-gray-500 shrink-0">#{{ card.cardNumber }}</span>
                  <p class="text-sm font-medium text-gray-900 dark:text-gray-100 truncate">
                    {{ card.title }}
                  </p>
                  <span
                    v-if="card.archivedAt"
                    class="text-xs text-gray-400 shrink-0"
                  >
                    archived
                  </span>
                </div>
                <p
                  v-if="card.description"
                  class="text-xs text-gray-500 mt-1 line-clamp-2"
                >
                  {{ stripHtml(card.description) }}
                </p>
              </div>

              <!-- Three-dot menu -->
              <div class="relative shrink-0">
                <span data-menu-btn>
                  <UButton
                    icon="i-lucide-ellipsis-vertical"
                    variant="ghost"
                    size="xs"
                    @click.stop="toggleMenu(card.id)"
                  />
                </span>
                <div
                  v-if="selectedMenuCardId === card.id"
                  data-menu-id
                  class="absolute right-0 top-full mt-1 bg-white dark:bg-gray-800 rounded-md border border-gray-200 dark:border-gray-700 shadow-lg py-1 z-50 min-w-[140px]"
                  @click.stop
                >
                  <button
                    class="w-full text-left px-3 py-1.5 text-sm hover:bg-gray-100 dark:hover:bg-gray-700 flex items-center gap-2 text-red-600 dark:text-red-400"
                    @click="handleArchive(card)"
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

            <div
              v-if="card.assignees.length > 0"
              class="flex items-center justify-between mt-2"
            >
              <div class="flex -space-x-1">
                <div
                  v-for="assignee in card.assignees.slice(0, 3)"
                  :key="assignee.userId"
                  class="size-5 rounded-full bg-primary text-white flex items-center justify-center text-xs"
                  :title="assignee.username"
                >
                  {{ (assignee.username[0] ?? '?').toUpperCase() }}
                </div>
              </div>
              <div
                v-if="card.dueAt"
                class="text-xs"
                :class="new Date(card.dueAt) < new Date() ? 'text-red-500 font-medium' : 'text-gray-400'"
              >
                {{ new Date(card.dueAt).toLocaleDateString('en-US', { month: 'short', day: 'numeric' }) }}
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>

    <ConfirmDialog
      v-model:open="showArchiveConfirm"
      title="Archive card"
      :message="archiveTargetCard ? `Archive #${archiveTargetCard.cardNumber} ${archiveTargetCard.title}?` : ''"
      confirm-text="Archive"
      @confirm="confirmArchive"
    />
  </div>
</template>
