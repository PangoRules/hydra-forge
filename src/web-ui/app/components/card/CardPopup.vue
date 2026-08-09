<!-- src/web-ui/app/components/card/CardPopup.vue -->
<script setup lang="ts">
import { useDraggable } from '@vueuse/core'
import CardPopupBody from '~/components/card/CardPopupBody.vue'

const props = defineProps<{ cardId: string }>()
const emit = defineEmits<{
  archived: []
  restored: []
}>()

const cardPopup = useCardPopupStore()
const popupZ = usePopupZIndex()

const dragHandle = ref<HTMLElement | null>(null)
const popupRef = ref<HTMLElement | null>(null)
const bodyRef = ref<InstanceType<typeof CardPopupBody> | null>(null)

const MIN_WIDTH_PX = 440
const MIN_HEIGHT_PX = 320
const VIEWPORT_MARGIN_PX = 16

const cardTitle = ref('')
const projectId = computed(() => cardPopup.getProjectId(props.cardId) ?? '')
const readonly = computed(() => cardPopup.isProjectArchived(props.cardId))

// Initial position/size from the store (position set by openCard's nextPosition()
// cascade, size defaulted by openCard to DEFAULT_SIZE). Falls back to sensible
// defaults if missing (shouldn't happen).
const initialPos = cardPopup.positions[props.cardId] ?? { x: 48, y: 48 }
const initialSize = cardPopup.getSize(props.cardId)

// Clamp against the current viewport — a position/size saved via localStorage on a
// larger window/monitor would otherwise mount off-screen (or oversized) with no
// way to drag/resize it back (onMove only clamps live drags, not the initial mount).
function clampToViewport(pos: { x: number, y: number }, size: { width: number, height: number }): { x: number, y: number } {
  if (typeof window === 'undefined') return pos
  const maxX = Math.max(0, window.innerWidth - size.width)
  const maxY = Math.max(0, window.innerHeight - 48)
  return {
    x: Math.max(0, Math.min(pos.x, maxX)),
    y: Math.max(0, Math.min(pos.y, maxY))
  }
}

function clampSize(size: { width: number, height: number }): { width: number, height: number } {
  if (typeof window === 'undefined') return size
  return {
    width: Math.min(Math.max(MIN_WIDTH_PX, size.width), window.innerWidth - VIEWPORT_MARGIN_PX),
    height: Math.min(Math.max(MIN_HEIGHT_PX, size.height), window.innerHeight - VIEWPORT_MARGIN_PX)
  }
}

const size = ref(clampSize(initialSize))

const { x, y } = useDraggable(popupRef, {
  handle: dragHandle,
  initialValue: () => clampToViewport(initialPos, size.value),
  preventDefault: true,
  // Constrain within viewport — don't let the user drag it fully off-screen.
  // vueuse ignores onMove's return value, so the clamp has to write the refs
  // directly (x/y are the same live refs returned below via toRefs(position)).
  onMove(position) {
    const maxX = Math.max(0, window.innerWidth - size.value.width)
    const maxY = Math.max(0, window.innerHeight - 48) // leave 48px for bottom bar / dock FAB
    x.value = Math.max(0, Math.min(position.x, maxX))
    y.value = Math.max(0, Math.min(position.y, maxY))
  }
})

// Write drag position back to the store so re-mounts restore the exact spot.
watch([x, y], ([nx, ny]) => {
  if (cardPopup.positions[props.cardId]) {
    cardPopup.positions[props.cardId] = { x: nx, y: ny }
  }
})

// ── Resize (bottom-right corner handle) ──────────────────────────────────────
let resizing = false
let resizeStartX = 0
let resizeStartY = 0
let resizeStartWidth = 0
let resizeStartHeight = 0

function onResizePointerMove(e: PointerEvent) {
  if (!resizing) return
  const dx = e.clientX - resizeStartX
  const dy = e.clientY - resizeStartY
  const maxWidth = Math.max(MIN_WIDTH_PX, window.innerWidth - x.value - VIEWPORT_MARGIN_PX)
  const maxHeight = Math.max(MIN_HEIGHT_PX, window.innerHeight - y.value - VIEWPORT_MARGIN_PX)
  size.value = {
    width: Math.min(maxWidth, Math.max(MIN_WIDTH_PX, resizeStartWidth + dx)),
    height: Math.min(maxHeight, Math.max(MIN_HEIGHT_PX, resizeStartHeight + dy))
  }
  cardPopup.resizeCard(props.cardId, size.value)
}

function onResizePointerUp() {
  resizing = false
  window.removeEventListener('pointermove', onResizePointerMove)
  window.removeEventListener('pointerup', onResizePointerUp)
}

function onResizePointerDown(e: PointerEvent) {
  e.preventDefault()
  e.stopPropagation()
  resizing = true
  resizeStartX = e.clientX
  resizeStartY = e.clientY
  resizeStartWidth = size.value.width
  resizeStartHeight = size.value.height
  cardPopup.setActive(props.cardId)
  cardPopup.bringToFront(props.cardId)
  window.addEventListener('pointermove', onResizePointerMove)
  window.addEventListener('pointerup', onResizePointerUp)
}

onBeforeUnmount(() => {
  window.removeEventListener('pointermove', onResizePointerMove)
  window.removeEventListener('pointerup', onResizePointerUp)
})

// Bring this popup to front on pointerdown/focusin anywhere inside it — ensures
// the user clicking a card popup to interact with it also raises its z-index.
// Listens for 'pointerdown', not 'mousedown': the header is useDraggable's drag
// handle with preventDefault:true, and per the Pointer Events spec a
// preventDefault()-ed pointerdown suppresses the synthesized compatibility
// mousedown that would otherwise follow — so clicking the header (the single
// most natural place to click a background popup to bring it forward, like a
// window titlebar) would silently never fire a 'mousedown' listener here.
// 'pointerdown' fires first and unconditionally, before that suppression, and
// isn't affected by preventDefault() called by a different listener.
function handlePointerDown() {
  cardPopup.setActive(props.cardId)
  cardPopup.bringToFront(props.cardId)
}
</script>

<template>
  <ClientOnly>
    <div
      ref="popupRef"
      class="fixed bg-white dark:bg-gray-900 border border-gray-200 dark:border-gray-700 rounded-lg shadow-2xl flex flex-col overflow-hidden"
      :style="{
        width: `${size.width}px`,
        height: `${size.height}px`,
        maxHeight: 'calc(100vh - 1rem)',
        maxWidth: 'calc(100vw - 1rem)',
        left: `${x}px`,
        top: `${y}px`,
        zIndex: popupZ.zIndexFor(cardId)
      }"
      @pointerdown="handlePointerDown"
      @focusin="handlePointerDown"
    >
      <!-- Header — drag handle + watch/archive/restore + close, all one row -->
      <div
        ref="dragHandle"
        class="shrink-0 flex items-center justify-between px-4 py-2 border-b border-gray-200 dark:border-gray-700 cursor-move select-none"
      >
        <span class="text-sm font-medium truncate">
          {{ cardTitle || `Card ${cardId.slice(0, 8)}…` }}
        </span>
        <div class="flex items-center gap-1 shrink-0">
          <UButton
            v-if="bodyRef?.card"
            variant="ghost"
            size="xs"
            :icon="bodyRef.isWatching ? 'i-lucide-eye-off' : 'i-lucide-eye'"
            :color="bodyRef.isWatching ? 'primary' : 'neutral'"
            :title="bodyRef.isWatching ? 'Watching — click to unwatch' : 'Not watching — click to watch'"
            @click.stop="bodyRef?.toggleWatch()"
          />
          <UButton
            v-if="bodyRef?.card && !bodyRef.readonly && !bodyRef.isArchived"
            variant="ghost"
            size="xs"
            icon="i-lucide-archive"
            title="Archive card"
            @click.stop="bodyRef?.handleArchive()"
          />
          <UButton
            v-if="bodyRef?.card && !bodyRef.readonly && bodyRef.isArchived"
            variant="ghost"
            size="xs"
            icon="i-lucide-archive-restore"
            title="Restore card"
            @click.stop="bodyRef?.handleRestore()"
          />
          <UButton
            icon="i-lucide-x"
            variant="ghost"
            size="xs"
            title="Close"
            aria-label="Close card popup"
            @click.stop="cardPopup.closeCard(cardId)"
          />
        </div>
      </div>

      <!-- Body -->
      <div class="flex-1 min-h-0 overflow-hidden">
        <CardPopupBody
          v-if="projectId"
          ref="bodyRef"
          :card-id="cardId"
          :project-id="projectId"
          :readonly="readonly"
          @close="cardPopup.closeCard(cardId)"
          @archived="emit('archived')"
          @restored="emit('restored')"
          @title-loaded="cardTitle = $event"
        />
        <div
          v-else
          class="flex items-center justify-center h-full"
        >
          <p class="text-sm text-muted">
            Loading...
          </p>
        </div>
      </div>

      <!-- Resize handle — bottom-right corner -->
      <div
        class="absolute bottom-0 right-0 w-4 h-4 cursor-nwse-resize touch-none"
        title="Resize"
        @pointerdown="onResizePointerDown"
      >
        <svg
          class="absolute bottom-0.5 right-0.5 text-gray-400 dark:text-gray-500"
          width="10"
          height="10"
          viewBox="0 0 10 10"
        >
          <path
            d="M9 1 1 9M9 5 5 9M9 9 9 9"
            stroke="currentColor"
            stroke-width="1.2"
            stroke-linecap="round"
          />
        </svg>
      </div>
    </div>
  </ClientOnly>
</template>
