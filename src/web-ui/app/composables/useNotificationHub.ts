import * as signalR from '@microsoft/signalr'
import type { NotificationItem } from '~/composables/useNotifications'

/**
 * Real-time push for the notification bell (D-51). NotificationHub is
 * user-scoped, not project-scoped — unlike useRealtime's board hub, this
 * connects once per session (from the default layout), not per-project.
 */
export function useNotificationHub() {
  const { getToken } = useAuthToken()
  const config = useRuntimeConfig()
  const { onNotificationReceived } = useNotifications()

  let connection: signalR.HubConnection | null = null

  async function connect() {
    if (connection) return

    const token = getToken()
    if (!token) return

    const hubUrl = `${config.public.signalrBaseUrl}/hubs/notifications`
    connection = new signalR.HubConnectionBuilder()
      .withUrl(hubUrl, {
        accessTokenFactory: () => token
      })
      .withAutomaticReconnect()
      .build()

    connection.on('onNotificationReceived', (notification: NotificationItem) => {
      onNotificationReceived(notification)
    })

    try {
      await connection.start()
    } catch {
      // silent — bell falls back to whatever fetchUnreadCount already loaded
      // at mount; clicking the bell still fetches the live list either way.
    }
  }

  async function disconnect() {
    if (connection?.state === signalR.HubConnectionState.Connected) {
      await connection.stop()
    }
    connection = null
  }

  return { connect, disconnect }
}
