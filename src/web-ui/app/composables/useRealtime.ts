import * as signalR from '@microsoft/signalr'

export function useRealtime() {
  const { getToken } = useAuthToken()
  const board = useBoardStore()
  const config = useRuntimeConfig()

  let connection: signalR.HubConnection | null = null
  let activeProjectId: string | null = null
  let pollInterval: ReturnType<typeof setInterval> | null = null
  const isConnected = ref(false)
  const isReconnecting = ref(false)

  async function connect(projectId: string) {
    const token = getToken()
    if (!token) return

    activeProjectId = projectId

    const hubUrl = `${config.public.signalrBaseUrl}/hubs/board`
    connection = new signalR.HubConnectionBuilder()
      .withUrl(hubUrl, {
        accessTokenFactory: () => token
      })
      .withAutomaticReconnect()
      .build()

    connection.on('OnBoardEvent', (envelope: {
      eventId: string
      projectId: string
      entityType: string
      entityId: string
      action: string
      version: number
      occurredAt: string
      payload: unknown
      cardId?: string
    }) => {
      if (envelope.projectId !== projectId) return

      switch (envelope.entityType) {
        case 'Card':
          board.applyRealtimeCardEvent(projectId, envelope.entityId, envelope.action)
          board.signalCardContentEvent(envelope.entityId, envelope.entityType, envelope.action)
          break
        case 'Column':
          board.applyRealtimeColumnEvent(projectId, envelope.entityId, envelope.action)
          break
        case 'CardRelationship':
          // Affects relationship badges on both the source and target card, neither of
          // which is the relationship's own entityId — full refresh is simplest and this
          // event is comparatively rare (dependency add/remove, not routine card editing).
          board.fetchBoard(projectId)
          if (envelope.cardId) board.signalCardContentEvent(envelope.cardId, envelope.entityType, envelope.action)
          break
        case 'Comment':
        case 'ChecklistItem':
        case 'Attachment':
        case 'Spec':
        case 'Plan':
          // These don't render on the board card tile — nothing to patch here. If a
          // CardModal for this card happens to be open, it'll pick this signal up itself.
          if (envelope.cardId) board.signalCardContentEvent(envelope.cardId, envelope.entityType, envelope.action)
          break
        default:
          break
      }
    })

    connection.onreconnecting(() => {
      isReconnecting.value = true
    })
    connection.onreconnected(() => {
      isReconnecting.value = false
      connection?.invoke('JoinProject', projectId)
      board.fetchBoard(projectId)
    })
    connection.onclose(() => {
      isConnected.value = false
      isReconnecting.value = false
    })

    try {
      await connection.start()
      isConnected.value = true
      await connection.invoke('JoinProject', projectId)
    } catch {
      // silent — board still works without real-time
    }

    // Fallback for mobile browsers that freeze JS and suppress visibility/focus
    // events. Polls board state every 30s so missed events catch up within half
    // a minute even when WebSocket is suspended.
    if (import.meta.client) {
      pollInterval = setInterval(() => {
        if (activeProjectId && document.visibilityState === 'visible') {
          board.fetchBoard(activeProjectId)
        }
      }, 30_000)
    }
  }

  async function disconnect(projectId: string) {
    if (import.meta.client) {
      document.removeEventListener('visibilitychange', onVisibilityChange)
    }
    if (pollInterval !== null) {
      clearInterval(pollInterval)
      pollInterval = null
    }
    activeProjectId = null

    if (connection?.state === signalR.HubConnectionState.Connected) {
      try {
        await connection.invoke('LeaveProject', projectId)
      } catch {
        /* best effort */
      }
      await connection.stop()
    }
    connection = null
    isConnected.value = false
    isReconnecting.value = false
  }

  // Mobile browsers suspend JS/WebSocket when backgrounded. On return to
  // foreground the socket may be dead without SignalR noticing. Refetch board
  // state on visibility/focus restore; reconnect if the socket didn't survive.
  async function onForeground() {
    if (!activeProjectId) return

    board.fetchBoard(activeProjectId)

    if (connection && connection.state === signalR.HubConnectionState.Disconnected) {
      try {
        await connection.start()
        isConnected.value = true
        await connection.invoke('JoinProject', activeProjectId)
      } catch {
        /* silent — next foreground event will retry */
      }
    }
  }

  function onVisibilityChange() {
    if (document.visibilityState === 'visible') onForeground()
  }

  if (import.meta.client) {
    document.addEventListener('visibilitychange', onVisibilityChange)
  }

  return { connect, disconnect, isConnected, isReconnecting }
}
