import { defineStore } from 'pinia'
import { ApiError } from '~/lib/api-error'
import { ApiRoutes } from '~/lib/routes'
import type { ChatSessionDto } from '~/types/chat'

const LS_ACTIVE_SESSION_KEY = 'hydraforge:chat:activeSessionId'

const TITLE_MAX_LENGTH = 60

function deriveTitle(content: string): string {
  const firstLine = content.trim().split('\n')[0] ?? content.trim()
  return firstLine.length > TITLE_MAX_LENGTH
    ? `${firstLine.slice(0, TITLE_MAX_LENGTH)}…`
    : firstLine
}

export const useChatDockStore = defineStore('chatDock', () => {
  const isOpen = ref(false)
  const mode = ref<'draft' | 'session' | 'history'>('draft')
  const activeSessionId = ref<string | null>(null)
  const isCreating = ref(false)
  const position = ref({ x: 0, y: 0 })
  const isFullHeight = ref(false)
  const pendingMessage = ref<{
    content: string
    presetId: string | null
    modelId: string | null
    reasoningEffort: string | null
  } | null>(null)

  const route = useRoute()
  const api = useApi()
  const toast = useAppToast()

  const currentProjectId = computed(() => {
    if (route.path.match(/^\/projects\/[^/]+\/?$/)) {
      return route.params.id as string
    }
    return null
  })

  function toggleDock() {
    isOpen.value = !isOpen.value
  }

  function openDock() {
    isOpen.value = true
    // Resume last active session from localStorage — only if we don't already
    // have one in memory (Pinia store survives client-side navigation, so this
    // is mainly for the first open after a page load / hard reload).
    if (!activeSessionId.value) {
      const saved = localStorage.getItem(LS_ACTIVE_SESSION_KEY)
      if (saved) {
        activeSessionId.value = saved
        mode.value = 'session'
        return
      }
    }
    // Default to draft mode
    if (!activeSessionId.value) {
      mode.value = 'draft'
    }
  }

  function closeDock() {
    isOpen.value = false
    // activeSessionId is persisted by the watcher below — nothing to do here.
  }

  function toggleFullHeight() {
    isFullHeight.value = !isFullHeight.value
  }

  // Persist activeSessionId to localStorage on every change, not just on
  // closeDock(). The dock is a fixed overlay in the layout that survives
  // client-side navigation, but a hard reload / typed URL resets Pinia and
  // falls back to localStorage. Without this watcher, a session selected in
  // the dock but never explicitly "closed" would be lost on reload — closeDock
  // was the only write point, and it only fires on the X button.
  watch(activeSessionId, (id) => {
    if (id) {
      localStorage.setItem(LS_ACTIVE_SESSION_KEY, id)
    } else {
      localStorage.removeItem(LS_ACTIVE_SESSION_KEY)
    }
  })

  function loadSession(sessionId: string) {
    activeSessionId.value = sessionId
    mode.value = 'session'
    pendingMessage.value = null
  }

  function newChat() {
    activeSessionId.value = null
    pendingMessage.value = null
    mode.value = 'draft'
  }

  function clearPendingMessage() {
    pendingMessage.value = null
  }

  function showHistory() {
    mode.value = 'history'
  }

  function hideHistory() {
    mode.value = activeSessionId.value ? 'session' : 'draft'
  }

  async function startNewChat(
    content?: string,
    presetId?: string | null,
    modelId?: string | null,
    reasoningEffort?: string | null,
    personalityId?: string | null
  ) {
    if (isCreating.value) return
    isCreating.value = true
    try {
      // useBoardStore() always succeeds once Pinia is active (lazily creates
      // the store on first call) — no try/catch needed here. On a page with
      // no board mounted, openCardId is simply the store's untouched default (null).
      const openCardId = currentProjectId.value ? useBoardStore().openCardId : null
      const body: Record<string, unknown> = { title: content ? deriveTitle(content) : '' }
      if (currentProjectId.value) body.projectId = currentProjectId.value
      if (openCardId) body.openCardId = openCardId
      if (modelId) body.preferredModelConfigId = modelId
      if (reasoningEffort) body.preferredEffort = reasoningEffort
      if (personalityId) body.personalityId = personalityId
      const { data } = await api.POST<ChatSessionDto>(ApiRoutes.Chat.sessions.create(), { body })
      if (data) {
        activeSessionId.value = data.id
        mode.value = 'session'
        pendingMessage.value = content
          ? {
              content,
              presetId: presetId ?? null,
              modelId: modelId ?? null,
              reasoningEffort: reasoningEffort ?? null
            }
          : null
      }
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : 'Failed to create chat session')
    } finally {
      isCreating.value = false
    }
  }

  return {
    isOpen, mode, activeSessionId, isCreating, position, currentProjectId, pendingMessage, isFullHeight,
    toggleDock, openDock, closeDock, loadSession, newChat, showHistory, hideHistory, startNewChat, clearPendingMessage, toggleFullHeight
  }
})
