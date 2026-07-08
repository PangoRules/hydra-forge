import { onMounted, onUnmounted } from 'vue'

interface Shortcut {
  key: string
  handler: (e: KeyboardEvent) => void
  description: string
  scope: string
}

// Module-level shortcuts list so all instances share the same list
const shortcuts: Shortcut[] = []

export function useKeyboard() {
  function register(scope: string, key: string, handler: (e: KeyboardEvent) => void, description: string) {
    shortcuts.push({ key, handler, description, scope })
  }

  function unregister(scope: string) {
    const initialLength = shortcuts.length
    const filtered = shortcuts.filter(s => s.scope !== scope)
    if (filtered.length !== initialLength) {
      shortcuts.length = 0
      shortcuts.push(...filtered)
    }
  }

  function getShortcuts(scope: string) {
    return shortcuts.filter(s => s.scope === scope)
  }

  function getAllShortcuts() {
    return shortcuts
  }

  function handleKeyDown(event: KeyboardEvent) {
    // Skip shortcuts when typing in INPUT/TEXTAREA/contentEditable (except Escape)
    if ((event.target instanceof HTMLElement)
      && (['INPUT', 'TEXTAREA'].includes(event.target.nodeName)
        || event.target.isContentEditable)) {
      if (event.key !== 'Escape') {
        return
      }
    }

    // Find the last registered shortcut that matches (last wins for same key)
    const matchingShortcut = [...shortcuts]
      .reverse()
      .find(s => s.key === event.key)

    if (matchingShortcut) {
      event.preventDefault()
      event.stopPropagation()
      matchingShortcut.handler(event)
    }
  }

  onMounted(() => {
    window.addEventListener('keydown', handleKeyDown)
  })

  onUnmounted(() => {
    window.removeEventListener('keydown', handleKeyDown)
  })

  return {
    register,
    unregister,
    getShortcuts,
    getAllShortcuts
  }
}
