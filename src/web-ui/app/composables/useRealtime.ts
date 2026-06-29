import * as signalR from '@microsoft/signalr'

export function useRealtime() {
  const { getToken } = useAuthToken()
  const board = useBoardStore()
  const config = useRuntimeConfig()

  let connection: signalR.HubConnection | null = null
  const isConnected = ref(false)
  const isReconnecting = ref(false)

  async function connect(projectId: string) {
    const token = getToken()
    if (!token) return

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
    }) => {
      if (envelope.projectId === projectId) {
        board.fetchBoard(projectId)
      }
    })

    connection.onreconnecting(() => {
      isReconnecting.value = true
    })
    connection.onreconnected(() => {
      isReconnecting.value = false
      connection?.invoke('JoinProject', projectId)
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
  }

  async function disconnect(projectId: string) {
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

  return { connect, disconnect, isConnected, isReconnecting }
}
