import { defineStore } from 'pinia'
import { ApiError } from '~/lib/api-error'
import { ApiRoutes } from '~/lib/routes'
import type { ChatSessionDto } from '~/types/chat'

export const useChatDockStore = defineStore('chatDock', () => {
  const isOpen = ref(false)
  const activeSessionId = ref<string | null>(null)
  const isCreating = ref(false)
  const position = ref({ x: 0, y: 0 })

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
  }

  function closeDock() {
    isOpen.value = false
  }

  async function startNewChat() {
    if (isCreating.value) return
    isCreating.value = true
    try {
      const body: Record<string, unknown> = { title: 'New chat' }
      if (currentProjectId.value) body.projectId = currentProjectId.value
      const { data } = await api.POST<ChatSessionDto>(ApiRoutes.Chat.sessions.create(), { body })
      if (data) {
        activeSessionId.value = data.id
      }
    }
    catch (err) {
      toast.error(err instanceof ApiError ? err.message : 'Failed to create chat session')
    }
    finally {
      isCreating.value = false
    }
  }

  return {
    isOpen,
    activeSessionId,
    isCreating,
    position,
    currentProjectId,
    toggleDock,
    openDock,
    closeDock,
    startNewChat
  }
})
