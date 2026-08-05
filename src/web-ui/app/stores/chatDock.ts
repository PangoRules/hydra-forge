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
    if (route.path.match(/^\/projects\/[^/]+\/board/)) {
      return route.params.id as string
    }
    return null
  })

  function toggleDock() {
    isOpen.value = !isOpen.value
  }

  function openDock() {
    isOpen.value = true
    // Resume last active session from localStorage
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
    // Preserve activeSessionId in localStorage for resume
    if (activeSessionId.value) {
      localStorage.setItem(LS_ACTIVE_SESSION_KEY, activeSessionId.value)
    }
  }

  function loadSession(sessionId: string) {
    activeSessionId.value = sessionId
    mode.value = 'session'
    pendingMessage.value = null
    localStorage.setItem(LS_ACTIVE_SESSION_KEY, sessionId)
  }

  function newChat() {
    activeSessionId.value = null
    pendingMessage.value = null
    localStorage.removeItem(LS_ACTIVE_SESSION_KEY)
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
    reasoningEffort?: string | null
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
        localStorage.setItem(LS_ACTIVE_SESSION_KEY, data.id)
      }
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : 'Failed to create chat session')
    } finally {
      isCreating.value = false
    }
  }

  return {
    isOpen, mode, activeSessionId, isCreating, position, currentProjectId, pendingMessage,
    toggleDock, openDock, closeDock, loadSession, newChat, showHistory, hideHistory, startNewChat, clearPendingMessage
  }
})
