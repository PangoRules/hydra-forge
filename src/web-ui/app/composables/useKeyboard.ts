interface Shortcut {
  key: string
  handler: (e: KeyboardEvent) => void
  description: string
  scope: string
}

// Module-level shortcuts list so all instances share the same list
const shortcuts: Shortcut[] = []

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
    .find((s: Shortcut) => s.key === event.key)

  if (matchingShortcut) {
    event.preventDefault()
    event.stopPropagation()
    matchingShortcut.handler(event)
  }
}

// Global listener setup - only one listener per window
let globalListenerAttached = false

export function useKeyboard() {
  function register(scope: string, key: string, handler: (e: KeyboardEvent) => void, description: string) {
    shortcuts.push({ key, handler, description, scope })
  }

  function unregister(scope: string) {
    // Filter out all shortcuts with the given scope
    const filtered = shortcuts.filter((s: Shortcut) => s.scope !== scope)
    // Replace the shortcuts array with the filtered array
    shortcuts.length = 0
    shortcuts.push(...filtered)
  }

  function clear() {
    shortcuts.length = 0
  }

  function getShortcuts(scope: string) {
    return shortcuts.filter((s: Shortcut) => s.scope === scope)
  }

  function getAllShortcuts() {
    return shortcuts
  }

  // Only attach the global listener once
  if (!globalListenerAttached) {
    window.addEventListener('keydown', handleKeyDown)
    globalListenerAttached = true
  }

  return {
    register,
    unregister,
    clear,
    getShortcuts,
    getAllShortcuts
  }
}
