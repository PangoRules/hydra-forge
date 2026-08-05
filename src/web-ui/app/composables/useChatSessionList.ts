import { ApiRoutes } from '~/lib/routes'
import type { ChatSessionDto } from '~/types/chat'

interface ChatSessionPageDto {
  items: ChatSessionDto[]
  totalCount: number
}

export function useChatSessionList(options?: { folderId?: string, projectId?: string }) {
  const sessions = ref<ChatSessionDto[]>([])
  const loading = ref(false)
  const hasMore = ref(true)
  const api = useApi()

  async function loadMore() {
    if (loading.value || !hasMore.value) return
    loading.value = true
    try {
      const last = sessions.value[sessions.value.length - 1]
      const url = ApiRoutes.Chat.sessions.list(
        options?.folderId,
        options?.projectId,
        last?.updatedAt,
        last?.id,
        20
      )
      const { data } = await api.GET<ChatSessionPageDto>(url)
      if (data) {
        sessions.value.push(...data.items)
        hasMore.value = sessions.value.length < data.totalCount
      }
    } catch {
      // Silently fail — next scroll attempt will retry
    } finally {
      loading.value = false
    }
  }

  async function refresh() {
    sessions.value = []
    hasMore.value = true
    await loadMore()
  }

  // In-place update for a single session already in the loaded list — avoids
  // dropping the caller back to page 1 (which a full refresh() would do) just
  // to reflect a title/status change on an item that's already visible.
  function patchSession(id: string, patch: Partial<ChatSessionDto>) {
    const index = sessions.value.findIndex(s => s.id === id)
    if (index !== -1) sessions.value[index] = { ...sessions.value[index]!, ...patch }
  }

  // A brand-new session always sorts newest-first — prepend it locally instead
  // of refresh()ing. refresh() resets to page 1, which visibly "disappears"
  // any already-loaded pages 2+ if the user had scrolled into older history —
  // this keeps everything they'd already loaded intact.
  function prependSession(session: ChatSessionDto) {
    sessions.value.unshift(session)
  }

  // Same reasoning as prependSession — removing an archived item locally
  // avoids collapsing back to page 1 for anyone who'd scrolled further.
  function removeSession(id: string) {
    const index = sessions.value.findIndex(s => s.id === id)
    if (index !== -1) sessions.value.splice(index, 1)
  }

  // Load initial page
  loadMore()

  return {
    sessions: readonly(sessions),
    loading: readonly(loading),
    hasMore: readonly(hasMore),
    loadMore,
    refresh,
    patchSession,
    prependSession,
    removeSession
  }
}
