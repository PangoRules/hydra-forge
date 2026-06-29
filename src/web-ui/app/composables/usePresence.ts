import * as signalR from '@microsoft/signalr'

export function usePresence() {
  const { getToken } = useAuthToken()
  const store = usePresenceStore()

  let connection: signalR.HubConnection | null = null

  async function connect(projectId: string) {
    const token = getToken()
    if (!token) return

    connection = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/presence', {
        accessTokenFactory: () => token
      })
      .withAutomaticReconnect()
      .build()

    connection.on('UserJoined', (user: { userId: string, username: string, connectionId: string }) => {
      store.addUser(projectId, user)
    })

    connection.on('UserLeft', (user: { userId: string, username: string, connectionId: string }) => {
      store.removeUser(projectId, user.connectionId)
    })

    connection.on('CardFocused', (data: { userId: string, cardId: string }) => {
      store.setCardFocus(data.userId, data.cardId)
    })

    try {
      await connection.start()
      await connection.invoke('JoinProject', projectId)
    } catch {
      // silent — presence is non-critical
    }
  }

  async function focusCard(projectId: string, cardId: string) {
    if (connection?.state === signalR.HubConnectionState.Connected) {
      await connection.invoke('FocusCard', projectId, cardId)
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
  }

  return { connect, disconnect, focusCard }
}
