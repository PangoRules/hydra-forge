import { defineStore } from 'pinia'
import { useDebounceFn } from '@vueuse/core'

const LS_KEY = 'hydraforge:cardPopup:state'

interface StoredState {
  openCardIds: string[]
  positions: Record<string, { x: number, y: number }>
  sizes: Record<string, { width: number, height: number }>
  cardProjectIds: Record<string, string>
}

function loadState(): StoredState | null {
  if (typeof localStorage === 'undefined') return null
  try {
    const raw = localStorage.getItem(LS_KEY)
    return raw ? JSON.parse(raw) : null
  } catch { return null }
}

function saveState(
  ids: string[],
  pos: Record<string, { x: number, y: number }>,
  sizes: Record<string, { width: number, height: number }>,
  projIds: Record<string, string>
) {
  if (typeof localStorage === 'undefined') return
  try {
    localStorage.setItem(LS_KEY, JSON.stringify({ openCardIds: ids, positions: pos, sizes, cardProjectIds: projIds }))
  } catch { /* quota exceeded — non-fatal */ }
}

export const useCardPopupStore = defineStore('cardPopup', () => {
  const saved = loadState()

  // Drop any openCardId that has no corresponding cardProjectIds entry — these
  // are stale entries from persisted state before cardProjectIds was added
  // (pre-plan-20 upgrade). Without this, getProjectId returns null permanently
  // and every CardPopup hangs on "Loading…" forever after a hard reload.
  const savedCardProjectIds = saved?.cardProjectIds ?? {}
  const validOpenCardIds = (saved?.openCardIds ?? []).filter(id => id in savedCardProjectIds)

  const openCardIds = ref<string[]>(validOpenCardIds)
  const activeCardId = ref<string | null>(null)

  // Filter positions to only include cards that are in validOpenCardIds, so a hard
  // reload doesn't restore orphaned position entries for cards that were never saved
  // with a cardProjectIds (stale pre-upgrade state).
  const validPositions = saved?.positions ?? {}
  const validOpenCardIdsSet = new Set(validOpenCardIds)
  const positions = ref<Record<string, { x: number, y: number }>>(
    Object.fromEntries(Object.entries(validPositions).filter(([k]) => validOpenCardIdsSet.has(k)))
  )
  const validSizes = saved?.sizes ?? {}
  const sizes = ref<Record<string, { width: number, height: number }>>(
    Object.fromEntries(Object.entries(validSizes).filter(([k]) => validOpenCardIdsSet.has(k)))
  )
  const cardProjectIds = ref<Record<string, string>>(savedCardProjectIds)

  const toast = useAppToast()
  const popupZ = usePopupZIndex()

  // Max cards
  const canOpen = computed(() => openCardIds.value.length < 3)

  // Cascade offset for new popups — each new card opens 24px down-right from the last one.
  // Resets to a default anchor when the stack is empty.
  const DEFAULT_ANCHOR = { x: 48, y: 48 }
  const CASCADE_OFFSET = 24

  // Sized for full desktop screens by default (previous 520px felt cramped) —
  // still clamped to the viewport by CardPopup on mount for smaller windows.
  const DEFAULT_SIZE = { width: 680, height: 640 }

  function getSize(cardId: string): { width: number, height: number } {
    return sizes.value[cardId] ?? DEFAULT_SIZE
  }

  function resizeCard(cardId: string, size: { width: number, height: number }) {
    if (!openCardIds.value.includes(cardId)) return
    sizes.value[cardId] = size
  }

  function nextPosition(): { x: number, y: number } {
    if (openCardIds.value.length === 0) return { ...DEFAULT_ANCHOR }
    const lastId = openCardIds.value[openCardIds.value.length - 1]!
    const lastPos = positions.value[lastId] ?? DEFAULT_ANCHOR
    return { x: lastPos.x + CASCADE_OFFSET, y: lastPos.y + CASCADE_OFFSET }
  }

  function openCard(cardId: string, projectId?: string) {
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

    const pos = nextPosition()
    openCardIds.value = [...openCardIds.value, cardId]
    positions.value[cardId] = pos
    sizes.value[cardId] = { ...DEFAULT_SIZE }
    activeCardId.value = cardId
    if (projectId) cardProjectIds.value[cardId] = projectId

    popupZ.registerPopup(cardId, 'card', () => closeCard(cardId))
    popupZ.bringToFront(cardId)
  }

  function closeCard(cardId: string) {
    openCardIds.value = openCardIds.value.filter(id => id !== cardId)
    Reflect.deleteProperty(positions.value, cardId)
    Reflect.deleteProperty(sizes.value, cardId)
    Reflect.deleteProperty(cardProjectIds.value, cardId)
    popupZ.unregisterPopup(cardId)

    // If the closed card was the active one, set active to the new topmost.
    if (activeCardId.value === cardId) {
      activeCardId.value = openCardIds.value[openCardIds.value.length - 1] ?? null
    }
  }

  function closeTopmost() {
    popupZ.closeTopmost()
  }

  // Closes every open card popup without going through the dock's Escape
  // stack — used when navigating away from the project board that owns
  // them (a different project, or off the board entirely). Unlike
  // closeCard, this doesn't reassign activeCardId per-removal since
  // everything is going away.
  function closeAll() {
    for (const cardId of openCardIds.value) {
      popupZ.unregisterPopup(cardId)
    }
    openCardIds.value = []
    positions.value = {}
    sizes.value = {}
    cardProjectIds.value = {}
    activeCardId.value = null
  }

  function setActive(cardId: string) {
    if (activeCardId.value === cardId) return
    activeCardId.value = cardId
  }

  function bringToFront(cardId: string) {
    if (!openCardIds.value.includes(cardId)) return
    // No-op if already topmost — avoids replacing openCardIds (and the
    // reactive churn/localStorage write that follows) on every ordinary
    // click inside the frontmost popup, which fights an in-progress drag.
    if (openCardIds.value[openCardIds.value.length - 1] === cardId) return
    openCardIds.value = openCardIds.value.filter(id => id !== cardId)
    openCardIds.value = [...openCardIds.value, cardId]
    popupZ.bringToFront(cardId)
  }

  function getProjectId(cardId: string): string | null {
    return cardProjectIds.value[cardId] ?? null
  }

  // Persist openCardIds + positions + sizes + cardProjectIds to localStorage
  // (same pattern as chatDock's LS_ACTIVE_SESSION_KEY watcher — Pinia survives client-side
  // navigation but a hard reload resets it, localStorage bridges the gap).
  // Debounced: positions/sizes change on every pixel during a drag/resize, and a
  // synchronous localStorage write per pixel visibly jankifies the gesture.
  const persist = useDebounceFn(() => {
    saveState(openCardIds.value, positions.value, sizes.value, cardProjectIds.value)
  }, 250)
  watch([openCardIds, positions, sizes, cardProjectIds], persist, { deep: true })

  return {
    openCardIds, activeCardId, positions, sizes, canOpen,
    openCard, closeCard, closeAll, closeTopmost, setActive, bringToFront,
    getProjectId, getSize, resizeCard
  }
})
