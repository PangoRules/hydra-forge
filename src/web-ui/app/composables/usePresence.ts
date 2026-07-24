import * as signalR from '@microsoft/signalr'

export function usePresence() {
  const { getToken } = useAuthToken()
  const store = usePresenceStore()
  const config = useRuntimeConfig()

  let connection: signalR.HubConnection | null = null

  // Tracks the card focused while the connection was still establishing.
  // Flushed after JoinProject completes so the broadcast is never silently lost.
  let pendingFocusProjectId: string | null = null
  let pendingFocusCardId: string | null = null

  async function connect(projectId: string) {
    const token = getToken()
    if (!token) return

    const hubUrl = `${config.public.signalrBaseUrl}/hubs/presence`
    connection = new signalR.HubConnectionBuilder()
      .withUrl(hubUrl, {
        accessTokenFactory: () => token
      })
      .withAutomaticReconnect()
      .build()

    connection.on('UserJoined', (user: { userId: string, username: string, connectionId: string }) => {
      store.addUser(projectId, user)
    })

    connection.on('CurrentUsers', (users: { userId: string, username: string, connectionId: string }[]) => {
      store.setProjectUsers(projectId, users)
    })

    connection.on('UserLeft', (user: { userId: string, username: string, connectionId: string }) => {
      store.removeUser(projectId, user.connectionId)
    })

    connection.on('CardFocused', (data: { userId: string, cardId: string }) => {
      store.setCardFocus(data.userId, data.cardId)
    })

    connection.on('CardUnfocused', (data: { userId: string }) => {
      store.clearCardFocus(data.userId)
    })

    connection.onreconnected(() => {
      connection?.invoke('JoinProject', projectId)
      if (pendingFocusProjectId === projectId && pendingFocusCardId) {
        connection?.invoke('FocusCard', projectId, pendingFocusCardId)
      }
    })

    try {
      await connection.start()
      await connection.invoke('JoinProject', projectId)
      // Flush any card focus that was set before the connection was ready.
      if (pendingFocusProjectId === projectId && pendingFocusCardId) {
        await connection.invoke('FocusCard', projectId, pendingFocusCardId)
      }
    } catch {
      // silent — presence is non-critical
    }
  }

  async function focusCard(projectId: string, cardId: string) {
    pendingFocusProjectId = projectId
    pendingFocusCardId = cardId
    if (connection?.state === signalR.HubConnectionState.Connected) {
      try {
        await connection.invoke('FocusCard', projectId, cardId)
      } catch {
        // silent — will retry on reconnect
      }
    }
    // If not connected yet, pending vars are flushed after JoinProject in connect()
  }

  async function unfocusCard(projectId: string) {
    pendingFocusCardId = null
    if (connection?.state === signalR.HubConnectionState.Connected) {
      try {
        await connection.invoke('UnfocusCard', projectId)
      } catch {
        // silent
      }
    }
  }

  async function disconnect(projectId: string) {
    pendingFocusProjectId = null
    pendingFocusCardId = null
    if (connection?.state === signalR.HubConnectionState.Connected) {
      try {
        await connection.invoke('LeaveProject', projectId)
      } catch {
        /* best effort */
      }
      await connection.stop()
    }
    connection = null
    store.setProjectUsers(projectId, [])
    store.clearAllFocusedCards()
  }

  return { connect, disconnect, focusCard, unfocusCard }
}
