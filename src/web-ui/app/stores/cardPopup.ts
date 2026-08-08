import { defineStore } from 'pinia'

const LS_KEY = 'hydraforge:cardPopup:state'

interface StoredState {
  openCardIds: string[]
  positions: Record<string, { x: number, y: number }>
}

function loadState(): StoredState | null {
  if (typeof localStorage === 'undefined') return null
  try {
    const raw = localStorage.getItem(LS_KEY)
    return raw ? JSON.parse(raw) : null
  } catch { return null }
}

function saveState(ids: string[], pos: Record<string, { x: number, y: number }>) {
  if (typeof localStorage === 'undefined') return
  try {
    localStorage.setItem(LS_KEY, JSON.stringify({ openCardIds: ids, positions: pos }))
  } catch { /* quota exceeded — non-fatal */ }
}

export const useCardPopupStore = defineStore('cardPopup', () => {
  const saved = loadState()

  const openCardIds = ref<string[]>(saved?.openCardIds ?? [])
  const activeCardId = ref<string | null>(null)
  const positions = ref<Record<string, { x: number, y: number }>>(saved?.positions ?? {})

  const toast = useAppToast()
  const popupZ = usePopupZIndex()

  // Max cards
  const canOpen = computed(() => openCardIds.value.length < 3)

  // Cascade offset for new popups — each new card opens 24px down-right from the last one.
  // Resets to a default anchor when the stack is empty.
  const DEFAULT_ANCHOR = { x: 48, y: 48 }
  const CASCADE_OFFSET = 24

  function nextPosition(): { x: number, y: number } {
    if (openCardIds.value.length === 0) return { ...DEFAULT_ANCHOR }
    const lastId = openCardIds.value[openCardIds.value.length - 1]!
    const lastPos = positions.value[lastId] ?? DEFAULT_ANCHOR
    return { x: lastPos.x + CASCADE_OFFSET, y: lastPos.y + CASCADE_OFFSET }
  }

  function openCard(cardId: string) {
    // Already open — activate and bring to front (no-op duplicate)
    if (openCardIds.value.includes(cardId)) {
      setActive(cardId)
      bringToFront(cardId)
      return
    }

    // Max 3 enforcement
    if (openCardIds.value.length >= 3) {
      toast.error('Close a card popup first (max 3 open)')
      return
    }

    openCardIds.value = [...openCardIds.value, cardId]
    positions.value[cardId] = nextPosition()
    activeCardId.value = cardId

    popupZ.registerPopup(cardId, 'card', () => closeCard(cardId))
    popupZ.bringToFront(cardId)
  }

  function closeCard(cardId: string) {
    openCardIds.value = openCardIds.value.filter(id => id !== cardId)
    Reflect.deleteProperty(positions.value, cardId)
    popupZ.unregisterPopup(cardId)

    // If the closed card was the active one, set active to the new topmost.
    if (activeCardId.value === cardId) {
      activeCardId.value = openCardIds.value[openCardIds.value.length - 1] ?? null
    }
  }

  function closeTopmost() {
    popupZ.closeTopmost()
  }

  function setActive(cardId: string) {
    activeCardId.value = cardId
  }

  function bringToFront(cardId: string) {
    if (!openCardIds.value.includes(cardId)) return
    openCardIds.value = openCardIds.value.filter(id => id !== cardId)
    openCardIds.value = [...openCardIds.value, cardId]
    popupZ.bringToFront(cardId)
  }

  // Persist openCardIds + positions to localStorage on every change (same pattern as
  // chatDock's LS_ACTIVE_SESSION_KEY watcher — Pinia survives client-side navigation
  // but a hard reload resets it, localStorage bridges the gap).
  watch([openCardIds, positions], () => {
    saveState(openCardIds.value, positions.value)
  }, { deep: true })

  return {
    openCardIds, activeCardId, positions, canOpen,
    openCard, closeCard, closeTopmost, setActive, bringToFront
  }
})
