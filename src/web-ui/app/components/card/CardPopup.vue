<!-- src/web-ui/app/components/card/CardPopup.vue -->
<script setup lang="ts">
import { useDraggable } from '@vueuse/core'

const props = defineProps<{ cardId: string }>()

const cardPopup = useCardPopupStore()
const popupZ = usePopupZIndex()

const dragHandle = ref<HTMLElement | null>(null)
const popupRef = ref<HTMLElement | null>(null)

const POPUP_WIDTH_PX = 520
const POPUP_MIN_HEIGHT_PX = 300

// Initial position from the store (set by openCard's nextPosition() cascade).
// Falls back to a sensible default if the position isn't set (shouldn't happen).
const initialPos = cardPopup.positions[props.cardId] ?? { x: 48, y: 48 }

const { x, y } = useDraggable(popupRef, {
  handle: dragHandle,
  initialValue: () => ({ ...initialPos }),
  preventDefault: true,
  // Constrain within viewport — don't let the user drag it fully off-screen.
  onMove(position) {
    const el = popupRef.value
    if (!el) return position
    const rect = el.getBoundingClientRect()
    const maxX = Math.max(0, window.innerWidth - rect.width)
    const maxY = Math.max(0, window.innerHeight - 48) // leave 48px for bottom bar / dock FAB
    return {
      x: Math.max(0, Math.min(position.x, maxX)),
      y: Math.max(0, Math.min(position.y, maxY))
    }
  }
})

// Write drag position back to the store so Plan 20's re-mounts restore the exact spot.
watch([x, y], ([nx, ny]) => {
  if (cardPopup.positions[props.cardId]) {
    cardPopup.positions[props.cardId] = { x: nx, y: ny }
  }
})

// Bring this popup to front on mousedown/focusin anywhere inside it — ensures
// the user clicking a card popup to interact with it also raises its z-index.
function handlePointerDown() {
  cardPopup.setActive(props.cardId)
  cardPopup.bringToFront(props.cardId)
}

// Track min-height via CSS custom property so the popup is never shorter than
// 300px, but can grow with content.
const minHeightStyle = computed(() => `${POPUP_MIN_HEIGHT_PX}px`)
</script>

<template>
  <ClientOnly>
    <div
      :ref="popupRef"
      class="fixed bg-white dark:bg-gray-900 border border-gray-200 dark:border-gray-700 rounded-lg shadow-2xl flex flex-col overflow-hidden"
      :style="{
        width: `${POPUP_WIDTH_PX}px`,
        minHeight: minHeightStyle,
        maxHeight: 'calc(100vh - 2rem)',
        maxWidth: 'calc(100vw - 1rem)',
        left: `${x}px`,
        top: `${y}px`,
        zIndex: popupZ.zIndexFor(cardId)
      }"
      @mousedown="handlePointerDown"
      @focusin="handlePointerDown"
    >
      <!-- Header — drag handle + close button -->
      <div
        :ref="dragHandle"
        class="shrink-0 flex items-center justify-between px-4 py-2 border-b border-gray-200 dark:border-gray-700 cursor-move select-none"
      >
        <!-- Title placeholder — Plan 20 replaces this with the card title -->
        <span class="text-sm font-medium truncate">
          Card {{ cardId.slice(0, 8) }}…
        </span>
        <UButton
          icon="i-lucide-x"
          variant="ghost"
          size="xs"
          title="Close"
          aria-label="Close card popup"
          @click.stop="cardPopup.closeCard(cardId)"
        />
      </div>

      <!-- Body — default slot. Plan 20 fills this with the migrated CardModal body. -->
      <div class="flex-1 min-h-0 overflow-y-auto p-4">
        <slot>
          <!-- Placeholder until Plan 20 slots in real content -->
          <p class="text-sm text-gray-500">
            Card {{ cardId.slice(0, 8) }}… — body arrives in Plan 20
          </p>
        </slot>
      </div>
    </div>
  </ClientOnly>
</template>
