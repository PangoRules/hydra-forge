const STORAGE_KEY = 'hydraforge-sidebar-collapsed'

/**
 * Persists the sidebar's expanded/collapsed state across sessions.
 * Client-only read/write — SSR has no localStorage, so the server always
 * renders expanded and the client corrects itself on hydration.
 */
export function useSidebarCollapse() {
  const collapsed = ref(false)

  if (import.meta.client) {
    const stored = localStorage.getItem(STORAGE_KEY)
    if (stored !== null) collapsed.value = stored === 'true'
  }

  watch(collapsed, (value) => {
    if (import.meta.client) localStorage.setItem(STORAGE_KEY, String(value))
  })

  return { collapsed }
}
