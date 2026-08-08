import { ref, watch } from 'vue'

interface PopupEntry {
  id: string
  kind: 'card' | 'dock'
  close: () => void
}

const stack = ref<PopupEntry[]>([])
const baseZ = 50

let listenerRegistered = false

function ensureListener() {
  if (listenerRegistered) return
  if (typeof document === 'undefined') return
  listenerRegistered = true
  document.addEventListener('keydown', onGlobalKeydown)
}

function removeListener() {
  if (!listenerRegistered) return
  listenerRegistered = false
  if (typeof document === 'undefined') return
  document.removeEventListener('keydown', onGlobalKeydown)
}

function unregisterPopup(id: string) {
  stack.value = stack.value.filter(e => e.id !== id)
  if (stack.value.length === 0) removeListener()
}

function closeTopmost() {
  const top = stack.value[stack.value.length - 1]
  if (top) {
    top.close()
    setTimeout(() => {
      if (stack.value.length && stack.value[stack.value.length - 1]?.id === top.id) {
        unregisterPopup(top.id)
      }
    }, 0)
  }
}

function onGlobalKeydown(e: KeyboardEvent) {
  if (e.key !== 'Escape') return
  const activeEl = document.activeElement
  if (activeEl && activeEl instanceof HTMLElement) {
    const role = activeEl.closest('[role="dialog"], [role="listbox"], [role="menu"]')
    if (role) return
  }
  closeTopmost()
}

watch(
  () => stack.value.length,
  (len) => {
    if (len > 0) ensureListener()
    else removeListener()
  }
)

export function usePopupZIndex() {
  function registerPopup(id: string, kind: 'card' | 'dock', closeFn: () => void) {
    const existing = stack.value.findIndex(e => e.id === id)
    if (existing !== -1) {
      stack.value[existing]!.close = closeFn
      return
    }
    stack.value.push({ id, kind, close: closeFn })
    ensureListener()
  }

  function bringToFront(id: string) {
    const idx = stack.value.findIndex(e => e.id === id)
    if (idx === -1) return
    const [entry] = stack.value.splice(idx, 1)
    stack.value.push(entry!)
  }

  const zIndexFor = (id: string): number => {
    const idx = stack.value.findIndex(e => e.id === id)
    if (idx === -1) return baseZ
    return baseZ + idx
  }

  return { stack, registerPopup, unregisterPopup, bringToFront, closeTopmost, zIndexFor }
}
